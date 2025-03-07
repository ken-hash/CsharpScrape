using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Manga;
using localscrape.Models;
using localscrape.Repo;

DebugService debug = new(new FileHelper());
BrowserService edgeBrowser = new(BrowserType.Edge);
BrowserService chromeBrowser = new(BrowserType.Chrome);

MangaRepo asuraRepo = new("AsuraScans");
AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug);
asuraScans.RunDebug = false;
asuraScans.RunProcess();


MangaRepo weebCentralRepo = new("WeebCentral");
WeebCentralService weebCentralService = new(weebCentralRepo, edgeBrowser, debug);
weebCentralService.RunDebug = false;
weebCentralService.RunProcess();


MangaRepo flameScansRepo = new("FlameScans");
edgeBrowser = new(BrowserType.Edge);
FlameScansService flameScansService = new(flameScansRepo, edgeBrowser, debug);
flameScansService.RunDebug = false;
flameScansService.RunProcess();