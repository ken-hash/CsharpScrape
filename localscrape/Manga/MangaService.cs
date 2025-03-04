using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class MangaService
    {
        public virtual string? HomePage { get; protected set; }
        public virtual string? SeriesUrl { get; protected set; }
        public virtual string? TableName { get; protected set; }
        public bool RunDebug { get; set; } = false;
        public bool RunAllTitles { get; set; } = true;
        public List<MangaChapter> MangaChapters { get; set; } = new();
        private List<MangaObject> _allMangaObjects { get; set; }
        public List<MangaSeries> FetchedMangaSeries { get; } = new();
        public readonly IFileHelper _fileHelper;

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
        }

        public virtual List<MangaSeries> GetMangaLinks()
        {
            return new List<MangaSeries>();
        }

        public virtual void GetAllAvailableChapters(MangaSeries mangaSeries)
        {
            throw new NotImplementedException();
        }

        public virtual List<MangaChapter> GetChaptersToDownload(MangaSeries mangaSeries)
        {
            return new List<MangaChapter>();
        }

        private List<MangaObject> GetAllMangas()
        {
            return _repo.GetMangas();
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
            Console.WriteLine($"Executing {this.GetType().Name}");
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, "Home", MangaSiteEnum.HomePage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(HomePage!);
                    _debug.WriteDebugFile(TableName!, MangaSiteEnum.HomePage, "Home", _browser.GetPageSource());
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSiteEnum.ChapterPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(manga.MangaSeriesUri!);
                    _debug.WriteDebugFile(TableName!, MangaSiteEnum.ChapterPage, manga.MangaTitle!, _browser.GetPageSource());
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
                string sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSiteEnum.MangaPage);
                if (string.IsNullOrWhiteSpace(sourceDebug))
                {
                    _browser.NavigateToUrl(url);
                    _debug.WriteDebugFile(TableName!, MangaSiteEnum.MangaPage, manga.MangaTitle!, _browser.GetPageSource());
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
            var chaptersInDb = mangaDb.ExtraInformation!.Split(',').ToList();
            var uniqueChapters = manga.MangaChapters!.Select(e => e.ChapterName).Except(chaptersInDb).ToList();
            var mangaChaptersToDL = manga.MangaChapters!.Where(e => uniqueChapters.Contains(e.ChapterName)).ToList();

            foreach (var chapter in mangaChaptersToDL)
            {
                if (!string.IsNullOrEmpty(chapter.Uri))
                {
                    GoToMangaPage(manga, chapter.Uri);
                    var images = GetMangaImages(chapter);
                    if (images.Any()) AddImagesToDownload(images);
                }
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
                string chapter = Directory.GetParent(image.FullPath!)!.Name;
                string title = Directory.GetParent(image.FullPath!)!.Parent!.Name;
                DownloadObject queue = new()
                {
                    ChapterNum = chapter,
                    Title = title,
                    FileId = image.ImageFileName!,
                    Url = image.Uri!,
                };
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
            var match = Regex.Match(rawText, @"Chapter\s*(\d+(?:\.\d+)?)");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        public virtual string GetChapterName(string rawText)
        {
            var match = Regex.Match(rawText, @"chapter\s(\d+(\.\d+)?)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}