namespace localscrape.Models
{
    public class MangaSeries
    {
        public required string MangaTitle { get; set; }
        public required string MangaSeriesUri { get; set; }
        public List<MangaChapter>? MangaChapters { get; set; }
    }
}