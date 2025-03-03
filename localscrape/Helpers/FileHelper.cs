namespace localscrape.Helpers
{
    public interface IFileHelper
    {
        string GetMangaDownloadFolder();
        bool IsAnImage(string fileName);
        PlatformID GetOS();
        void WriteFile(string lines, string path);
        string ReadFile(string path);
    }

    public class FileHelper : IFileHelper
    {
        HashSet<string> allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        };

        public string GetMangaDownloadFolder()
        {
            PlatformID OS = GetOS();
            switch (OS)
            {
                case PlatformID.Unix:
                    return Path.Join("/", "mnt", "MangaPi", "downloads");
                default:
                    return Path.Join("\\\\192.168.50.11", "Public-Manga", "downloads");
            }
        }

        public bool IsAnImage(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return allowedExtensions.Contains(extension);
        }

        public PlatformID GetOS()
        {
            return Environment.OSVersion.Platform;
        }

        public void WriteFile(string lines, string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(path, lines);
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }

        public string ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return string.Empty;
                }
                using (StreamReader reader = new(path))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"{ex.Message}");
            }
        }
    }
}