namespace localscrape.Models
{
    public class MangaObject
    {
        public int? ID { get; set; }
        public string? Title { get; set; }
        public string? LatestChapter { get; set; }
        public DateTime LastUpdated { get; set; }
        public string? ExtraInformation { get; set; }
        public virtual string? TableName { get; set; }
    }
}