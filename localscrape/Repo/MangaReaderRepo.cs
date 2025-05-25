using localscrape.Manga;
using localscrape.Models;
using Microsoft.Extensions.Logging;

namespace localscrape.Repo
{
    public interface IMangaReaderRepo
    {
        public Task<string?> UpdateLatestUpdate(string mangaSite, string mangaSiteUrl);
    }

    public class MangaReaderRepo : IMangaReaderRepo
    {
        private readonly IRestService _restService;
        private readonly ILogger _logger;
        public MangaReaderRepo(ILogger logger)
        {
            _logger = logger;
            _restService = new RestService(logger);
        }

        public MangaReaderRepo(IRestService restService, ILogger logger)
        {
            _logger = logger;
            _restService = restService;
        }

        public async Task<string?> UpdateLatestUpdate(string mangaSite, string mangaSiteUrl)
        {
            _logger.LogInformation($"Updating {mangaSite} {Environment.MachineName}");
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
