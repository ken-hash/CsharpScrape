using localscrape.Browser;
using localscrape.Debug;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class FlameScansService : MangaService
    {
        private readonly MangaSeries? SingleManga;
        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public FlameScansService(IMangaRepo repo, IBrowser browser, IDebugService debug, MangaSeries? mangaSeries = null)
            : base(repo, browser, debug)
        {
            HomePage = "https://flamecomics.xyz";
            SeriesUrl = "https://flamecomics.xyz/series/";
            TableName = repo.GetTableName();
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
                }
                ProcessFetchedManga();
            }
            finally
            {
                CloseBrowser();
            }
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            var allMangasInDb = GetAllMangaTitles();
            var mangaTitles = FindByElements(By.XPath("//div[contains(@class, 'm_96bdd299 mantine-Grid-col')]"));
            foreach (var titleBox in mangaTitles)
            {
                var mangaSeriesLink = titleBox.FindElement(By.XPath("//a[contains(@class, 'SeriesCard_chapterImageLink__cDtXf')]"));
                var linkText = mangaSeriesLink.GetAttribute("href").Replace(HomePage!,"");
                var seriesLink = $"{HomePage}{linkText}";
                var mangaTitleBox = titleBox.FindElement(By.XPath("//a[contains(@class, 'mantine-Text-root')]"));
                var mangaTitle = mangaTitleBox.Text.Trim();
                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                    continue;
                var latestChapterBox = titleBox.FindElement(By.XPath("//div[contains(@class, 'SeriesCard_chapterPillWrapper__0yOPE')]"));
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
            var cleanedFilter = mangaSeries.MangaSeriesUri!.Replace(HomePage!,"");
            var wholeChapterBox = FindByCssSelector("#__next > main > div > div > div > div.m_96bdd299.mantine-Grid-col.__m__-r2q9 > div > div:nth-child(3) > div.m_d57069b5.mantine-ScrollArea-root > div.m_c0783ff9.mantine-ScrollArea-viewport").First();
            var chapterBoxes = wholeChapterBox.FindElements(By.XPath($"//a[contains(@href, '{cleanedFilter}')]"));

            foreach (var chapter in chapterBoxes)
            {
                var chapterBox = chapter.FindElement(By.XPath("//div[contains(@class, 'm_c0783ff9 mantine-Stack-root')]"));
                var chapterNameBox = chapterBox.FindElement(By.XPath("//p[contains(@class, 'mantine-focus-auto m_b6d8b162 mantine-Text-root')]"));
                var chapterName = GetChapterName(chapterNameBox.Text.Trim());
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
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"))
                .Select(image => new MangaImages
                {
                    ImageFileName = image.GetAttribute("src").Split('/').Last(),
                    FullPath = Path.Combine(_fileHelper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, image.GetAttribute("src").Split('/').Last()),
                    Uri = image.GetAttribute("src")
                })
                .Where(img => _fileHelper.IsAnImage(img.ImageFileName!) && !BlockedFileNames.Contains(img.ImageFileName!) && !Regex.IsMatch(img.ImageFileName!, "-thumb-small\\.webp"))
                .ToList();

            return images;
        }
    }
}