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
        MangaTitle = "why-i-quit-being-the-demon-king",
        MangaChapters = new List<MangaChapter>(),
        MangaSeriesUri = "https://weebcentral.com/series/01JJCBS4QMEAJA05GBS5NPACXS/why-i-quit-being-the-demon-king"
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