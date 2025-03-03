namespace localscrape.Helpers
{
    public class FileHelper
    {
        HashSet<string> allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        };

        public string GetMangaDownloadFolder()
        {
            var OS = GetOS();
            switch (OS)
            {
                case PlatformID.Unix:
                    return Path.Join("/", "mnt", "MangaPi", "downloads");
                default:
                    return Path.Join("\\\\192.168.50.11", "Public-Manga", "downloads");
            }
        }

        public bool isAnImage(string fileName)
        {
            var extension = Path.GetExtension(fileName);
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
                string directory = Path.GetDirectoryName(path)??Directory.GetCurrentDirectory();
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
                using (StreamReader reader = new StreamReader(path))
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