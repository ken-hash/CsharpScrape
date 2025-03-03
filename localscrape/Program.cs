using localscrape.Manga;
using localscrape.Models;
using localscrape.Repo;
using localscrape.Browser;
using localscrape.Debug;

var asuraRepo = new MangaRepo("AsuraScans");
var chromeBrowser = new BrowserService(BrowserType.Chrome);
var debug = new DebugService();
var asuraScans = new AsuraScansService(asuraRepo, chromeBrowser, debug);
asuraScans.RunDebug = true;
asuraScans.RunProcess();