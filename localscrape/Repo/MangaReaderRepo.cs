using localscrape.Manga;
using localscrape.Models;

namespace localscrape.Repo
{
    public interface IMangaReaderRepo
    {
        public Task<string?> UpdateLatestUpdate(string mangaSite, string mangaSiteUrl);
    }

    public class MangaReaderRepo : IMangaReaderRepo
    {
        private readonly IRestService _restService;
        public MangaReaderRepo()
        {
            _restService = new RestService();
        }

        public MangaReaderRepo(IRestService restService)
        {
            _restService = restService;
        }

        public async Task<string?> UpdateLatestUpdate(string mangaSite, string mangaSiteUrl)
        {
            var update = new LastUpdatedModel 
            {
                User = Environment.MachineName,
                Site = mangaSite,
                Url = mangaSiteUrl,
                UpdateDate = DateTime.Now
            };
            return await _restService.PostAsync<string>("/api/Updates/update", update);
        }
    }
}
