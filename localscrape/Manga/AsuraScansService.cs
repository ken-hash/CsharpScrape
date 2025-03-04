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
        private readonly MangaSeries? SingleManga;
        private readonly HashSet<string> BlockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public AsuraScansService(IMangaRepo repo, IBrowser browser, IDebugService debug, MangaSeries? mangaSeries = null)
            : base(repo, browser, debug)
        {
            HomePage = "https://asuracomic.net";
            SeriesUrl = "https://asuracomic.net/series";
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
                var chapterName = GetChapterName(chapter.Text.Trim());
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