using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Models;
using localscrape.Repo;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public abstract class MangaService
    {
        public abstract string HomePage { get; }
        public abstract string SeriesUrl { get; }
        public virtual string TableName { get; protected set; }
        public bool RunDebug { get; set; } = false;
        public bool RunAllTitles { get; set; } = true;
        public List<MangaSeries> FetchedMangaSeries { get; } = new();

        private List<MangaObject> _allMangaObjects { get; set; }
        private readonly IFileHelper _fileHelper;
        private readonly IMangaRepo _repo;
        private readonly IDebugService _debug;
        private readonly IBrowser _browser;
        private readonly RabbitRepo _rabbitRepo;
        private readonly ILogger _logger;

        public MangaService(IMangaRepo repo, IBrowser browser, IDebugService debug, ILogger logger)
        {
            _repo = repo;
            _browser = browser;
            _debug = debug;
            _fileHelper = debug.GetFileHelper();
            _allMangaObjects = GetAllMangas();
            TableName = repo.GetTableName();
            _rabbitRepo = new RabbitRepo(logger);
            _logger = logger;
        }


        public abstract List<MangaSeries> GetMangaLinks();

        public abstract void GetAllAvailableChapters(MangaSeries mangaSeries);

        private List<MangaObject> GetAllMangas()
        {
            return _repo.GetMangas();
        }

        public void UpdateMangaSeries(MangaObject manga)
        {
            _repo.UpdateManga(manga);
        }

        public List<MangaObject> GetAllMangaTitles()
        {
            return _allMangaObjects;
        }

        /// <summary>
        /// Grabs all the images from the manga page
        /// </summary>
        /// <param name="manga"></param>
        /// <returns></returns>
        public virtual List<MangaImages> GetMangaImages(MangaChapter manga)
        {

            List<MangaImages> mangaImages = new();
            Thread.Sleep(2000);
            List<IWebElement> images = FindByCssSelector("img");

            foreach (IWebElement image in images)
            {
                string url = image.GetAttribute("src")??string.Empty;
                if (string.IsNullOrEmpty(url)) continue;
                string fileName = url.Split('/').Last();
                if (_fileHelper.IsAnImage(fileName))
                {
                    string fullPath = Path.Combine(_fileHelper.GetMangaDownloadFolder(), manga.MangaTitle, manga.ChapterName, fileName);
                    mangaImages.Add(new MangaImages { ImageFileName = fileName, FullPath = fullPath, Uri = url });
                }
            }
            _logger.LogInformation($"Found {mangaImages.Count} image(s) for chapter '{manga.ChapterName}' of '{manga.MangaTitle}'");
            return mangaImages;
        }

        public virtual void RunProcess()
        {
            _logger.LogInformation($"Executing {GetType().Name} with RunAllTitles: {RunAllTitles}");
            //override this
            //usually checks home page for latest updated manga that includes chapter links
            //checks if manga chapters is in db 
            //and if the latest chapter is synced with the db
            //if not, then checks series page to grab all chapters
            //run all chapters in db vs available chapters in site
            //all chapters that are not in the db are then to be added to download queue
        }

        /// <summary>
        /// Navigates to the home page of the manga site
        /// if RunDebug is true, it will check if there is a debug file for the home page
        /// to avoid hitting the site multiple times
        /// if there is no debug file, it will create one
        /// </summary>
        public void GoToHomePage()
        {
            if (RunDebug)
            {
                string sourceDebug = _debug.ReadDebugFile(TableName, "Home", MangaSitePages.HomePage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _logger.LogInformation($"Navigating to homepage: {HomePage}");
                    _browser.NavigateToUrl(HomePage);
                    _debug.WriteDebugFile(TableName, MangaSitePages.HomePage, "Home", _browser.GetPageSource());
                }
                else
                {
                    _browser.NavigateToString(sourceDebug);
                }
            }
            else
            {
                _browser.NavigateToUrl(HomePage);
            }
        }

        /// <summary>
        /// Same as GoToHomePage but for the series page
        /// </summary>
        /// <param name="manga"></param>
        public void GoToSeriesPage(MangaSeries manga)
        {
            if (RunDebug)
            {
                string sourceDebug = _debug.ReadDebugFile(TableName, manga.MangaTitle, MangaSitePages.ChapterPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(manga.MangaSeriesUri);
                    _debug.WriteDebugFile(TableName, MangaSitePages.ChapterPage, manga.MangaTitle, _browser.GetPageSource());
                }
                else
                {
                    _browser.NavigateToString(sourceDebug);
                }
            }
            else
            {
                _browser.NavigateToUrl(manga.MangaSeriesUri);
            }
        }

        /// <summary>
        /// Same as GoToHomePage but for the manga page
        /// </summary>
        /// <param name="manga"></param>
        /// <param name="url"></param>
        public void GoToMangaPage(MangaSeries manga, string url)
        {
            if (RunDebug)
            {
                string sourceDebug = _debug.ReadDebugFile(TableName, manga.MangaTitle, MangaSitePages.MangaPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(url);
                    _debug.WriteDebugFile(TableName, MangaSitePages.MangaPage, manga.MangaTitle, _browser.GetPageSource());
                }
                else
                {
                    _browser.NavigateToString(sourceDebug);
                }
            }
            else
            {
                _browser.NavigateToUrl(url);
            }
        }

        public virtual void ProcessUpdatedChapters(MangaSeries manga, MangaObject mangaDb)
        {
            GetAllAvailableChapters(manga);
            var chaptersInDb = mangaDb.ChaptersDownloaded;
            var uniqueChapters = manga.MangaChapters?.Select(e => e.ChapterName).Except(chaptersInDb).ToList();
            var mangaChaptersToDL = manga.MangaChapters?.Where(e => uniqueChapters?.Contains(e.ChapterName) ?? false 
                 && !string.IsNullOrEmpty(e.Uri))
                .DistinctBy(e=>e.Uri ?? string.Empty).ToList();

            _logger.LogInformation($"Processing updated chapters for: {manga.MangaTitle}");

            if (mangaChaptersToDL is null || mangaChaptersToDL.Count == 0)
            {
                _logger.LogInformation($"No new chapters found for: {manga.MangaTitle}");
                return;
            }

            foreach (var chapter in mangaChaptersToDL!.OrderBy(e=>e.ChapterName, new NaturalSortComparer()))
            {
                if (string.IsNullOrEmpty(chapter.Uri))
                    continue;
                GoToMangaPage(manga, chapter.Uri);
                var images = GetMangaImages(chapter);
                if (images.Any()) 
                    AddImagesToDownload(images);
            }
        }

        public void ProcessFetchedManga()
        {
            var mangaInDb = GetAllMangaTitles();
            var mangaNotInDb = FetchedMangaSeries.Where(m => mangaInDb.All(dbManga => dbManga.Title != m.MangaTitle)).ToList();
            var fetchedMangasInDb = FetchedMangaSeries.Where(m => mangaInDb.Any(dbManga => dbManga.Title == m.MangaTitle 
                || dbManga.Title == m.MangaTitle?.ToLower())).ToList();

            foreach (var manga in mangaNotInDb)
            {
                InsertNewManga(manga);
                GetAllAvailableChapters(manga);
                FetchedMangaSeries.Add(manga);
            }

            foreach (var manga in fetchedMangasInDb)
            {
                var mangaDb = mangaInDb.First(e => e.Title == manga.MangaTitle);
                if (manga.MangaChapters is null) continue;
                if (manga.MangaChapters.First().ChapterName != mangaDb.LatestChapter)
                {
                    ProcessUpdatedChapters(manga, mangaDb);
                }
            }
        }

        public void SyncDownloadedChapters()
        {
            var mangasInDb = GetAllMangaTitles();
            foreach (var manga in FetchedMangaSeries)
            {
                var mangaObject = mangasInDb.FirstOrDefault(e => e.Title == manga.MangaTitle);
                if (mangaObject is null)
                    continue;
                if (mangaObject.ChaptersDownloaded.Count < 1)
                    continue;
                var latestChapterDownloaded = mangaObject.ChaptersDownloaded.Last();
                if (manga.MangaChapters is null) continue;
                var fetchedLatestChapter= manga.MangaChapters.First().ChapterName;
                if (fetchedLatestChapter == latestChapterDownloaded && mangaObject.LatestChapter != fetchedLatestChapter)
                {
                    mangaObject.LatestChapter = latestChapterDownloaded;
                    mangaObject.LastUpdated = DateTime.Now;
                    UpdateMangaSeries(mangaObject);
                }
            }
        }

        public void GoToUrl(string url)
        {
            _browser.NavigateToUrl(url);
        }

        public List<IWebElement> FindByTagName(string tagName)
        {
            return _browser.FindElements(By.TagName(tagName));
        }

        public List<IWebElement> FindByCssSelector(string cssSelector)
        {
            return _browser.FindElements(By.CssSelector(cssSelector));
        }

        public List<IWebElement> FindByElements(By by)
        {
            return _browser.FindElements(by);
        }

        public IWebElement? SafeGetElement(IWebElement element, By by)
        {
            return _browser.SafeGetElement(element, by);
        }

        public void CloseBrowser()
        {
            _browser.CloseDriver();
        }

        /// <summary>
        /// Convert MangaImages and insert it into the download queue
        /// </summary>
        /// <param name="mangaImages"></param>
        public async void AddImagesToDownload(List<MangaImages> mangaImages)
        {

            string chapter = Directory.GetParent(mangaImages.First().FullPath)!.Name;
            string title = Directory.GetParent(mangaImages.First().FullPath)!.Parent!.Name;
            var downloadObj = new DownloadObject {
                Title = title,
                ChapterNum = chapter,
                MangaImages = mangaImages
            };
            _logger.LogInformation($"Sending {mangaImages.Count} image(s) to download queue for: {title} - Chapter: {chapter}");

            await _rabbitRepo.Send(downloadObj);
        }

        /// <summary>
        /// Insert a new manga into the database
        /// </summary>
        /// <param name="manga"></param>
        public void InsertNewManga(MangaSeries manga)
        {
            _logger.LogInformation($"Inserting new manga: {manga.MangaTitle}");

            if (string.IsNullOrEmpty(manga.MangaTitle))
                throw new Exception($"\'{manga.MangaTitle}\' is invalid.");
            if (!_repo.DoesExist(manga.MangaTitle))
                _repo.InsertManga(new MangaObject { Title = manga.MangaTitle });
        }

        public virtual string ExtractMangaTitle(string rawText)
        {
            var match = Regex.Match(rawText, @"series\/([\w-]+)-(\w+)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public virtual string ExtractChapterName(string? rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return string.Empty;
            var match = Regex.Match(rawText, @"Chapter\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public WebDriverWait GetBrowserWait(int seconds)
        {
            return _browser.GetWait(seconds);
        }

        public IFileHelper GetFileHelper()
        {
            return _fileHelper;
        }

        public Screenshot GetScreenshot()
        {
            return _browser.GetScreenshot();
        }

        public List<MangaImages> ScreenShotWholePage(string fullPath, string domain)
        {
            List<MangaImages> mangaImages = new();
            var screenshots = _browser.ScreenShotWholePage();
            for(int i = 0; i < screenshots.Count; i++)
            {
                string fileName = $"{i}.png";
                string fullFilePath = Path.Combine(fullPath, fileName);
                byte[] raw = screenshots[i].AsByteArray;
                long quality = 80L;

                using var srcStream = new MemoryStream(raw);
                using var img = Image.FromStream(srcStream);

                using var outStream = new MemoryStream();
                var jpegCodec = ImageCodecInfo.GetImageEncoders()
                                   .First(codec => codec.FormatID == System.Drawing.Imaging.ImageFormat.Jpeg.Guid);
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(
                    Encoder.Quality,
                    Math.Clamp(quality, 1L, 100L)
                );

                img.Save(outStream, jpegCodec, encoderParams);

                var string64 = Convert.ToBase64String(outStream.ToArray());
                mangaImages.Add(new MangaImages { ImageFileName = fileName, FullPath = fullFilePath, Base64String = string64 , Uri = domain});
            }
            return mangaImages;
        }
    }
}