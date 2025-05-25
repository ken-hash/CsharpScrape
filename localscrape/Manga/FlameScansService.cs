using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class FlameScansService : MangaService
    {
        public override string HomePage { get => "https://flamecomics.xyz"; }
        public override string SeriesUrl { get => "https://flamecomics.xyz/series/"; }

        private readonly MangaSeries? SingleManga;
        private readonly ILogger _logger;

        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "desktop.jpg", "close-icon.png","angry.png","shock.png","happy.png","surprise.png","love.png"
        };

        public FlameScansService(
            IMangaRepo repo,
            IBrowser browser,
            IDebugService debug,
            ILogger logger,
            MangaSeries? mangaSeries = null
        ) : base(repo, browser, debug, logger)
        {
            SingleManga = mangaSeries;
            RunAllTitles = mangaSeries is null;
            _logger = logger;
        }

        public override void RunProcess()
        {
            _logger.LogInformation($"Starting FlameScansService.RunProcess with RunAllTitles = {RunAllTitles}");

            try
            {
                base.RunProcess();

                if (RunAllTitles)
                {
                    _logger.LogInformation("Running full title scan for FlameScans.");
                    GoToHomePage();
                    GetMangaLinks();
                }
                else if (SingleManga != null)
                {
                    _logger.LogInformation($"Running single manga scrape for: {SingleManga.MangaTitle}");
                    GetAllAvailableChapters(SingleManga);
                    FetchedMangaSeries.Add(SingleManga);
                }

                ProcessFetchedManga();
                SyncDownloadedChapters();
            }
            finally
            {
                _logger.LogInformation("RunProcess complete. Closing browser.");
                CloseBrowser();
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            _logger.LogInformation("Scraping manga links from series page...");

            var allMangasInDb = GetAllMangaTitles();
            var mangaTitleTexts = FindByElements(By.XPath("//a[contains(@class, 'mantine-Text-root')]"))
                .Where(e => !e.Text.Contains("Chapter"))
                .Select(e => e.Text.Trim()).ToList();

            var latestChapterTexts = FindByElements(By.XPath("//div[contains(@class, 'SeriesCard_chapterPillWrapper__0yOPE')]"))
                .Select(e => e.Text.Trim()).ToList();

            var mangaSeriesLinks = FindByElements(By.XPath("//a[contains(@class, 'SeriesCard_chapterImageLink__cDtXf')]"))
                .Select(e => (e.GetAttribute("href") ?? string.Empty).Replace(HomePage, "")).ToList();

            for (int x = 0; x < mangaSeriesLinks.Count; x++)
            {
                var linkText = mangaSeriesLinks[x];
                var seriesLink = $"{HomePage}{linkText}";
                var mangaTitle = mangaTitleTexts[x];

                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                {
                    _logger.LogDebug($"Skipping title not in list: {mangaTitle}");
                    continue;
                }

                var lastChapterAdded = ExtractChapterName(latestChapterTexts[x * 2]);

                _logger.LogDebug($"Found: {mangaTitle}, Last Chapter: {lastChapterAdded}");

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
            _logger.LogInformation($"Fetching all chapters for: {mangaSeries.MangaTitle}");

            GoToSeriesPage(mangaSeries);
            var cleanedFilter = mangaSeries.MangaSeriesUri.Replace(HomePage, "");

            var chapterBoxes = FindByElements(By.XPath("//p[contains(@class, 'mantine-Text-root') and @data-size='md' and @data-line-clamp='true']"))
                .Select(e => e.Text.Trim()).ToList();

            var chapterLinks = FindByElements(By.XPath($"//a[starts-with(@href, '{cleanedFilter}') and not(contains(substring(@href, string-length(@href) - 3), '.'))]"))
                .Select(e => e.GetAttribute("href") ?? string.Empty).ToList();

            for (int x = 0; x < chapterBoxes.Count; x++)
            {
                var chapterName = ExtractChapterName(chapterBoxes[x]);

                if (!string.IsNullOrEmpty(chapterName))
                {
                    var link = chapterLinks.Count >= 2 ? chapterLinks[x + 2] : chapterLinks[x];
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = link
                    });

                    _logger.LogDebug($"Found chapter: {chapterName}, Link: {link}");
                }
            }

            _logger.LogInformation($"Total chapters fetched: {mangaSeries.MangaChapters?.Count ?? 0}");
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            _logger.LogInformation($"Getting images for {manga.MangaTitle} - {manga.ChapterName}");

            var fileHelper = GetFileHelper();
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"))
                .Select(image =>
                new MangaImages
                {
                    ImageFileName = Uri.UnescapeDataString(image.GetAttribute("src") ?? string.Empty.Split('/').Last().Split('?').First()),
                    FullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(), manga.MangaTitle, manga.ChapterName, Uri.UnescapeDataString(image.GetAttribute("src") ?? string.Empty.Split('/').Last())),
                    Uri = image.GetAttribute("src") ?? string.Empty
                })
                .Where(img => fileHelper.IsAnImage(img.ImageFileName) && !BlockedFileNames.Contains(img.ImageFileName) && !Regex.IsMatch(img.ImageFileName!, "-thumb-small\\.webp"))
                .ToList();

            return images;
        }
    }
}