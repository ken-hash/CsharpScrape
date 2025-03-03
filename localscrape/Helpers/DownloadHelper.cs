using localscrape.Models;
using System.Text.RegularExpressions;
namespace localscrape.Helpers
{
    public interface IDownloadHelper
    {
        DownloadObject CreateDownloadObject(string downloadPath, string uri);
    }

    public class DownloadHelper : IDownloadHelper
    {
        public DownloadObject CreateDownloadObject(string downloadPath, string uri)
        {
            DownloadObject dlObject = new()
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