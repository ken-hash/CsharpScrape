using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class WeebCentralService : MangaService
    {
        public override string HomePage => "https://weebcentral.com/";
        public override string SeriesUrl => "https://weebcentral.com/series/";

        private readonly MangaSeries? SingleManga;
        private readonly ILogger _logger;

        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "01J8ASTVA4P1F2KWD9BN8YMQH7.webp", "brand.png",
            "01JHXXEA5H4RYV3NAQVKGYAFVZ.jpg", "01JHXXCP5XR40JWCMV9R2VNVRV.jpg",
            "01JHXXFS4V8SNK0EVYCZ9T0WC0.jpg", "01JFH1FQQCE4978WHX0FVC4ZZR.jpg",
            "01JHY152CQBTB9G3C4P5TQEHV5.jpg", "01JHY1JHJB23CM6Z80A1HXN4RJ.jpg",
            "01JNT09EWDN41KFY2H5C1SSPNR.jpg", "01JNSZBT9VXJ5TNYEBAQDD5VZC.jpg",
            "01JNSWGS067VH92020JEW4XCK0.jpg", "01JNSX3SEYTK9MRTZ1CRSBGM9J.jpg"
        };

        public WeebCentralService(IMangaRepo repo, IBrowser browser, IDebugService debug, 
            ILogger logger, MangaSeries? mangaSeries = null)
            : base(repo, browser, debug,logger)
        {
            _logger = logger;
            SingleManga = mangaSeries;
            RunAllTitles = mangaSeries is null;
        }

        public override void RunProcess()
        {
            try
            {
                _logger.LogInformation("Starting WeebCentral scrape process.");
                base.RunProcess();

                if (RunAllTitles)
                {
                    _logger.LogInformation("Running full scrape for all titles.");
                    GoToHomePage();
                    GetMangaLinks();
                }
                else if (SingleManga != null)
                {
                    _logger.LogInformation($"Scraping single manga: {SingleManga.MangaTitle}");
                    GetAllAvailableChapters(SingleManga);
                    FetchedMangaSeries.Add(SingleManga);
                }

                ProcessFetchedManga();
                SyncDownloadedChapters();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scraping process.");
            }
            finally
            {
                CloseBrowser();
                _logger.LogInformation("Scraping process complete.");
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            _logger.LogInformation("Fetching manga links from homepage.");
            var allMangasInDb = GetAllMangaTitles();
            var mangaTitles = FindByCssSelector(".bg-base-100.hover\\:bg-base-300.flex.items-center.gap-4");

            foreach (var titleBox in mangaTitles)
            {
                var mangaSeriesLink = SafeGetElement(titleBox, By.CssSelector("a"));
                if (mangaSeriesLink is null)
                    continue;

                var seriesLink = mangaSeriesLink.GetAttribute("href") ?? string.Empty;
                if (string.IsNullOrEmpty(seriesLink)) continue;

                var mangaTitle = ExtractMangaTitle(seriesLink);
                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                    continue;

                var latestChapterBox = SafeGetElement(titleBox, By.CssSelector(".flex.items-center.gap-2.opacity-70"));
                var lastChapterAdded = ExtractChapterName(latestChapterBox?.Text.Trim());

                _logger.LogInformation($"Found series: {mangaTitle}, Latest Chapter: {lastChapterAdded}");

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
            _logger.LogInformation($"Getting chapters for: {mangaSeries.MangaTitle}");
            GoToSeriesPage(mangaSeries);
            var showAllChapters = FindByElements(By.CssSelector("button.hover\\:bg-base-300.p-2"));
            showAllChapters.First().Click();
            Thread.Sleep(3000);

            var chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapters/')]")).ToList();

            foreach (var chapter in chapterBoxes)
            {
                var chapterTextElem = SafeGetElement(chapter, By.CssSelector("span.grow.flex.items-center.gap-2 > span:first-child"));
                if (chapterTextElem is null)
                    continue;

                var chapterName = ExtractChapterName(chapterTextElem.Text.Trim());
                if (!string.IsNullOrEmpty(chapterName))
                {
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = chapter.GetAttribute("href") ?? string.Empty
                    });

                    _logger.LogDebug($"Added chapter {chapterName} for {mangaSeries.MangaTitle}");
                }
            }
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            _logger.LogInformation($"Getting images for {manga.MangaTitle} - Chapter {manga.ChapterName}");

            var fileHelper = GetFileHelper();
            var images = GetMangaImagesString64(manga);

            var filteredImages = images.Where(img =>
                fileHelper.IsAnImage(img.ImageFileName) &&
                !BlockedFileNames.Contains(img.ImageFileName) &&
                !Regex.IsMatch(img.ImageFileName, "-thumb-small\\.webp") &&
                img.ImageFileName.Length < 20).ToList();

            _logger.LogInformation($"Found { filteredImages.Count} valid images for {manga.MangaTitle} Chapter {manga.ChapterName}");
            return filteredImages;
        }

        private List<MangaImages> GetMangaImagesString64(MangaChapter manga)
        {
            var fileHelper = GetFileHelper();
            Thread.Sleep(1000);

            var fullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(), manga.MangaTitle, manga.ChapterName);
            _logger.LogDebug($"Saving images to: {fullPath}");

            return ScreenShotWholePage(fullPath, HomePage);
        }

        public override string ExtractMangaTitle(string rawText)
        {
            return rawText.Trim().Split('/').Last();
        }

        public override string ExtractChapterName(string? rawText)
        {
            if (string.IsNullOrEmpty(rawText))
                return string.Empty;

            var chapterText = rawText;
            if (chapterText.Split(' ').Length == 2)
                return chapterText.Split(' ').Last();

            string numChapterText = rawText;
            string season = string.Empty;

            MatchCollection matches = Regex.Matches(chapterText, @"Chapter\s*(\d+(?:\.\d+)?)");
            Match seasonMatch = Regex.Match(chapterText, @"(S\d+)");
            if (seasonMatch.Success)
                season = seasonMatch.Groups[1].Value;

            if (matches.Count > 0)
            {
                numChapterText = matches[0].Groups[1].Value.Trim();
            }
            else
            {
                Match pageEpisodeMatch = Regex.Match(chapterText, @"(Page|Episode)\s+(\d+)");
                if (pageEpisodeMatch.Success)
                {
                    numChapterText = pageEpisodeMatch.Groups[2].Value.Trim();
                }
                else
                {
                    MatchCollection numberMatches = Regex.Matches(chapterText, @"\b\d+\b");
                    if (numberMatches.Count > 0)
                    {
                        numChapterText = numberMatches[0].Value.Trim();
                    }
                    else
                    {
                        _logger.LogWarning($"Could not extract chapter number from: {chapterText}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(season) && numChapterText != null)
            {
                numChapterText = $"{season}-{numChapterText}";
            }

            return numChapterText!;
        }
    }
}
