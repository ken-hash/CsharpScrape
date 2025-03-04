﻿using localscrape.Models;
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
    }

    public class BrowserService : IBrowser
    {
        private readonly IWebDriver _driver;
        public string PageSource { get => _driver.PageSource; }

        public BrowserService(BrowserType browserType)
        {
            _driver = StartBrowser(browserType);
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
            _driver.Navigate().GoToUrl(Url);
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
    }
}
