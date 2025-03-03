using localscrape.Browser;
using localscrape.Debug;
using localscrape.Helpers;
using localscrape.Manga;
using localscrape.Models;
using localscrape.Repo;

MangaRepo asuraRepo = new("AsuraScans");
BrowserService chromeBrowser = new(BrowserType.Chrome);
DebugService debug = new(new FileHelper());
AsuraScansService asuraScans = new(asuraRepo, chromeBrowser, debug);
asuraScans.RunDebug = true;
asuraScans.RunProcess();