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
            "desktop.jpg", "close-icon.png","angry.png","shock.png","happy.png","surprise.png","love.png",
            "discord.webp", "flame-scans-new-ad.jpg"
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
            var seriesCardElements = FindByElements(By.CssSelector("div.m_6d731127.mantine-Stack-root"));

            var mangaData = new Dictionary<string, (string SeriesLink, List<(string Chapter, string ChapterLink)>)>();

            foreach (var card in seriesCardElements)
            {
                try
                {
                    var stackRoots = card.FindElements(By.CssSelector(".m_6d731127.mantine-Stack-root")).ToList();

                    if (stackRoots.Count < 2)
                        continue;

                    var titleSection = stackRoots[0];
                    var chapterSection = stackRoots[1];

                    var titleAnchor = titleSection.FindElement(By.CssSelector("a[href^='/series/']"));
                    var title = titleAnchor.Text.Trim();
                    var seriesLink = titleAnchor.GetAttribute("href") ?? "";

                    var chapterAnchors = chapterSection.FindElements(By.CssSelector("a[href^='/series/']")).ToList();

                    var chapterRegex = new Regex(@"/series/\d+/\w+$");
                    var chapters = new List<(string Chapter, string ChapterLink)>();

                    foreach (var anchor in chapterAnchors)
                    {
                        var href = anchor.GetAttribute("href") ?? "";
                        if (!chapterRegex.IsMatch(href)) continue;

                        var chapterText = anchor.Text.Trim();
                        chapters.Add((chapterText, href));
                    }

                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(seriesLink))
                    {
                        mangaData[title] = (seriesLink, chapters);
                    }
                }
                catch (NoSuchElementException)
                {
                    continue;
                }
            }


            var keyList = mangaData.Keys.ToList();

            for (int x = 0; x < keyList.Count; x++)
            {
                var linkText = mangaData[keyList[x]].SeriesLink;
                var seriesLink = linkText;
                var mangaTitle = keyList[x];

                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                {
                    _logger.LogDebug($"Skipping title not in list: {mangaTitle}");
                    continue;
                }

                var lastChapterAdded = ExtractChapterName(mangaData[keyList[x]].Item2.First().Chapter);

                _logger.LogDebug($"Found: {mangaTitle}, Latest Chapter: {lastChapterAdded}");

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
                .Where(e=> !string.IsNullOrEmpty(e.Text))
                .Select(e => e.Text.Trim()).ToList();

            var chapterLinks = FindByElements(By.XPath($"//a[starts-with(@href, '{cleanedFilter}') and not(contains(substring(@href, string-length(@href) - 3), '.'))]"))
                .Select(e => e.GetAttribute("href") ?? string.Empty).ToList();

            for (int x = 0; x < chapterBoxes.Count; x++)
            {
                var chapterName = ExtractChapterName(chapterBoxes[x]);

                if (!string.IsNullOrEmpty(chapterName))
                {
                    var link = chapterLinks.Count >= 2 && chapterLinks.Count <= x+1 ? chapterLinks[x + 2] : chapterLinks[x];
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = link
                    });

                    _logger.LogDebug($"Found chapter: {chapterName} for {mangaSeries.MangaTitle}, Link: {link}");
                }
            }

            _logger.LogInformation($"Total chapters fetched: {mangaSeries.MangaChapters?.Count ?? 0}");
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            _logger.LogInformation($"Getting images for {manga.MangaTitle} - {manga.ChapterName}");

            var fileHelper = GetFileHelper();
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"));
            var validImages = new List<MangaImages>();
            foreach(var image in images)
            {
                var img = new MangaImages
                {
                    ImageFileName = Uri.UnescapeDataString(image.GetAttribute("src") ?? string.Empty).Split('/').Last().Split('?').First(),
                    FullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(false), 
                        manga.MangaTitle, 
                        manga.ChapterName, 
                        Uri.UnescapeDataString(image.GetAttribute("src") ?? string.Empty).Split('/').Last()).Split('?').First(),
                    Uri = image.GetAttribute("src") ?? string.Empty
                };
                if (fileHelper.IsAnImage(img.ImageFileName) && !BlockedFileNames.Contains(img.ImageFileName)
                && !Regex.IsMatch(img.ImageFileName!, "-thumb-small\\.webp") && Regex.IsMatch(img.Uri, "cdn"))
                    validImages.Add(img);
            }

            _logger.LogInformation($"Found {validImages.Count} images for {manga.MangaTitle} - {manga.ChapterName}");
            return validImages;
        }
    }
}