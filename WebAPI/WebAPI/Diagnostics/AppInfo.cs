using System.Reflection;

namespace WebAPI.Diagnostics
{
    public interface IAppInfo
    {
        /// <summary>When this process started, not when the type was constructed.</summary>
        DateTimeOffset StartedAtUtc { get; }

        string Version { get; }

        /// <summary>Deployed revision, or null when the host does not provide one.</summary>
        string? Commit { get; }

        string EnvironmentName { get; }
    }

    /// <summary>
    /// Fixed facts about the running process, read once at startup.
    /// </summary>
    /// <remarks>
    /// A singleton so that StartedAtUtc really is the process start. It also keeps
    /// IConfiguration out of HealthController: controllers taking IConfiguration
    /// directly is a defect this project already fixed once, see docs/CODE-REVIEW.md.
    /// </remarks>
    public sealed class AppInfo : IAppInfo
    {
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;

        public string Version { get; }

        public string? Commit { get; }

        public string EnvironmentName { get; }

        public AppInfo(IHostEnvironment env)
        {
            EnvironmentName = env.EnvironmentName;

            var assembly = typeof(AppInfo).Assembly;
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "unknown";

            // GIT_COMMIT first, RENDER_GIT_COMMIT only as a fallback. The API is
            // meant to move hosts with a DNS change, so the host's own variable
            // must not be the one thing that makes this work.
            Commit = FirstNonEmpty(
                Environment.GetEnvironmentVariable("GIT_COMMIT"),
                Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT"));
        }

        private static string? FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
