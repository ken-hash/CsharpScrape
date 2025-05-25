namespace localscrape.Models
{
    public class DownloadObject
    {
        public required string Title { get; set; }
        public required string ChapterNum { get; set; }
        public required List<MangaImages> MangaImages { get; set; }

        public override string ToString()
        {
            return $"DonwloadObject = {{Ttile:{Title} ChapterNum:{ChapterNum} ImagesCount: {MangaImages.Count}}}";
        }
    }
}