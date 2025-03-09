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

        public RestService()
        {
            _httpClient = new HttpClient();
        }

        public RestService(string baseUrl)
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }

        public async Task<T?> GetAsync<T>(string url, object data)
        {
            var endpoint = new Uri(_baseUrl + url);
            var response = await _httpClient.GetAsync(endpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        }

        public async Task<T?> PostAsync<T>(string url, object data)
        {
            var endpoint = new Uri(_baseUrl + url);
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
