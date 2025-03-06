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
            "desktop.jpg", "close-icon.png"
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
            var mangaTitleTexts = FindByElements(By.XPath("//a[contains(@class, 'mantine-Text-root')]"))
                .Where(e=>!e.Text.Contains("Chapter"))
                .Select(e=>e.Text.Trim()).ToList();
            var latestChapterTexts = FindByElements(By.XPath("//div[contains(@class, 'SeriesCard_chapterPillWrapper__0yOPE')]"))
                .Select(e => e.Text.Trim()).ToList();
            var mangaSeriesLinks = FindByElements(By.XPath("//a[contains(@class, 'SeriesCard_chapterImageLink__cDtXf')]"));

            for (int x=0; x< mangaSeriesLinks.Count;x++)
            {
                var linkText = mangaSeriesLinks[x].GetAttribute("href").Replace(HomePage!,"");
                var seriesLink = $"{HomePage}{linkText}";
                var mangaTitle = mangaTitleTexts[x];
                if (!allMangasInDb.Any(e => e.Title == mangaTitle))
                    continue;
                var lastChapterAdded = ExtractChapterName(latestChapterTexts[x*2]);

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
            var chapterBoxes = FindByElements(By.XPath("//p[contains(@class, 'mantine-Text-root') and @data-size='md' and @data-line-clamp='true']"));
            var chapterLinks = FindByElements(By.XPath($"//a[starts-with(@href, '{cleanedFilter}') and not(contains(substring(@href, string-length(@href) - 3), '.'))]"));

            for (int x=0;x<chapterBoxes.Count;x++)
            {
                var chapterName = GetChapterName(chapterBoxes[x].Text.Trim());
                if (!string.IsNullOrEmpty(chapterName))
                {
                    mangaSeries.MangaChapters ??= new List<MangaChapter>();
                    mangaSeries.MangaChapters.Add(new MangaChapter
                    {
                        MangaTitle = mangaSeries.MangaTitle,
                        ChapterName = chapterName,
                        Uri = $"{chapterLinks[x].GetAttribute("href")}"
                    });
                }
            }
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"))
                .Select(image => new MangaImages
                {
                    ImageFileName = image.GetAttribute("src").Split('/').Last().Split('?').First(),
                    FullPath = Path.Combine(_fileHelper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, image.GetAttribute("src").Split('/').Last()),
                    Uri = image.GetAttribute("src")
                })
                .Where(img => _fileHelper.IsAnImage(img.ImageFileName!) && !BlockedFileNames.Contains(img.ImageFileName!) && !Regex.IsMatch(img.ImageFileName!, "-thumb-small\\.webp"))
                .ToList();

            return images;
        }
    }
}