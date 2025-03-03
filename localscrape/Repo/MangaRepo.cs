using Dapper;
using localscrape.Helpers;
using localscrape.Models;
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
        void InsertQueue(DownloadObject downloadObject);
    }

    public class MangaRepo : IMangaRepo
    {
        private readonly string? _tableName;
        private readonly string? _connectionString;
        public MangaRepo(string? TableName)
        {
            DotEnvHelper dotEnv = new();
            _tableName = TableName;
            _connectionString = dotEnv.GetEnvValue("connectionString");
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
                return results?.Count() > 0;
            }
        }

        public List<MangaObject> GetMangas()
        {
            using (MySqlConnection sql = new(_connectionString))
            {
                string query = $"SELECT * FROM {_tableName}";
                IEnumerable<MangaObject> results = sql.Query<MangaObject>(query);
                return results.ToList();
            }
        }

        public MangaObject? GetMangaTitle(string mangaTitle)
        {
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
                sql.Execute(query, parameters);
            }
        }

        public void InsertQueue(DownloadObject downloadObject)
        {
            using (MySqlConnection sql = new(_connectionString))
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