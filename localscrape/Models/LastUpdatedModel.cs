namespace localscrape.Models
{
    public class LastUpdatedModel
    {
        public int Id { get; set; }
        public string? Site { get; set; }
        public string? Url { get; set; }
        public DateTime UpdateDate { get; set; }
        public required string? User { get; set; }
    }
}
