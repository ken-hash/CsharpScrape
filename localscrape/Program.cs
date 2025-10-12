using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Manga;
using localscrape.Models;
using localscrape.Repo;
using Microsoft.Extensions.Logging;
using Serilog;

var serilogger= new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
var factory = new LoggerFactory().AddSerilog(serilogger);
var logger = factory.CreateLogger("MangaScraperService");
DebugService debug = new(new FileHelper(logger));
BrowserService edgeBrowser = new(BrowserType.Edge, logger);
BrowserService chromeBrowser = new(BrowserType.Chrome, logger);
#if DEBUG
var isSingle = true;
#else
var isSingle = false;
#endif
MangaSeries? mangaSeriesOne = null;
isSingle = false;
if (isSingle)
{
    mangaSeriesOne = new MangaSeries
    {
        MangaTitle = "Hunter-X-Hunter",
        MangaChapters = new List<MangaChapter>(),
        MangaSeriesUri = "https://weebcentral.com/series/01J76XY7EXQV9RE9KQ3JYE0WZ9/Hunter-X-Hunter"
    };
}

MangaRepo asuraRepo = new("AsuraScans", logger);
chromeBrowser.SetTimeout(15);
RestService restService = new("http://192.168.50.11", logger);
MangaReaderRepo readerRepo = new(restService, logger);
//AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug, readerRepo, logger, mangaSeriesOne);
AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug, readerRepo, logger);
asuraScans.RunDebug = false;
asuraScans.RunProcess();

MangaRepo weebCentralRepo = new("WeebCentral", logger);
edgeBrowser.SetTimeout(15);
//WeebCentralService weebCentralService = new(weebCentralRepo, edgeBrowser, debug, logger);
WeebCentralService weebCentralService = new(weebCentralRepo, edgeBrowser, debug, logger, mangaSeriesOne);
weebCentralService.RunDebug = false;
weebCentralService.RunProcess();

MangaRepo flameScansRepo = new("FlameScans", logger);
edgeBrowser = new(BrowserType.Edge, logger);
FlameScansService flameScansService = new(flameScansRepo, edgeBrowser, debug, logger);
flameScansService.RunDebug = false;
flameScansService.RunProcess();