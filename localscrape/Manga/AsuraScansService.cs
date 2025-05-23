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
        public override string HomePage { get => "https://asuracomic.net/"; }
        public override string SeriesUrl { get => "https://asuracomic.net/series"; }
        
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
                    FetchedMangaSeries.Add(SingleManga);
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
                var mangaSeriesLink = SafeGetElement(titleBox,By.CssSelector("a"));
                if (mangaSeriesLink is null)
                    continue;
                var seriesLink = mangaSeriesLink.GetAttribute("href")??string.Empty;
                if (string.IsNullOrEmpty(seriesLink)) continue;
                var mangaTitle = ExtractMangaTitle(seriesLink);
                var latestChapterBox = SafeGetElement(titleBox,By.CssSelector("div.flex.flex-row.justify-between.rounded-sm"));
                var lastChapterAdded = ExtractChapterName(latestChapterBox?.Text.Trim());

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
                        Uri = chapter.GetAttribute("href") ?? string.Empty
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
                    ImageFileName = (image.GetAttribute("src")??string.Empty).Split('/').Last(),
                    FullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(), manga.MangaTitle, manga.ChapterName, 
                    (image.GetAttribute("src")??string.Empty).Split('/').Last()),
                    Uri = image.GetAttribute("src") ?? string.Empty
                })
                .Where(img => fileHelper.IsAnImage(img.ImageFileName) 
                    && !BlockedFileNames.Contains(img.ImageFileName) 
                    && !Regex.IsMatch(img.ImageFileName, "(http|small)")
                    && (img.ImageFileName.Length<100||img.ImageFileName.Contains("optimized")))
                .ToList();
            if (images.Count > 5)
            {
                return images;
            }
            return new List<MangaImages>();
        }
    }
}