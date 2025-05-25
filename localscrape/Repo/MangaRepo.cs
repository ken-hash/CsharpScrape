using Dapper;
using dotenv.net;
using localscrape.Helpers;
using localscrape.Models;
using Microsoft.Extensions.Logging;
using MySqlConnector;
namespace localscrape.Repo
{
    public interface IMangaRepo
    {
        string GetTableName();
        bool DoesExist(string mangaTitle);
        List<MangaObject> GetMangas();
        MangaObject? GetMangaTitle(string mangaTitle);
        void UpdateManga(MangaObject mangaObject);
        void InsertManga(MangaObject mangaObject);
    }

    public class MangaRepo : IMangaRepo
    {
        private readonly string? _tableName;
        private readonly string? _connectionString;
        private DotEnvHelper? _envHelper;
        private readonly ILogger _logger;

        public MangaRepo(string? TableName, ILogger logger)
        {
            _logger = logger;
            if (_envHelper is null)
            {
                _envHelper = new DotEnvHelper(logger);
            }
            _tableName = TableName;
            _connectionString = _envHelper.GetEnvValue("connectionString");
        }

        public MangaRepo(string? tableName, DotEnvOptions options, ILogger logger) : this(tableName, logger)
        {
            _logger = logger;
            _envHelper = new DotEnvHelper(options, logger);
        }

        public string GetTableName()
        {
            return _tableName ?? string.Empty;
        }

        public bool DoesExist(string mangaTitle)
        {
            using (MySqlConnection sql = new(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaTitle
                };
                string query = $"SELECT COUNT(1) FROM {_tableName} WHERE Title = @Title";
                IEnumerable<int> results = sql.Query<int>(query, parameters);
                var exist = results?.First() > 0;
                _logger.LogInformation($"Title {mangaTitle} exist in {_tableName} : {exist}");
                return exist;
            }
        }

        public List<MangaObject> GetMangas()
        {
            using (MySqlConnection sql = new(_connectionString))
            {
                string query = $"SELECT * FROM {_tableName}";
                IEnumerable<MangaObject> results = sql.Query<MangaObject>(query);
                _logger.LogInformation($"Found {results.Count()} titles in {_tableName}");
                return results.ToList();
            }
        }

        public MangaObject? GetMangaTitle(string mangaTitle)
        {
            _logger.LogInformation($"Getting DB info on {mangaTitle}");
            using (MySqlConnection sql = new(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaTitle
                };
                string query = $"SELECT * FROM {_tableName} WHERE Title = @Title";
                IEnumerable<MangaObject> results = sql.Query<MangaObject>(query, parameters);
                return results.FirstOrDefault();
            }
        }

        public void UpdateManga(MangaObject mangaObject)
        {
            using (MySqlConnection sql = new(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaObject.Title,
                    ExtraInformation = mangaObject.ExtraInformation,
                    LastUpdated = mangaObject.LastUpdated,
                    LatestChapter = mangaObject.LatestChapter
                };
                string query = $"UPDATE {_tableName} SET ExtraInformation = @ExtraInformation, LastUpdated = @LastUpdated, LatestChapter = @LatestChapter WHERE Title = @Title";
                sql.Execute(query, parameters);
                _logger.LogInformation($"Updated {mangaObject.Title} in {_tableName}");
            }
        }

        public void InsertManga(MangaObject mangaObject)
        {
            using (MySqlConnection sql = new(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaObject.Title,
                    ExtraInformation = mangaObject.ExtraInformation,
                    LastUpdated = mangaObject.LastUpdated,
                    LatestChapter = mangaObject.LatestChapter
                };
                string query = $"INSERT INTO {_tableName}(Title) VALUES (@Title)";
                _logger.LogInformation($"Inserted new title {mangaObject.Title} to {_tableName}");
                sql.Execute(query, parameters);
            }
        }
    }
}