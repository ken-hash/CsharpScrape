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
var isSingle = false;
#else
var isSingle = false;
#endif
MangaSeries? mangaSeriesOne = null;
 //isSingle = true;
if (isSingle)
{
    mangaSeriesOne = new MangaSeries
    {
        MangaTitle = "magic-academys-genius-blinker",
        MangaChapters = new List<MangaChapter>(),
        MangaSeriesUri = "https://asuracomic.net/series/magic-academys-genius-blinker-04464a29"
    };
}

MangaRepo asuraRepo = new("AsuraScans", logger);
chromeBrowser.SetTimeout(15);
RestService restService = new("http://192.168.50.11", logger);
MangaReaderRepo readerRepo = new(restService, logger);
AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug, readerRepo, logger, mangaSeriesOne);
asuraScans.RunDebug = false;
asuraScans.RunProcess();

MangaRepo weebCentralRepo = new("WeebCentral", logger);
edgeBrowser.SetTimeout(15);
WeebCentralService weebCentralService = new(weebCentralRepo, edgeBrowser, debug, logger);
weebCentralService.RunDebug = false;
weebCentralService.RunProcess();

MangaRepo flameScansRepo = new("FlameScans", logger);
edgeBrowser = new(BrowserType.Edge, logger);
FlameScansService flameScansService = new(flameScansRepo, edgeBrowser, debug, logger);
flameScansService.RunDebug = false;
flameScansService.RunProcess();