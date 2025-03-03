using OpenQA.Selenium;

namespace localscrape.Browser
{
    public interface IBrowser
    {
        void NavigateToUrl(string Url);
        void NavigateToString(string PageSource);
        List<IWebElement> FindElements(By by);
        void CloseDriver();
        string GetPageSource();
    }
}