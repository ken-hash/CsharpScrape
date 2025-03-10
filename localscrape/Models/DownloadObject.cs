namespace localscrape.Models
{
    public class DownloadObject
    {
        public required string Title { get; set; }
        public required string ChapterNum { get; set; }
        public required string FileId { get; set; }
        public required string Url { get; set; }
        public string? String64 { get; set; }
    }
}