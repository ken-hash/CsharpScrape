using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using System;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class WeebCentralService : MangaService
    {
        public override string? HomePage { get => "https://weebcentral.com/"; }
        public override string? SeriesUrl { get => "https://weebcentral.com/series/"; }

        private readonly MangaSeries? SingleManga;
        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "01J8ASTVA4P1F2KWD9BN8YMQH7.webp", "01JHXXEA5H4RYV3NAQVKGYAFVZ.jpg", "01JHXXCP5XR40JWCMV9R2VNVRV.jpg",
            "01JHXXFS4V8SNK0EVYCZ9T0WC0.jpg", "01JFH1FQQCE4978WHX0FVC4ZZR.jpg", "01JHY152CQBTB9G3C4P5TQEHV5.jpg",
            "01J8ASTVA4P1F2KWD9BN8YMQH7.webp", "01JHY1JHJB23CM6Z80A1HXN4RJ.jpg", "01JNT09EWDN41KFY2H5C1SSPNR.jpg",
            "01JNSZBT9VXJ5TNYEBAQDD5VZC.jpg", "01JNSWGS067VH92020JEW4XCK0.jpg", "01JNSX3SEYTK9MRTZ1CRSBGM9J.jpg",
            "brand.png"
        };

        public WeebCentralService(IMangaRepo repo, IBrowser browser, IDebugService debug, MangaSeries? mangaSeries = null)
            : base(repo, browser, debug)
        {
            SingleManga = mangaSeries;
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
                }
                else if (SingleManga != null)
                {
                    GetAllAvailableChapters(SingleManga);
                    FetchedMangaSeries.Add(SingleManga);
                }
                ProcessFetchedManga();
                SyncDownloadedChapters();
            }
            finally
            {
                CloseBrowser();
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            var allMangasInDb = GetAllMangaTitles();
            var mangaTitles = FindByCssSelector(".bg-base-100.hover\\:bg-base-300.flex.items-center.gap-4");

            foreach (var titleBox in mangaTitles)
            {
                var mangaSeriesLink = SafeGetElement(titleBox,By.CssSelector("a"));
                if (mangaSeriesLink is null)
                    continue;
                var seriesLink = mangaSeriesLink.GetAttribute("href");
                var mangaTitle = ExtractMangaTitle(seriesLink);
                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                    continue;
                var latestChapterBox = SafeGetElement(titleBox, By.CssSelector(".flex.items-center.gap-2.opacity-70"));
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
            var showAllChapters = FindByElements(By.CssSelector("button.hover\\:bg-base-300.p-2"));
            showAllChapters.First().Click();
            Thread.Sleep(3000);
            var chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapters/')]")).ToList();

            foreach (var chapter in chapterBoxes)
            {
                var chapterTextElem = SafeGetElement(chapter,By.CssSelector("span.grow.flex.items-center.gap-2 > span:first-child"));
                if (chapterTextElem is  null)
                    continue;
                var chapterName = ExtractChapterName(chapterTextElem.Text.Trim());
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
            var images = GetMangaImagesString64(manga);
            var filteredImages = images.Where(img => fileHelper.IsAnImage(img.ImageFileName!) 
                && !BlockedFileNames.Contains(img.ImageFileName!) 
                && !Regex.IsMatch(img.ImageFileName!, "-thumb-small\\.webp")
                && img.ImageFileName!.Length<20)
                .ToList();

            return filteredImages;
        }

        private List<MangaImages> GetMangaImagesString64(MangaChapter manga)
        {
            var fileHelper = GetFileHelper();
            List<MangaImages> mangaImages = new();
            Thread.Sleep(2000);
            List<IWebElement> images = FindByCssSelector("img");
            var fullPath = Path.Combine(fileHelper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!);
            mangaImages = ScreenShotWholePage(fullPath, HomePage!);
            return mangaImages;
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

            if (chapterText.Split(' ').Count() == 2)
            {
                return chapterText.Split(' ').Last();
            }

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
                        Console.WriteLine($"{chapterText} can't match");
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