using Microsoft.Data.Sqlite;

namespace WebAPI.Data
{
    /// <summary>
    /// Creates and seeds the SQLite demo database on startup.
    /// </summary>
    /// <remarks>
    /// Free container tiers have an ephemeral filesystem: it is writable at
    /// runtime but resets on redeploy and on spin-down. This therefore runs on
    /// every boot and must be idempotent. It is a no-op when the provider is
    /// SQL Server, so local development is untouched.
    /// </remarks>
    public sealed class SqliteDatabaseInitializer
    {
        private readonly IDbConnectionFactory _factory;
        private readonly IStoragePaths _paths;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<SqliteDatabaseInitializer> _logger;

        public SqliteDatabaseInitializer(
            IDbConnectionFactory factory,
            IStoragePaths paths,
            IWebHostEnvironment env,
            ILogger<SqliteDatabaseInitializer> logger)
        {
            _factory = factory;
            _paths = paths;
            _env = env;
            _logger = logger;
        }

        public void Initialize()
        {
            Directory.CreateDirectory(_paths.PhotosPath);
            _logger.LogInformation("Photos directory: {PhotosPath}", _paths.PhotosPath);
            RestoreSeedPhotos();

            if (_factory.Provider != DbProvider.Sqlite)
            {
                _logger.LogInformation("Provider is {Provider}; skipping SQLite initialisation.",
                    _factory.Provider);
                return;
            }

            using var connection = (SqliteConnection)_factory.CreateConnection();
            var dbPath = connection.DataSource;
            if (!string.IsNullOrEmpty(dbPath))
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(dbPath));
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }

            connection.Open();
            _logger.LogInformation("SQLite database: {DbPath}", dbPath);

            Execute(connection, "PRAGMA journal_mode=WAL;");
            Execute(connection, ReadSeedFile("schema.sqlite.sql"));

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM Department";
            var existing = Convert.ToInt64(count.ExecuteScalar() ?? 0L);
            if (existing > 0)
            {
                _logger.LogInformation("Database already has {Count} departments; not seeding.", existing);
                return;
            }

            Execute(connection, ReadSeedFile("seed.sqlite.sql"));
            _logger.LogInformation("Seeded demo data.");
        }

        /// <summary>
        /// Photos live on the same ephemeral volume as the database, so both reset
        /// together and never disagree. Restoring the placeholder keeps seeded rows
        /// rendering an image rather than a broken one.
        /// </summary>
        private void RestoreSeedPhotos()
        {
            var seedPhotos = Path.Combine(_env.ContentRootPath, "Photos");
            if (!Directory.Exists(seedPhotos) ||
                Path.GetFullPath(seedPhotos) == Path.GetFullPath(_paths.PhotosPath))
            {
                return;
            }

            foreach (var source in Directory.EnumerateFiles(seedPhotos))
            {
                var target = Path.Combine(_paths.PhotosPath, Path.GetFileName(source));
                if (!File.Exists(target)) File.Copy(source, target);
            }
        }

        private string ReadSeedFile(string name)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Seed", name);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Seed script '{name}' was not found at '{path}'. It is copied to the output " +
                    "directory by WebAPI.csproj; check that the build published it.", path);
            }
            return File.ReadAllText(path);
        }

        private static void Execute(SqliteConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }
}
