using System.Data.Common;
using System.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace WebAPI.Data
{
    public sealed class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbProvider Provider { get; }

        public DbConnectionFactory(IConfiguration config)
        {
            var configured = config["Database:Provider"] ?? nameof(DbProvider.SqlServer);
            if (!Enum.TryParse<DbProvider>(configured, ignoreCase: true, out var provider))
            {
                throw new InvalidOperationException(
                    $"Database:Provider is '{configured}'; expected 'SqlServer' or 'Sqlite'.");
            }

            Provider = provider;
            _connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection is not configured.");
        }

        public DbConnection CreateConnection() => Provider switch
        {
            DbProvider.Sqlite => new SqliteConnection(_connectionString),
            _ => new SqlConnection(_connectionString)
        };

        public string DateAsIso(string column) => Provider switch
        {
            // SQLite stores date-affinity columns as yyyy-MM-dd text already.
            DbProvider.Sqlite => column,
            _ => $"convert(varchar(10),{column},120)"
        };
    }
}
