using Microsoft.Extensions.Logging;

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
        private readonly ILogger _logger;

        private readonly HashSet<string> allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"
        };

        public FileHelper(ILogger logger)
        {
            _logger = logger;
        }

        public string GetMangaDownloadFolder()
        {
            PlatformID OS = GetOS();
            string path = OS switch
            {
                PlatformID.Unix => Path.Join("/", "mnt", "MangaPi", "downloads"),
                _ => Path.Join("\\\\192.168.50.11", "Public-Manga", "downloads"),
            };
            _logger.LogInformation($"Resolved download folder: {path}");
            return path;
        }

        public bool IsAnImage(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            bool result = allowedExtensions.Contains(extension);
            _logger.LogDebug($"Checked image file: {fileName}, Result: {result}");
            return result;
        }

        public PlatformID GetOS()
        {
            var os = Environment.OSVersion.Platform;
            _logger.LogInformation($"Detected OS: {os}");
            return os;
        }

        public void WriteFile(string lines, string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    _logger.LogInformation($"Created directory: {directory}");
                }

                File.WriteAllText(path, lines);
                _logger.LogInformation($"Successfully wrote to file: {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error writing to file: {path}");
                throw new Exception($"Failed to write to file: {ex.Message}");
            }
        }

        public string ReadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    _logger.LogWarning($"File does not exist: {path}");
                    return string.Empty;
                }

                string content = File.ReadAllText(path);
                _logger.LogInformation($"Successfully read file: {path}");
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading file: {path}");
                throw new Exception($"Failed to read file: {ex.Message}");
            }
        }
    }
}
