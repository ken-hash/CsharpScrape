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
        private readonly MangaSeries? SingleManga;
        HashSet<string> blockedFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "close-icon.png", "logo.webp", "google.webp"
        };

        public List<MangaSeries> FetchedMangaSeries = new();

        public AsuraScansService(IMangaRepo repo, IBrowser browser, IDebugService debug, MangaSeries? mangaSeries = null) : base(repo, browser, debug)
        {
            HomePage = "https://asuracomic.net";
            SeriesUrl = "https://asuracomic.net/series";
            TableName = repo.GetTableName();
            if (mangaSeries is not null)
            {
                RunAllTitles = false;
                SingleManga = mangaSeries!;
            }
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
                    List<MangaObject> mangaInDb = base.GetAllMangaTitles();
                    List<MangaSeries> mangaNotInDb = FetchedMangaSeries.Where(e => !mangaInDb.Select(e => e.Title).ToList().Contains(e.MangaTitle)).ToList();
                    List<MangaSeries> fetchedMangasInDb = FetchedMangaSeries.Where(e => mangaInDb.Select(e => e.Title).ToList().Contains(e.MangaTitle)).ToList();
                    foreach (MangaSeries? manga in mangaNotInDb)
                    {
                        InsertNewManga(manga);
                        GetAllAvailableChapters(manga);
                    }
                    Dictionary<string, string> fetchedDict = FetchedMangaSeries.ToDictionary(k => k.MangaTitle!, v => v.MangaChapters!.First().ChapterName!);
                    foreach (MangaSeries? manga in fetchedMangasInDb)
                    {
                        MangaObject mangaDb = mangaInDb.First(e => e.Title == manga.MangaTitle);
                        if (manga.MangaChapters!.First().ChapterName != mangaDb.LatestChapter)
                        {
                            GetAllAvailableChapters(manga);
                            List<string> chaptersInDb = mangaDb.ExtraInformation!.Split(',').ToList();
                            List<string?> chaptersInSite = manga.MangaChapters!.Select(e => e.ChapterName).ToList();
                            List<string?> uniqueChapters = chaptersInSite.Except(chaptersInDb).ToList();
                            List<MangaChapter> mangaChaptersToDL = manga.MangaChapters!.Where(e => uniqueChapters.Contains(e.ChapterName)).ToList();
                            foreach (MangaChapter? chapter in mangaChaptersToDL)
                            {
                                if (string.IsNullOrEmpty(chapter.Uri))
                                    continue;
                                GoToMangaPage(manga, chapter.Uri!);
                                List<MangaImages> images = GetMangaImages(chapter);
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
        public override List<MangaSeries> GetMangaLinks()
        {
            List<IWebElement> mangaTitles = FindByCssSelector("div.grid.grid-rows-1.grid-cols-12.m-2");
            foreach (IWebElement titleBoxes in mangaTitles)
            {
                IWebElement mangaSeriesLink = titleBoxes.FindElement(By.CssSelector("a"));
                string mangaSeriesValue = mangaSeriesLink.GetAttribute("href");
                mangaSeriesValue.Replace(HomePage!, "");
                string mangaTitle = ExtractMangaTitle(mangaSeriesValue);
                string seriesLinkValue = $"{HomePage}{mangaSeriesValue}";
                IWebElement latestChapterBox = titleBoxes.FindElement(By.CssSelector("div.flex.flex-row.justify-between.rounded-sm"));
                string rawlatestChapterText = latestChapterBox.Text.Trim();
                string lastChapterAdded = ExtractChapterName(rawlatestChapterText);
                List<MangaChapter> inCompleteMangaChapterList = new() {
                    new MangaChapter
                    {
                        MangaTitle = mangaTitle,
                        ChapterName = lastChapterAdded
                    }
                };
                MangaSeries manga = new()
                {
                    MangaSeriesUri = seriesLinkValue,
                    MangaTitle = mangaTitle,
                    MangaChapters = inCompleteMangaChapterList
                };
                FetchedMangaSeries.Add(manga);
            }
            return FetchedMangaSeries;
        }

        public override void GetAllAvailableChapters(MangaSeries mangaSeries)
        {
            GoToSeriesPage(mangaSeries);
            List<IWebElement> chapterBoxes = FindByElements(By.XPath("//a[contains(@href, '/chapter/')]"));
            foreach (IWebElement chapter in chapterBoxes)
            {
                string chapterName = GetChapterName(chapter.Text.Trim());
                if (string.IsNullOrEmpty(chapterName))
                    continue;
                string url = chapter.GetAttribute("href");
                MangaChapter chapterObj = new()
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
        private string ExtractMangaTitle(string rawText)
        {
            Match match = Regex.Match(rawText, @"series\/([\w-]+)-(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            else
            {
                Match noPrefixMatch = Regex.Match(rawText, @"https:\/\/(www\.)?(asura).+\/manga\/([\w-]+)\/");
                if (noPrefixMatch.Success)
                {
                    return noPrefixMatch.Groups[3].Value;
                }
            }
            return string.Empty;
        }

        public override List<MangaImages> GetMangaImages(MangaChapter manga)
        {
            FileHelper helper = new();
            List<MangaImages> unfiltered = new();
            List<IWebElement> images = FindByElements(By.XPath("//img[@alt and normalize-space(@alt) != '']"));
            foreach (IWebElement image in images)
            {
                string url = image.GetAttribute("src");
                string fileName = url.Split('/').Last();
                if (helper.IsAnImage(fileName))
                {
                    string fullPath = Path.Combine(helper.GetMangaDownloadFolder(), manga.MangaTitle!, manga.ChapterName!, fileName);
                    unfiltered.Add(new MangaImages { ImageFileName = fileName, FullPath = fullPath, Uri = url });
                }
            }

            string pattern = @"-thumb-small\.webp";
            List<MangaImages> regexFiltered = unfiltered.Where(e => !Regex.IsMatch(e.ImageFileName!, pattern)).ToList();

            return regexFiltered.Where(e => !blockedFileNames.Contains(e.ImageFileName!)).ToList();
        }
        private string ExtractChapterName(string rawText)
        {
            string pattern = @"Chapter\s*(\d+(?:\.\d+)?)";
            MatchCollection matches = Regex.Matches(rawText, pattern);

            string numChapterLinks = string.Empty;

            if (matches.Count > 0)
            {
                numChapterLinks = matches[0].Value.Trim().Split().Last();
            }
            else
            {
                pattern = @"(Page|Episode)\n\t+(\d+)";
                Match pageMatch = Regex.Match(rawText, pattern);

                if (pageMatch.Success)
                {
                    numChapterLinks = pageMatch.Groups[2].Value.Trim().Split().Last();
                }
                else
                {
                    pattern = @"\b\d+\b";
                    MatchCollection numberMatches = Regex.Matches(rawText, pattern);

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
        private string GetChapterName(string rawText)
        {
            string chapterPattern = @"chapter\s(\d+(\.\d+)?)";
            Match match = Regex.Match(rawText, chapterPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return string.Empty;
        }
    }
}