using localscrape.Helpers;
using localscrape.Models;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Collections.ObjectModel;
using System.Drawing;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;

namespace localscrape.Browser
{
    public interface IBrowser
    {
        void NavigateToUrl(string Url);
        void NavigateToString(string PageSource);
        List<IWebElement> FindElements(By by);
        void CloseDriver();
        void ClickCSSSelector(string cssSelector);
        string GetPageSource();
        WebDriverWait GetWait(int seconds);
        void SetTimeout(int seconds);
        Screenshot GetScreenshot();
        List<Screenshot> ScreenShotWholePage();
        IWebElement? SafeGetElement(IWebElement element, By by);
    }

    public class BrowserService : IBrowser
    {
        private readonly IWebDriver _driver;
        private readonly ILogger _logger;
        private readonly IImageHelper _imageHelper;

        public string PageSource { get => _driver.PageSource; }
        private const int _retries = 3;
        private WebDriverWait _wait;

        public BrowserService(BrowserType browserType, ILogger logger)
        {
            _logger = logger;
            _driver = StartBrowser(browserType);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(5);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
            _imageHelper = new ImageHelper();

            _logger.LogInformation("BrowserService initialized with browser: {BrowserType}", browserType);
        }


        private IWebDriver StartBrowser(BrowserType browserType)
        {

            switch (browserType)
            {
                case BrowserType.Chrome:
                    new DriverManager().SetUpDriver(new ChromeConfig());
                    return new ChromeDriver();
                case BrowserType.Edge:
                    new DriverManager().SetUpDriver(new EdgeConfig());
                    return new EdgeDriver();
                default:
                    throw new ArgumentException("Unsupported browser type");
            }
        }

        public void NavigateToUrl(string Url)
        {
            _logger.LogInformation("Navigating to URL: {Url}", Url);
            try
            {
                _driver.Navigate().GoToUrl(Url);
            }
            catch (WebDriverTimeoutException ex)
            {
                _logger.LogWarning(ex, "Page load timeout. Attempting to stop loading.");
                IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
                js.ExecuteScript("window.stop();");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Browser Navigation Error.");
            }
        }


        public void CloseDriver()
        {
            _driver.Quit();
        }

        public List<IWebElement> FindElements(By by)
        {
            try
            {
                var elements = SafeFindElements(by);
                if (elements is null)
                    return new List<IWebElement>();
                return elements.ToList();
            }
            catch
            {
                return new List<IWebElement>(); 
            }
        }

        private ReadOnlyCollection<IWebElement>? SafeFindElements(By by, IWebElement? webElement = null)
        {
            for (int i = 0; i < _retries; i++)
            {
                try
                {
                    ReadOnlyCollection<IWebElement> elems;
                    if (webElement is null)
                    {
                        elems = _wait.Until(e => _driver.FindElements(by));
                    }
                    else
                    {
                        elems = _wait.Until(e => webElement?.FindElements(by));
                    }
                    return elems;
                }
                catch (StaleElementReferenceException)
                {
                    _logger.LogWarning($"Stale element detected. Retrying ({i + 1}/{_retries})...");
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception during element search.");
                    break;
                }

            }
            return null; 
        }


        public IWebElement? SafeGetElement(IWebElement element, By by)
        {
            return SafeFindElements(by, element)?.FirstOrDefault();
        }

        public string GetPageSource()
        {
            return PageSource;
        }

        public void NavigateToString(string PageSource)
        {
            _driver.Navigate().GoToUrl($"data:text/html;charset=utf-8,{Uri.EscapeDataString(PageSource)}");
        }

        public WebDriverWait GetWait(int seconds)
        {
            return new WebDriverWait(_driver, TimeSpan.FromSeconds(seconds));
        }

        public void SetTimeout(int seconds)
        {
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(seconds);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
        }

        public Screenshot GetScreenshot()
        {
            return ((ITakesScreenshot)_driver).GetScreenshot();
        }

        public List<Screenshot> ScreenShotWholePage()
        {
            var path = "\\\\192.168.50.11\\Public-Manga\\downloads\\Leviathan-LEE-Gyuntak\\blank\\blank.png";
            var emptyScreenshot = LoadScreenshotOrFallback(path);
            _logger.LogInformation($"Starting Screenshot function");
            List<Screenshot> screenshots = new();
            IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
            long totalHeight = Convert.ToInt64(js.ExecuteScript("return document.body.scrollHeight"));
            long viewportHeight = Convert.ToInt64(js.ExecuteScript("return window.innerHeight"));
            long currentY = 0;
            long lastY = 0;
            var maxRetries = 2;

            while (currentY + viewportHeight < totalHeight)
            {
                WaitForImagesToLoadInViewport(js);

                Screenshot currentScreenshot = ((ITakesScreenshot)_driver).GetScreenshot();

                int retryCount = 0;
                while (_imageHelper.AreScreenshotsHistogramSimilar(emptyScreenshot, currentScreenshot, 0.95) && retryCount < maxRetries)
                {
                    Thread.Sleep(1000);
                    retryCount++;
                    WaitForImagesToLoadInViewport(js);
                    currentScreenshot = ((ITakesScreenshot)_driver).GetScreenshot();
                }

                if (retryCount == maxRetries)
                {
                    _logger.LogDebug($"Still similar to empty after {maxRetries} retries. Continuing anyway...");
                }

                screenshots.Add(currentScreenshot);

                js.ExecuteScript($"window.scrollBy(0, {viewportHeight});");
                Thread.Sleep(500);

                currentY = Convert.ToInt64(js.ExecuteScript("return window.scrollY"));
                if (lastY != currentY)
                    lastY = currentY;
                else
                {
                    _logger.LogDebug($"Scrolled to the end of page");
                    break;
                }
            }

            long remainingHeight = totalHeight - currentY;
            if (remainingHeight > 0)
            {
                js.ExecuteScript($"window.scrollBy(0, {remainingHeight});");
                Thread.Sleep(500);
                screenshots.Add(((ITakesScreenshot)_driver).GetScreenshot());
            }

            _logger.LogInformation($"{screenshots.Count} were screenshotted");

            return screenshots;
        }

        private void WaitForImagesToLoadInViewport(IJavaScriptExecutor js, int maxAttempts = 10, int delayMs = 500)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                bool allLoaded = (bool)js.ExecuteScript(@"
                const viewportTop = window.scrollY;
                const viewportBottom = viewportTop + window.innerHeight;
                return Array.from(document.images).every(img => {
                    const rect = img.getBoundingClientRect();
                    const top = rect.top + window.scrollY;
                    const bottom = rect.bottom + window.scrollY;
                    const isVisible = bottom > viewportTop && top < viewportBottom;
                    return !isVisible || (img.complete && img.naturalHeight > 0);
                });
            ");
                if (allLoaded) return;
                Thread.Sleep(delayMs);
            }
        }

        public Bitmap LoadScreenshotOrFallback(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    string folder = AppDomain.CurrentDomain.BaseDirectory;
                    string fallbackPath = Path.Combine(folder, "fallback.png");

                    using (var bmp = new Bitmap(1, 1))
                    {
                        bmp.SetPixel(0, 0, Color.White);
                        bmp.Save(fallbackPath, System.Drawing.Imaging.ImageFormat.Png);
                    }

                    path = fallbackPath;
                }

                return new Bitmap(path); 
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load screenshot from '{path}'", ex);
            }
        }

        public void ClickCSSSelector(string cssSelector)
        {
            WebDriverWait wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(10));

            IWebElement button = wait.Until(
                ExpectedConditions.ElementToBeClickable(
                    By.CssSelector(cssSelector)
                )
            );
            ((IJavaScriptExecutor)_driver).ExecuteScript("arguments[0].scrollIntoView({block: 'center'});", button);
            button.Click();
        }
    }
}
