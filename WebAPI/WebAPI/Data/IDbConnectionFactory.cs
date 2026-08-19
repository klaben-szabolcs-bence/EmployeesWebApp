using System.Data.Common;

namespace WebAPI.Data
{
    public interface IDbConnectionFactory
    {
        DbProvider Provider { get; }

        /// <summary>Creates a closed connection to the configured database.</summary>
        DbConnection CreateConnection();

        /// <summary>
        /// SQL expression yielding <paramref name="column"/> as an ISO date string
        /// (yyyy-MM-dd). SQL Server needs an explicit CONVERT; SQLite's date-affinity
        /// columns already store that format, so the bare column name is correct.
        /// </summary>
        string DateAsIso(string column);
    }
}
