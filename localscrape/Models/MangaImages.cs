namespace localscrape.Models
{
    public class MangaImages
    {
        public required string FullPath { get; set; }
        public required string ImageFileName { get; set; }
        public required string Uri { get; set; }
        public string? Base64String { get; set; }
    }
}