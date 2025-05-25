using localscrape.Models;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;
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

        public string PageSource { get => _driver.PageSource; }
        private const int _retries = 3;
        private WebDriverWait _wait;

        public BrowserService(BrowserType browserType, ILogger logger)
        {
            _logger = logger;
            _driver = StartBrowser(browserType);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(5);
            _wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));

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
            _logger.LogInformation($"Starting Screenshot function");
            List<Screenshot> screenshots = new();
            IJavaScriptExecutor js = (IJavaScriptExecutor)_driver;
            Thread.Sleep(1500);
            long totalHeight = Convert.ToInt64(js.ExecuteScript("return document.body.scrollHeight"));
            long viewportHeight = Convert.ToInt64(js.ExecuteScript("return window.innerHeight"));
            long currentY = 0;

            while (currentY + viewportHeight < totalHeight)
            {
                screenshots.Add(((ITakesScreenshot)_driver).GetScreenshot());

                js.ExecuteScript($"window.scrollBy(0, {viewportHeight});");
                Thread.Sleep(1000); 

                currentY = Convert.ToInt64(js.ExecuteScript("return window.scrollY"));
            }

            long remainingHeight = totalHeight - currentY;
            if (remainingHeight > 0)
            {
                js.ExecuteScript($"window.scrollBy(0, {remainingHeight});");
                Thread.Sleep(500); 
                screenshots.Add(((ITakesScreenshot)_driver).GetScreenshot());
            }
            _logger.LogInformation($"Reached end of page");
            return screenshots;
        }
    }
}
