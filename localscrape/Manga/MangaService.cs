using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public abstract class MangaService
    {
        public abstract string? HomePage { get; }
        public abstract string? SeriesUrl { get; }
        public virtual string? TableName { get; protected set; }
        public bool RunDebug { get; set; } = false;
        public bool RunAllTitles { get; set; } = true;
        public List<MangaSeries> FetchedMangaSeries { get; } = new();

        private List<MangaObject> _allMangaObjects { get; set; }
        private readonly IDownloadHelper _downloadHelper;
        private readonly IFileHelper _fileHelper;
        private readonly IMangaRepo _repo;
        private readonly IDebugService _debug;
        private readonly IBrowser _browser;

        public MangaService(IMangaRepo repo, IBrowser browser, IDebugService debug)
        {
            _repo = repo;
            _browser = browser;
            _debug = debug;
            _fileHelper = debug.GetFileHelper();
            _allMangaObjects = GetAllMangas();
            TableName = repo.GetTableName();
            _downloadHelper = new DownloadHelper();
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
                string url = image.GetAttribute("src");
                string fileName = url.Split('/').Last();
                if (_fileHelper.IsAnImage(fileName))
                {
                    string fullPath = Path.Combine(_fileHelper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, fileName);
                    mangaImages.Add(new MangaImages { ImageFileName = fileName, FullPath = fullPath, Uri = url });
                }
            }
            return mangaImages;
        }

        public virtual void RunProcess()
        {
            Console.WriteLine($"Executing {this.GetType().Name} with RunAllTitles :{RunAllTitles}");
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, "Home", MangaSitePages.HomePage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(HomePage!);
                    _debug.WriteDebugFile(TableName!, MangaSitePages.HomePage, "Home", _browser.GetPageSource());
                }
                else
                {
                    _browser.NavigateToString(sourceDebug);
                }
            }
            else
            {
                _browser.NavigateToUrl(HomePage!);
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSitePages.ChapterPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(manga.MangaSeriesUri!);
                    _debug.WriteDebugFile(TableName!, MangaSitePages.ChapterPage, manga.MangaTitle!, _browser.GetPageSource());
                }
                else
                {
                    _browser.NavigateToString(sourceDebug);
                }
            }
            else
            {
                _browser.NavigateToUrl(manga.MangaSeriesUri!);
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSitePages.MangaPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(url);
                    _debug.WriteDebugFile(TableName!, MangaSitePages.MangaPage, manga.MangaTitle!, _browser.GetPageSource());
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
            var uniqueChapters = manga.MangaChapters!.Select(e => e.ChapterName).Except(chaptersInDb).ToList();
            var mangaChaptersToDL = manga.MangaChapters!.Where(e => uniqueChapters.Contains(e.ChapterName) 
                 && !string.IsNullOrEmpty(e.Uri))
                .DistinctBy(e=>e.Uri).ToList();

            foreach (var chapter in mangaChaptersToDL.OrderBy(e=>e.ChapterName, new NaturalSortComparer()))
            {
                GoToMangaPage(manga, chapter.Uri!);
                var images = GetMangaImages(chapter);
                if (images.Any()) AddImagesToDownload(images);
            }
        }

        public void ProcessFetchedManga()
        {
            var mangaInDb = GetAllMangaTitles();
            var mangaNotInDb = FetchedMangaSeries.Where(m => mangaInDb.All(dbManga => dbManga.Title != m.MangaTitle)).ToList();
            var fetchedMangasInDb = FetchedMangaSeries.Where(m => mangaInDb.Any(dbManga => dbManga.Title == m.MangaTitle)).ToList();

            foreach (var manga in mangaNotInDb)
            {
                InsertNewManga(manga);
                GetAllAvailableChapters(manga);
            }

            foreach (var manga in fetchedMangasInDb)
            {
                var mangaDb = mangaInDb.First(e => e.Title == manga.MangaTitle);
                if (manga.MangaChapters!.First().ChapterName != mangaDb.LatestChapter)
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
                var mangaObject = mangasInDb.First(e => e.Title == manga.MangaTitle);
                var latestChapterDownloaded = mangaObject.ChaptersDownloaded.Last();
                var fetchedLatestChapter= manga.MangaChapters!.First().ChapterName;
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

        public void CloseBrowser()
        {
            _browser.CloseDriver();
        }

        /// <summary>
        /// Convert MangaImages and insert it into the download queue
        /// </summary>
        /// <param name="mangaImages"></param>
        public void AddImagesToDownload(List<MangaImages> mangaImages)
        {
            foreach (MangaImages image in mangaImages)
            {
                var queue = _downloadHelper.CreateDownloadObject(image.FullPath!, image.Uri!, image.ImageFileName, image.Base64String);
                
                _repo.InsertQueue(queue);
            }
        }

        /// <summary>
        /// Insert a new manga into the database
        /// </summary>
        /// <param name="manga"></param>
        public void InsertNewManga(MangaSeries manga)
        {
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

        public virtual string ExtractChapterName(string rawText)
        {
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
                var string64 = screenshots[i].AsBase64EncodedString;
                mangaImages.Add(new MangaImages { ImageFileName = fileName, FullPath = fullFilePath, Base64String = string64 , Uri = domain});
            }
            return mangaImages;
        }
    }
}