namespace WebAPI.Data
{
    public interface IStoragePaths
    {
        /// <summary>Absolute path to the directory holding employee photos.</summary>
        string PhotosPath { get; }
    }

    /// <summary>
    /// Resolves the photo directory from configuration.
    /// </summary>
    /// <remarks>
    /// Previously both Program.cs and EmployeeController derived this from
    /// Directory.GetCurrentDirectory(), which is the process working directory and
    /// is not guaranteed to be the content root in a container. One configured
    /// value, two consumers.
    /// </remarks>
    public sealed class StoragePaths : IStoragePaths
    {
        public string PhotosPath { get; }

        public StoragePaths(IConfiguration config, IWebHostEnvironment env)
        {
            var configured = config["Storage:PhotosPath"];
            PhotosPath = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, "Photos")
                : Path.GetFullPath(configured);
        }
    }
}
