using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Models;
using localscrape.Repo;
using OpenQA.Selenium;
using System.Text.RegularExpressions;

namespace localscrape.Manga
{
    public class AsuraScansService : MangaService
    {
        public new string HomePage = "https://asuracomic.net";
        public new string SeriesUrl = "https://asuracomic.net/series";
        public new string TableName = "AsuraScans";
        private readonly MangaSeries? SingleManga;
        HashSet<string> blockedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public List<MangaSeries> FetchedMangaSeries = new List<MangaSeries>();

        public AsuraScansService(MangaRepo repo, BrowserService browser, DebugService debug, MangaSeries? mangaSeries = null) : base(repo, browser, debug)
        {
            if (mangaSeries is not null)
            {
                RunAllTitles = false;
                SingleManga = mangaSeries!;
            }
            base.HomePage = HomePage;
            base.SeriesUrl = SeriesUrl;
            base.TableName = TableName;
        }

        public override List<MangaSeries> GetMangaLinks()
        {
            var mangaTitles = FindByCssSelector("div.grid.grid-rows-1.grid-cols-12.m-2");
            foreach (var titleBoxes in mangaTitles)
            {
                var mangaSeriesLink = titleBoxes.FindElement(By.CssSelector("a"));
                var mangaSeriesValue = mangaSeriesLink.GetAttribute("href");
                mangaSeriesValue.Replace(HomePage, "");
                var mangaTitle = ExtractMangaTitle(mangaSeriesValue);
                var seriesLinkValue = $"{HomePage}{mangaSeriesValue}";
                var latestChapterBox = titleBoxes.FindElement(By.CssSelector("div.flex.flex-row.justify-between.rounded-sm"));
                var rawlatestChapterText = latestChapterBox.Text.Trim();
                var lastChapterAdded = ExtractChapterName(rawlatestChapterText);
                var inCompleteMangaChapterList = new List<MangaChapter>() {
                    new MangaChapter 
                    {
                        MangaTitle = mangaTitle, 
                        ChapterName = lastChapterAdded 
                    }
                };
                var manga = new MangaSeries 
                { 
                    MangaSeriesUri = seriesLinkValue, 
                    MangaTitle = mangaTitle, 
                    MangaChapters = inCompleteMangaChapterList 
                };
                FetchedMangaSeries.Add(manga);
            }
            return FetchedMangaSeries;
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
                    var mangaInDb = base.GetAllMangaTitles();
                    var mangaNotInDb = FetchedMangaSeries.Where(e => !mangaInDb.Select(e => e.Title).ToList().Contains(e.MangaTitle)).ToList();
                    var fetchedMangasInDb = FetchedMangaSeries.Where(e => mangaInDb.Select(e => e.Title).ToList().Contains(e.MangaTitle)).ToList();
                    foreach (var manga in mangaNotInDb)
                    {
                        InsertNewManga(manga);
                        GetAllAvailableChapters(manga);
                    }
                    var fetchedDict = FetchedMangaSeries.ToDictionary(k => k.MangaTitle!, v => v.MangaChapters!.First().ChapterName!);
                    foreach (var manga in fetchedMangasInDb)
                    {
                        var mangaDb = mangaInDb.First(e => e.Title == manga.MangaTitle);
                        if (manga.MangaChapters!.First().ChapterName != mangaDb.LatestChapter)
                        {
                            GetAllAvailableChapters(manga);
                            var chaptersInDb = mangaDb.ExtraInformation!.Split(',').ToList();
                            var chaptersInSite = manga.MangaChapters!.Select(e => e.ChapterName).ToList();
                            var uniqueChapters = chaptersInSite.Except(chaptersInDb).ToList();
                            var mangaChaptersToDL = manga.MangaChapters!.Where(e => uniqueChapters.Contains(e.ChapterName)).ToList();
                            foreach (var chapter in mangaChaptersToDL)
                            {
                                if (string.IsNullOrEmpty(chapter.Uri))
                                    continue;
                                GoToMangaPage(manga, chapter.Uri!);
                                var images = GetMangaImages(chapter);
                                if (images.Count > 0)
                                    AddImagesToDownload(images);
                            }
                        }
                    }
                }
                else
                {
                    GetAllAvailableChapters(SingleManga!);
                }
            }
            finally
            {
                CloseBrowser();
            }
        }

        private string ExtractMangaTitle(string rawText)
        {
            var match = Regex.Match(rawText, @"series\/([\w-]+)-(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            else
            {
                var noPrefixMatch = Regex.Match(rawText, @"https:\/\/(www\.)?(asura).+\/manga\/([\w-]+)\/");
                if (noPrefixMatch.Success)
                {
                    return noPrefixMatch.Groups[3].Value;
                }
            }
            return string.Empty;
        }

        private string ExtractChapterName(string rawText)
        {
            var pattern = @"Chapter\s*(\d+(?:\.\d+)?)";
            var matches = Regex.Matches(rawText, pattern);

            string numChapterLinks = string.Empty;

            if (matches.Count > 0)
            {
                numChapterLinks = matches[0].Value.Trim().Split().Last();
            }
            else
            {
                pattern = @"(Page|Episode)\n\t+(\d+)";
                var pageMatch = Regex.Match(rawText, pattern);

                if (pageMatch.Success)
                {
                    numChapterLinks = pageMatch.Groups[2].Value.Trim().Split().Last();
                }
                else
                {
                    pattern = @"\b\d+\b";
                    var numberMatches = Regex.Matches(rawText, pattern);

                    if (numberMatches.Count > 0)
                    {
                        numChapterLinks = numberMatches[0].Value.Trim().Split().Last();
                    }
                    else
                    {
                        Console.WriteLine($"{rawText} can't match");
                    }
                }
            }
            return numChapterLinks;
        }

        public override void GetAllAvailableChapters(MangaSeries mangaSeries)
        {
            GoToSeriesPage(mangaSeries);
            var chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapter/')]"));
            foreach(var chapter in chapterBoxes)
            {
                var chapterName = GetChapterName(chapter.Text.Trim());
                if (string.IsNullOrEmpty(chapterName))
                    continue;
                var url = chapter.GetAttribute("href");
                var chapterObj = new MangaChapter 
                {
                    MangaTitle = mangaSeries.MangaTitle,
                    ChapterName = chapterName, 
                    Uri = url 
                };
                if (mangaSeries.MangaChapters is null)
                    mangaSeries.MangaChapters = new List<MangaChapter>();
                mangaSeries.MangaChapters.Add(chapterObj);
            }
        }

        private string GetChapterName(string rawText)
        {
            var chapterPattern = @"chapter\s(\d+(\.\d+)?)";
            var match = Regex.Match(rawText, chapterPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            var helper = new FileHelper();
            var unfiltered = new List<MangaImages>();
            var images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"));
            foreach (var image in images)
            {
                var url = image.GetAttribute("src");
                var fileName = url.Split('/').Last();
                if (helper.isAnImage(fileName))
                {
                    var fullPath = Path.Combine(helper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, fileName);
                    unfiltered.Add(new MangaImages { ImageFileName = fileName, FullPath = fullPath, Uri = url });
                }
            }

            var pattern = @"-thumb-small\.webp";
            var regexFiltered = unfiltered.Where(e => !Regex.IsMatch(e.ImageFileName!, pattern)).ToList();

            return regexFiltered.Where(e=>!blockedFileNames.Contains(e.ImageFileName!)).ToList();
        }
    }
}