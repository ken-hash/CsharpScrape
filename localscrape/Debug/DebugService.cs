using localscrape.Helpers;
using localscrape.Models;

namespace localscrape.Debug
{
    public class DebugService
    {
        public string DebugFolder { get; set; }
        private readonly FileHelper _helper;

        public DebugService()
        {
            _helper = new FileHelper();
            DebugFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"DebugFiles");
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
    }
}
