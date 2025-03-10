using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class AsuraScansService : MangaService
    {
        public override string? HomePage { get => "https://asuracomic.net"; }
        public override string? SeriesUrl { get => "https://asuracomic.net/series"; }
        
        private readonly IMangaReaderRepo _readerRepo;
        private readonly MangaSeries? SingleManga;
        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public AsuraScansService(IMangaRepo repo, IBrowser browser, IDebugService debug, IMangaReaderRepo readerRepo, MangaSeries? mangaSeries = null)
            : base(repo, browser, debug)
        {
            SingleManga = mangaSeries;
            _readerRepo = readerRepo;
            RunAllTitles = mangaSeries is null;
        }

        public override void RunProcess()
        {
            try
            {
                base.RunProcess();
                if (RunAllTitles)
                {
                    GoToHomePage();
                    GetMangaLinks();
                    SyncDownloadedChapters();
                }
                else if (SingleManga != null)
                {
                    GetAllAvailableChapters(SingleManga);
                }
                ProcessFetchedManga();
                _ = _readerRepo.UpdateLatestUpdate("Asura", HomePage!).Result;
            }
            finally
            {
                CloseBrowser();
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            var mangaTitles = FindByCssSelector("div.grid.grid-rows-1.grid-cols-12.m-2");
            foreach (var titleBox in mangaTitles)
            {
                var mangaSeriesLink = titleBox.FindElement(By.CssSelector("a"));
                var seriesLink = mangaSeriesLink.GetAttribute("href");
                var mangaTitle = ExtractMangaTitle(seriesLink);
                var latestChapterBox = titleBox.FindElement(By.CssSelector("div.flex.flex-row.justify-between.rounded-sm"));
                var lastChapterAdded = ExtractChapterName(latestChapterBox.Text.Trim());

                FetchedMangaSeries.Add(new MangaSeries
                {
                    MangaSeriesUri = seriesLink,
                    MangaTitle = mangaTitle,
                    MangaChapters = new List<MangaChapter>
                    {
                        new() { MangaTitle = mangaTitle, ChapterName = lastChapterAdded }
                    }
                });
            }
            return FetchedMangaSeries;
        }

        public override void GetAllAvailableChapters(MangaSeries mangaSeries)
        {
            GoToSeriesPage(mangaSeries);
            var chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapter/')]")).ToList();

            foreach (var chapter in chapterBoxes)
            {
                var chapterName = ExtractChapterName(chapter.Text.Trim());
                if (!string.IsNullOrEmpty(chapterName))
                {
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = chapter.GetAttribute("href")
                    });
                }
            }
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            var fileHelper = GetFileHelper();
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"))
                .Select(image => new MangaImages
                {
                    ImageFileName = image.GetAttribute("src").Split('/').Last(),
                    FullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, image.GetAttribute("src").Split('/').Last()),
                    Uri = image.GetAttribute("src")
                })
                .Where(img => fileHelper.IsAnImage(img.ImageFileName!) && !BlockedFileNames.Contains(img.ImageFileName!) && !Regex.IsMatch(img.ImageFileName!, "http"))
                .ToList();
            return images;
        }
    }
}