namespace localscrape.Models
{
    public class MangaSeries
    {
        public string? MangaTitle { get; set; }
        public string? MangaSeriesUri { get; set; }
        public List<MangaChapter>? MangaChapters { get; set; }
    }
}