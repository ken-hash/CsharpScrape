using localscrape.Models;
using System.Text.RegularExpressions;
namespace localscrape.Helpers
{
    public interface IDownloadHelper
    {
        DownloadObject CreateDownloadObject(string downloadPath, string uri, string? fileName=null, string? string64=null);
    }

    public class DownloadHelper : IDownloadHelper
    {
        public DownloadObject CreateDownloadObject(string downloadPath, string uri, string? fileName, string? string64)
        {
            string chapter = Directory.GetParent(downloadPath)!.Name;
            string title = Directory.GetParent(downloadPath)!.Parent!.Name;

            DownloadObject dlObject = new()
            {
                Title = title,
                ChapterNum = chapter,
                FileId = string.IsNullOrEmpty(fileName) ? uri.Split('/').Last() : fileName,
                Url = uri,
                String64 = string64
            };

            if (Regex.IsMatch(dlObject.FileId, @"\.(webp|jpeg)$", RegexOptions.IgnoreCase))
            {
                dlObject.FileId = Regex.Replace(dlObject.FileId, @"\.(webp|jpeg)$", ".jpg", RegexOptions.IgnoreCase);
            }
            return dlObject;
        }
    }
}