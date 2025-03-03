using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;

namespace localscrape.Manga
{
    public class MangaService
    {
        public virtual string? HomePage { get; set; }
        public virtual string? SeriesUrl { get; set; }
        public virtual string? TableName { get; set; }
        public bool RunDebug { get; set; } = false;
        public bool RunAllTitles { get; set; } = true;
        public List<MangaChapter> MangaChapters { get; set; } = new List<MangaChapter>();
        private List<MangaObject> _allMangaObjects { get; set; }
        private readonly MangaRepo _repo;
        private readonly DebugService _debug;
        private readonly IBrowser _browser;

        public MangaService(MangaRepo repo, IBrowser browser, DebugService debug)
        {
            _repo = repo;
            _browser = browser;
            _debug = debug;
            _allMangaObjects = GetAllMangas();
        }

        public virtual List<MangaSeries> GetMangaLinks()
        {
            return new List<MangaSeries>();
        }

        public virtual bool ValidateTitle(string mangaTitle)
        {
            return false;
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

        public virtual List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            var helper = new FileHelper();
            var mangaImages = new List<MangaImages>();
            var images = FindByCssSelector("img");
            foreach(var image in images)
            {
                var url = image.GetAttribute("src");
                var fileName =  url.Split('/').Last();
                if (helper.isAnImage(fileName))
                {
                    var fullPath = Path.Combine(helper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, fileName);
                    mangaImages.Add(new MangaImages { ImageFileName = fileName, FullPath = fullPath, Uri = url });
                }
            }
            return mangaImages;
        }

        public virtual void RunProcess()
        {
            Console.WriteLine($"Executing {this.GetType().Name}");
        }

        public void GoToHomePage()
        {
            if (RunDebug)
            {
                var sourceDebug = _debug.ReadDebugFile(TableName!, "Home", MangaSiteEnum.HomePage);
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

        public void GoToSeriesPage(MangaSeries manga)
        {
            if (RunDebug)
            {
                var sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSiteEnum.ChapterPage);
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

        public void GoToMangaPage(MangaSeries manga, string url)
        {
            if (RunDebug)
            {
                var sourceDebug = _debug.ReadDebugFile(TableName!, manga.MangaTitle!, MangaSiteEnum.MangaPage);
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
        public void AddImagesToDownload(List<MangaImages> mangaImages)
        {
            foreach(var image in mangaImages)
            {
                var chapter = Directory.GetParent(image.FullPath!)!.Name;
                var title = Directory.GetParent(image.FullPath!)!.Parent!.Name;
                var queue = new DownloadObject {
                    ChapterNum = chapter,
                    Title = title,
                    FileId = image.ImageFileName!,
                    Url = image.Uri!,
                };
                _repo.InsertQueue(queue);
            }
        }

        public void InsertNewManga(MangaSeries manga)
        {
            if (!_repo.DoesExist(manga.MangaTitle))
                _repo.InsertManga(new MangaObject { Title = manga.MangaTitle });
        }
    }
}