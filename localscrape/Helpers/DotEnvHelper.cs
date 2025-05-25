using dotenv.net;
using Microsoft.Extensions.Logging;

namespace localscrape.Helpers
{
    public interface IDotEnvHelper
    {
        string GetEnvValue(string key);
    }

    public class DotEnvHelper : IDotEnvHelper
    {
        private readonly ILogger _logger;

        public DotEnvHelper(DotEnvOptions options, ILogger logger)
        {
            _logger = logger;
            try
            {
                DotEnv.Load(options);
                _logger.LogInformation("DotEnv loaded with custom options.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DotEnv with custom options.");
            }
        }

        public DotEnvHelper(ILogger logger)
        {
            _logger = logger;
            try
            {
                DotEnv.Load();
                _logger.LogInformation("DotEnv loaded with default options.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DotEnv with default options.");
            }
        }

        public string GetEnvValue(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value))
            {
                _logger.LogWarning($"Environment variable '{key}' not found.");
                return string.Empty;
            }

            _logger.LogInformation($"Environment variable '{key}' retrieved.");
            return value;
        }
    }
}
