namespace localscrape.Models
{
    public class MangaChapter
    {
        public required string MangaTitle { get; set; }
        public required string ChapterName { get; set; }
        public string? Uri { get; set; }
    }
}