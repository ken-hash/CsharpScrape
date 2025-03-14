using dotenv.net;
namespace localscrape.Helpers
{
    public interface IDotEnvHelper
    {
        string GetEnvValue(string Key);
    }

    public class DotEnvHelper : IDotEnvHelper
    {
        public DotEnvHelper(DotEnvOptions options)
        {
            DotEnv.Load(options);
        }

        public DotEnvHelper()
        {
            DotEnv.Load();
        }

        public string GetEnvValue(string Key)
        {
            return Environment.GetEnvironmentVariable(Key) ?? string.Empty;
        }
    }
}