using localscrape.Helpers;
using localscrape.Models;

namespace localscrape.Debug
{
    public interface IDebugService
    {
        string DebugFolder { get; set; }
        void WriteDebugFile(string tableName, MangaSiteEnum site, string mangaTitle, string writeLogs);
        string ReadDebugFile(string tableName, string mangaTitle, MangaSiteEnum mangaSite);
        IFileHelper GetFileHelper();
    }

    public class DebugService : IDebugService
    {
        public string DebugFolder { get; set; }
        private readonly IFileHelper _helper;

        public DebugService(IFileHelper fileHelper)
        {
            _helper = fileHelper;
            DebugFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DebugFiles");
        }

        public void WriteDebugFile(string tableName, MangaSiteEnum site, string mangaTitle, string writeLogs)
        {
            _helper.WriteFile(writeLogs, Path.Combine(DebugFolder, tableName, mangaTitle, $"{Enum.GetName(site)}.html"));
        }

        public string ReadDebugFile(string tableName, string mangaTitle, MangaSiteEnum site)
        {
            try
            {
                return _helper.ReadFile(Path.Combine(DebugFolder, tableName, mangaTitle, $"{Enum.GetName(site)}.html"));
            }
            catch
            {
                return string.Empty;
            }
        }
        public IFileHelper GetFileHelper()
        {
            return _helper;
        }
    }
}
