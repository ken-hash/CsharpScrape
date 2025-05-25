using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace localscrape.Manga
{
    public interface IRestService
    {
        Task<T?> GetAsync<T>(string url, object data);
        Task<T?> PostAsync<T>(string url, object data);
        Task<T?> PatchAsync<T>(string url, object data);
    }

    public class RestService : IRestService
    {
        private readonly string? _baseUrl;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public RestService(ILogger logger)
        {
            _httpClient = new HttpClient();
            _logger = logger;
        }

        public RestService(string baseUrl, ILogger logger)
        {
            _baseUrl = baseUrl;
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }

        public async Task<T?> GetAsync<T>(string url, object data)
        {
            var endpoint = new Uri(_baseUrl + url);
            _logger.LogInformation($"Sending GET request to {url}");
            var response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        }

        public async Task<T?> PostAsync<T>(string url, object data)
        {
            var endpoint = new Uri(_baseUrl + url);
            _logger.LogInformation($"Sending POST request to {url} with {data}");
            var response = await _httpClient.PostAsJsonAsync(endpoint, data).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (typeof(T) == typeof(string))
            {
                var result = await response.Content.ReadAsStringAsync();
                return (T)(object)result;  
            }
            return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        }

        public async Task<T?> PatchAsync<T>(string url, object data)
        {
            _logger.LogInformation($"Sending PATCH request to {url} with {data}");
            var endpoint = new Uri(_baseUrl + url);
            var request = new HttpRequestMessage(HttpMethod.Patch, endpoint)
            {
                Content = JsonContent.Create(data)
            };
            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (typeof(T) == typeof(string))
            {
                var result = await response.Content.ReadAsStringAsync();
                return (T)(object)result;
            }
            return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        }
    }
}
