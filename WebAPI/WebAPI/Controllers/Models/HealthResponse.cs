namespace WebAPI.Controllers.Models
{
    /// <summary>
    /// Shape of GET /api/health.
    /// </summary>
    /// <remarks>
    /// A dashboard reads this by property path, so the names are part of the API
    /// contract. Renaming one breaks the panel silently, the same way the
    /// DataTable responses make column names a contract. The difference is that
    /// this one is written down.
    /// </remarks>
    public record HealthResponse
    {
        /// <summary>
        /// "ok", "degraded" or "unhealthy". Only "unhealthy" answers 503; a
        /// degraded API is still serving and must not be reported as an outage.
        /// </summary>
        public string Status { get; init; } = "ok";

        public DateTimeOffset CheckedAtUtc { get; init; }

        public DateTimeOffset StartedAtUtc { get; init; }

        /// <summary>Short uptime means the free tier just cold started.</summary>
        public long UptimeSeconds { get; init; }

        public string Version { get; init; } = string.Empty;

        public string? Commit { get; init; }

        public string Environment { get; init; } = string.Empty;

        public DatabaseHealth Database { get; init; } = new();

        public PhotosHealth Photos { get; init; } = new();
    }

    public record DatabaseHealth
    {
        /// <summary>"Sqlite" or "SqlServer".</summary>
        public string Provider { get; init; } = string.Empty;

        public bool Ok { get; init; }

        public long LatencyMs { get; init; }

        /// <summary>Null when the probe failed. Zero would read as "no rows".</summary>
        public long? Departments { get; init; }

        public long? Employees { get; init; }
    }

    /// <summary>
    /// Photos share a directory with the database so the two reset together on an
    /// ephemeral filesystem. If this count is zero while rows exist, that stopped
    /// being true.
    /// </summary>
    public record PhotosHealth
    {
        public bool Ok { get; init; }

        /// <summary>Null when the directory could not be read.</summary>
        public int? Count { get; init; }
    }
}
