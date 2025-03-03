using localscrape.Models;
using System.Text.RegularExpressions;
namespace localscrape.Helpers
{
    public class DownloadHelper
    {
        public DownloadObject CreateDownloadObject(string downloadPath, string uri)
        {
            var dlObject = new DownloadObject
            {
                Title = Path.GetFileName(Path.GetDirectoryName(downloadPath))!,
                ChapterNum = Path.GetFileName(downloadPath),
                FileId = uri.Split('/').Last(),
                Url = uri
            };

            if (Regex.IsMatch(dlObject.FileId, @"\.(webp|jpeg)$", RegexOptions.IgnoreCase))
            {
                dlObject.FileId = Regex.Replace(dlObject.FileId, @"\.(webp|jpeg)$", ".jpg", RegexOptions.IgnoreCase);
            }
            return dlObject;
        }
    }
}