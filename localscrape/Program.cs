using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Manga;
using localscrape.Models;
using localscrape.Repo;

DebugService debug = new(new FileHelper());
BrowserService edgeBrowser = new(BrowserType.Edge);
BrowserService chromeBrowser = new(BrowserType.Chrome);
#if DEBUG
var isSingle = true;
#else
var isSingle = false;
#endif
MangaSeries? mangaSeriesOne = null;

if (isSingle)
{
    mangaSeriesOne = new MangaSeries
    {
        MangaTitle = "Auto-Hunting-With-My-Clones",
        MangaChapters = new List<MangaChapter>(),
        MangaSeriesUri = "https://weebcentral.com/series/01J76XYH3NP2PBAA7D0ASA1GA8/Auto-Hunting-With-My-Clones"
    };
}

MangaRepo asuraRepo = new("AsuraScans");
chromeBrowser.SetTimeout(15);
RestService restService = new("http://192.168.50.11");
MangaReaderRepo readerRepo = new(restService);
AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug, readerRepo);
asuraScans.RunDebug = false;
asuraScans.RunProcess();

MangaRepo weebCentralRepo = new("WeebCentral");
edgeBrowser.SetTimeout(15);
WeebCentralService weebCentralService = new(weebCentralRepo, edgeBrowser, debug, mangaSeriesOne);
weebCentralService.RunDebug = false;
weebCentralService.RunProcess();

MangaRepo flameScansRepo = new("FlameScans");
edgeBrowser = new(BrowserType.Edge);
FlameScansService flameScansService = new(flameScansRepo, edgeBrowser, debug);
flameScansService.RunDebug = false;
flameScansService.RunProcess();