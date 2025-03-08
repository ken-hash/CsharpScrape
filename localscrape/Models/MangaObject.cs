namespace localscrape.Models
{
    public class MangaObject
    {
        public int? ID { get; set; }
        public string? Title { get; set; }
        public string? LatestChapter { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? ExtraInformation { get; set; }
        public List<string> ChaptersDownloaded
        {
            get
            {
                return ExtraInformation!.Split(',').SkipLast(1).ToList();
            }
        }
    }
}