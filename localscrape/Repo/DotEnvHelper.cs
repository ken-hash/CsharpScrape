using dotenv.net;
namespace localscrape.Repo
{
    public class DotEnvHelper
    {
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