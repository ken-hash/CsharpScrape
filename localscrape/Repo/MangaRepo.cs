using Dapper;
using localscrape.Models;
using MySqlConnector;
namespace localscrape.Repo
{

    public class MangaRepo
    {
        private readonly string? _tableName;
        private readonly string? _connectionString;
        public MangaRepo(string? TableName)
        {
            var dotEnv = new DotEnvHelper();
            _tableName = TableName;
            _connectionString = dotEnv.GetEnvValue("connectionString");
        }

        public bool DoesExist(string mangaTitle)
        {
            using (var sql = new MySqlConnection(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaTitle
                };
                string query = $"SELECT COUNT(1) FROM {_tableName} WHERE Title = @Title";
                var results = sql.Query<int>(query, parameters);
                return results?.Count() > 0;
            }
        }

        public List<MangaObject> GetMangas()
        {
            using (var sql = new MySqlConnection(_connectionString))
            {
                string query = $"SELECT * FROM {_tableName}";
                var results = sql.Query<MangaObject>(query);
                return results.ToList();
            }
        }

        public MangaObject? GetMangaTitle(string mangaTitle)
        {
            using (var sql = new MySqlConnection(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaTitle
                };
                string query = $"SELECT * FROM {_tableName} WHERE Title = @Title";
                var results = sql.Query<MangaObject>(query, parameters);
                return results.FirstOrDefault();
            }
        }

        public void UpdateManga(MangaObject mangaObject)
        {
            using (var sql = new MySqlConnection(_connectionString))
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
            }
        }

        public void InsertManga(MangaObject mangaObject)
        {
            using (var sql = new MySqlConnection(_connectionString))
            {
                var parameters = new
                {
                    Title = mangaObject.Title,
                    ExtraInformation = mangaObject.ExtraInformation,
                    LastUpdated = mangaObject.LastUpdated,
                    LatestChapter = mangaObject.LatestChapter
                };
                string query = $"INSERT INTO {_tableName}(Title) VALUES (@Title)";
                sql.Execute(query, parameters);
            }
        }

        public void InsertQueue(DownloadObject downloadObject)
        {
            using (var sql = new MySqlConnection(_connectionString))
            {
                var parameters = new
                {
                    Title = downloadObject.Title,
                    ChapterNum = downloadObject.ChapterNum,
                    FileId = downloadObject.FileId,
                    Url = downloadObject.Url
                };
                string query = $"INSERT INTO DownloadQueue(Title, ChapterNum, FileId, Url) VALUES (@Title, @ChapterNum, @FileId, @Url)";
                sql.Execute(query, parameters);
            }
        }
    }
}