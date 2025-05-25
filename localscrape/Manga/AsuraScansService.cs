using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger _logger;

        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public AsuraScansService(
            IMangaRepo repo,
            IBrowser browser,
            IDebugService debug,
            IMangaReaderRepo readerRepo,
            ILogger logger,
            MangaSeries? mangaSeries = null
        ) : base(repo, browser, debug, logger)
        {
            SingleManga = mangaSeries;
            _readerRepo = readerRepo;
            RunAllTitles = mangaSeries is null;
            _logger = logger;
        }


        public override void RunProcess()
        {
            try
            {
                _logger.LogInformation($"Starting AsuraScansService.RunProcess with RunAllTitles = {RunAllTitles}");
                base.RunProcess();
                if (RunAllTitles)
                {
                    _logger.LogInformation("Running full title scan for AsuraScans.");
                    GoToHomePage();
                    GetMangaLinks();
                    SyncDownloadedChapters();
                }
                else if (SingleManga != null)
                {
                    _logger.LogInformation($"Running single manga scrape for: {SingleManga.MangaTitle}");
                    GetAllAvailableChapters(SingleManga);
                    FetchedMangaSeries.Add(SingleManga);
                }
                ProcessFetchedManga();
                _ = _readerRepo.UpdateLatestUpdate("Asura", HomePage!).Result;
            }
            finally
            {
                _logger.LogInformation("RunProcess complete. Closing browser.");
                CloseBrowser();
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            _logger.LogInformation("Scraping manga links from series page.");
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
                _logger.LogDebug($"Found manga: {mangaTitle}, Latest chapter: {lastChapterAdded}");
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
            _logger.LogInformation($"Fetched {FetchedMangaSeries.Count} manga titles.");
            return FetchedMangaSeries;
        }

        public override void GetAllAvailableChapters(MangaSeries mangaSeries)
        {
            _logger.LogInformation($"Fetching all available chapters for: {mangaSeries.MangaTitle}");
            GoToSeriesPage(mangaSeries);
            var chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapter/')]")).ToList();

            foreach (var chapter in chapterBoxes)
            {
                var chapterName = ExtractChapterName(chapter.Text.Trim());
                var uri = chapter.GetAttribute("href") ?? string.Empty;
                if (!uri.Contains(mangaSeries.MangaTitle)) 
                    continue;
                _logger.LogDebug($"Found chapter: {chapterName}");
                if (!string.IsNullOrEmpty(chapterName))
                {
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = uri
                    });
                }
            }
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            _logger.LogInformation($"Getting images for {manga.MangaTitle} - {manga.ChapterName}");
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
            _logger.LogDebug($"Image count after filtering: {images.Count}");
            if (images.Count > 5)
            {
                _logger.LogInformation("Sufficient image count, returning image list.");
                return images;
            }
            _logger.LogWarning($"Too few images ({images.Count}) for {manga.MangaTitle} - {manga.ChapterName}, skipping.");
            return new List<MangaImages>();
        }
    }
}