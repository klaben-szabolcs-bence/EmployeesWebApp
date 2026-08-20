using System.Data.Common;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Controllers.Models;
using WebAPI.Data;
using WebAPI.Diagnostics;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        // Full statements rather than an interpolated table name. A table name
        // cannot be a parameter, and this project's one real strength is that no
        // SQL anywhere is built by concatenation. Keep it that way.
        private const string CountDepartments = "SELECT COUNT(*) FROM Department";
        private const string CountEmployees = "SELECT COUNT(*) FROM Employee";

        // Same three words ASP.NET's own health checks use, and the same rule:
        // only Unhealthy is a 503. Degraded still serves, so it must not page.
        private const string StatusOk = "ok";
        private const string StatusDegraded = "degraded";
        private const string StatusUnhealthy = "unhealthy";

        private readonly IDbConnectionFactory _db;
        private readonly IStoragePaths _paths;
        private readonly IAppInfo _app;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            IDbConnectionFactory db,
            IStoragePaths paths,
            IAppInfo app,
            ILogger<HealthController> logger)
        {
            _db = db;
            _paths = paths;
            _app = app;
            _logger = logger;
        }

        /// <summary>
        /// Liveness, plus the few facts worth putting on a dashboard.
        /// </summary>
        /// <remarks>
        /// HttpHead as well as HttpGet on purpose. Uptime monitors and badge
        /// services probe with HEAD, and an action marked only HttpGet answers 405,
        /// which they report as an outage. That is not hypothetical: shields.io
        /// called this API down while it was serving 200s.
        /// </remarks>
        [HttpGet]
        [HttpHead]
        public IActionResult Get()
        {
            var now = DateTimeOffset.UtcNow;
            var database = CheckDatabase();
            var photos = CheckPhotos();
            var status = DecideStatus(database, photos);

            var response = new HealthResponse
            {
                Status = status,
                CheckedAtUtc = now,
                StartedAtUtc = _app.StartedAtUtc,
                UptimeSeconds = (long)(now - _app.StartedAtUtc).TotalSeconds,
                Version = _app.Version,
                Commit = _app.Commit,
                Environment = _app.EnvironmentName,
                Database = database,
                Photos = photos
            };

            // Only Unhealthy answers 503. A monitor reads the status code and
            // nothing else, so anything still able to serve has to stay 200 or it
            // reports an outage that is not happening.
            return new JsonResult(response)
            {
                StatusCode = status == StatusUnhealthy
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status200OK
            };
        }

        /// <summary>
        /// Turns the individual checks into the one word the dashboard shows and
        /// the HTTP status code that monitors read.
        /// </summary>
        private static string DecideStatus(DatabaseHealth database, PhotosHealth photos)
        {
            // No database means nothing to serve. This is the only case worth
            // waking someone for.
            if (!database.Ok)
            {
                return StatusUnhealthy;
            }

            // Photos and the database live in one directory so that on an ephemeral
            // filesystem they reset together. An unreadable directory, or no photos
            // at all while employee rows exist, means that stopped being true and
            // rows now point at images that are not there. The API still answers,
            // so this is visible on the dashboard rather than a 503.
            if (!photos.Ok || (photos.Count == 0 && database.Employees > 0))
            {
                return StatusDegraded;
            }

            return StatusOk;
        }

        private DatabaseHealth CheckDatabase()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var connection = _db.CreateConnection();
                connection.Open();

                return new DatabaseHealth
                {
                    Provider = _db.Provider.ToString(),
                    Ok = true,
                    Departments = CountRows(connection, CountDepartments),
                    Employees = CountRows(connection, CountEmployees),
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                // The only try/catch in a controller here, and deliberately so. A
                // health endpoint has to answer with a status rather than a 500
                // stack trace. The exception text carries the connection string, so
                // it is logged and never returned.
                _logger.LogError(ex, "Health check could not reach the database.");

                return new DatabaseHealth
                {
                    Provider = _db.Provider.ToString(),
                    Ok = false,
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// COUNT(*) is in the subset SQL Server and SQLite share, so the check runs
        /// on either provider.
        /// </summary>
        private static long CountRows(DbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
        }

        private PhotosHealth CheckPhotos()
        {
            try
            {
                if (!Directory.Exists(_paths.PhotosPath))
                {
                    return new PhotosHealth { Ok = false, Count = null };
                }

                return new PhotosHealth
                {
                    Ok = true,
                    Count = Directory.EnumerateFiles(_paths.PhotosPath).Count()
                };
            }
            catch (Exception ex)
            {
                // The path is in the message, so log it rather than return it.
                _logger.LogError(ex, "Health check could not read the photos directory.");
                return new PhotosHealth { Ok = false, Count = null };
            }
        }
    }
}
