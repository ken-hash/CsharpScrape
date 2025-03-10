using localscrape.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;

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
    }

    public class BrowserService : IBrowser
    {
        private readonly IWebDriver _driver;
        public string PageSource { get => _driver.PageSource; }

        public BrowserService(BrowserType browserType)
        {
            _driver = StartBrowser(browserType);
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(5);
        }

        private IWebDriver StartBrowser(BrowserType browserType)
        {
            switch (browserType)
            {
                case BrowserType.Chrome:
                    return new ChromeDriver();
                case BrowserType.Edge:
                    return new EdgeDriver();
                default:
                    throw new ArgumentException("Unsupported browser type");
            }
        }

        public void NavigateToUrl(string Url)
        {
            try
            {
                _driver.Navigate().GoToUrl(Url);
            }
            catch (WebDriverTimeoutException)
            {
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
            return _driver.FindElements(by).ToList();
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
        }

        public Screenshot GetScreenshot()
        {
            return ((ITakesScreenshot)_driver).GetScreenshot();
        }

        public List<Screenshot> ScreenShotWholePage()
        {
            List<Screenshot> screenshots = new();
            long lastHeight = 0;
            long newHeight = 1;

            while (true)
            {
                Screenshot screenshot = ((ITakesScreenshot)_driver).GetScreenshot();

                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollBy(0, window.innerHeight);");
                Thread.Sleep(1000);

                lastHeight = newHeight;
                newHeight = (long)(_driver as IJavaScriptExecutor)!.ExecuteScript("return document.body.scrollHeight");

                screenshots.Add(screenshot);
                long scrollPosition = (long)((IJavaScriptExecutor)_driver).ExecuteScript("return window.scrollY + window.innerHeight");
                long totalHeight = (long)((IJavaScriptExecutor)_driver).ExecuteScript("return document.body.scrollHeight");

                if (scrollPosition >= totalHeight)
                    break;
            }
            return screenshots;
        }
    }
}
