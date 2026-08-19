using System.Data;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Controllers.Models;
using WebAPI.Data;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeeController : ControllerBase
    {
        /// <summary>Extensions accepted for employee photos.</summary>
        private static readonly string[] AllowedPhotoExtensions =
            { ".png", ".jpg", ".jpeg", ".webp" };

        private const long MaxPhotoBytes = 2 * 1024 * 1024;

        private readonly IDbConnectionFactory _db;
        private readonly IStoragePaths _paths;

        public EmployeeController(IDbConnectionFactory db, IStoragePaths paths)
        {
            _db = db;
            _paths = paths;
        }

        /// <summary>
        /// List all employees
        /// </summary>
        /// <returns>API result</returns>
        [HttpGet]
        public IActionResult Get()
        {
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT EmployeeId, EmployeeName, Department, " +
                $"{_db.DateAsIso("DateOfJoining")} AS DateOfJoining, " +
                "PhotoFileName FROM Employee";

            connection.Open();
            using var reader = command.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);

            return new JsonResult(table) { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Create an employee
        /// </summary>
        /// <param name="employee">Employee to create</param>
        /// <returns>API result</returns>
        [HttpPost]
        public IActionResult Post(Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.EmployeeName))
            {
                return BadRequest(new { Message = "EmployeeName is required" });
            }

            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Employee (EmployeeName, Department, DateOfJoining, PhotoFileName) " +
                "VALUES (@Name, @Department, @DateOfJoining, @PhotoFileName)";
            command.AddParam("@Name", employee.EmployeeName);
            command.AddParam("@Department", employee.Department);
            command.AddParam("@DateOfJoining", employee.DateOfJoining);
            command.AddParam("@PhotoFileName", employee.PhotoFileName);

            connection.Open();
            command.ExecuteNonQuery();

            return new JsonResult(new { Message = "Employee added successfully" })
            { StatusCode = StatusCodes.Status201Created };
        }

        /// <summary>
        /// Update an employee
        /// </summary>
        /// <param name="employee">Employee to update</param>
        /// <returns>API result</returns>
        [HttpPut]
        public IActionResult Put(Employee employee)
        {
            if (string.IsNullOrWhiteSpace(employee.EmployeeName))
            {
                return BadRequest(new { Message = "EmployeeName is required" });
            }

            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE Employee SET EmployeeName = @Name, Department = @Department, " +
                "DateOfJoining = @DateOfJoining, PhotoFileName = @PhotoFileName " +
                "WHERE EmployeeId = @Id";
            command.AddParam("@Id", employee.EmployeeId);
            command.AddParam("@Name", employee.EmployeeName);
            command.AddParam("@Department", employee.Department);
            command.AddParam("@DateOfJoining", employee.DateOfJoining);
            command.AddParam("@PhotoFileName", employee.PhotoFileName);

            connection.Open();
            var affected = command.ExecuteNonQuery();

            if (affected == 0)
            {
                return NotFound(new { Message = $"No employee with id {employee.EmployeeId}" });
            }

            return new JsonResult(new { Message = "Employee updated successfully" })
            { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Delete employee
        /// </summary>
        /// <param name="id">Employee to delete</param>
        /// <returns>API result</returns>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Employee WHERE EmployeeId = @Id";
            command.AddParam("@Id", id);

            connection.Open();
            var affected = command.ExecuteNonQuery();

            if (affected == 0)
            {
                return NotFound(new { Message = $"No employee with id {id}" });
            }

            return new JsonResult(new { Message = "Employee deleted successfully" })
            { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Upload an employee photo
        /// </summary>
        /// <returns>API result, including the stored file name</returns>
        [HttpPost]
        [Route("SaveFile")]
        [RequestSizeLimit(MaxPhotoBytes)]
        public IActionResult SaveFile()
        {
            if (Request.Form.Files.Count == 0)
            {
                // Previously indexed Files[0] unguarded, so an empty POST was a 500.
                return BadRequest(new { Message = "No file was uploaded" });
            }

            var file = Request.Form.Files[0];
            if (file.Length == 0)
            {
                return BadRequest(new { Message = "Uploaded file is empty" });
            }
            if (file.Length > MaxPhotoBytes)
            {
                return BadRequest(new { Message = "File exceeds the 2 MB limit" });
            }

            var storedName = ResolveSafePhotoName(file.FileName);
            if (storedName == null)
            {
                return BadRequest(new
                {
                    Message = "Unsupported file name or type. Allowed: " +
                              string.Join(", ", AllowedPhotoExtensions)
                });
            }

            var photosRoot = _paths.PhotosPath;
            Directory.CreateDirectory(photosRoot);
            var destination = Path.Combine(photosRoot, storedName);

            // Defence in depth: storedName is generated, so this should always hold.
            // If it ever does not, that is a bug in ResolveSafePhotoName and writing
            // the file would be the wrong thing to do.
            if (!Path.GetFullPath(destination).StartsWith(
                    Path.GetFullPath(photosRoot) + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
            {
                return BadRequest(new { Message = "Rejected file name" });
            }

            using (var stream = new FileStream(destination, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            return new JsonResult(new { Message = "File uploaded successfully", FileName = storedName })
            { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Turns a client-supplied file name into a safe name to store on disk,
        /// or returns null to reject the upload.
        /// </summary>
        /// <remarks>
        /// The client controls this string completely. The previous implementation
        /// passed it to Path.Combine after stripping only quotes, which allowed
        /// writing outside the photos directory.
        /// </remarks>
        private static string? ResolveSafePhotoName(string clientFileName)
        {
            if (string.IsNullOrWhiteSpace(clientFileName))
            {
                return null;
            }

            // Take only the extension from the client, and only if it is on the
            // allow-list. A deny-list would have to enumerate every dangerous
            // extension; the allow-list enumerates the few safe ones, so anything
            // unforeseen is rejected by default.
            var extension = Path.GetExtension(clientFileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension))
            {
                return null;
            }

            // A human-readable slug, purely so the file is recognisable on disk.
            // Only ASCII letters and digits survive; every other character --
            // including path separators, dots and Unicode -- is collapsed into a
            // single dash, so the slug cannot smuggle a "../" or an absolute path
            // through. The original name is otherwise discarded, not trusted.
            var original = Path.GetFileNameWithoutExtension(clientFileName);
            var slug = new StringBuilder(Math.Min(original.Length, 40));
            foreach (var c in original)
            {
                if (char.IsAsciiLetterOrDigit(c))
                {
                    if (slug.Length >= 40) break;
                    slug.Append(char.ToLowerInvariant(c));
                }
                else if (slug.Length > 0 && slug[^1] != '-' && slug.Length < 40)
                {
                    slug.Append('-');
                }
            }
            var slugText = slug.ToString().Trim('-');

            // The GUID is what actually makes the name safe and unique; the slug is
            // decoration in front of it. This is also why the StartsWith check at the
            // call site is defence in depth, not the primary guard -- the name we
            // return here contains nothing the client controls except a vetted
            // extension.
            var unique = Guid.NewGuid().ToString("N");
            return slugText.Length > 0
                ? $"{slugText}-{unique}{extension}"
                : $"{unique}{extension}";
        }

        /// <summary>
        /// List all department names, for the employee form's dropdown
        /// </summary>
        /// <returns>API result</returns>
        [HttpGet]
        [Route("GetAllDepartmentNames")]
        public IActionResult GetAllDepartmentNames()
        {
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT DepartmentName FROM Department";

            connection.Open();
            using var reader = command.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);

            return new JsonResult(table) { StatusCode = StatusCodes.Status200OK };
        }
    }
}
