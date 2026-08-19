using System.Data;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Controllers.Models;
using WebAPI.Data;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DepartmentController : ControllerBase
    {
        private readonly IDbConnectionFactory _db;

        public DepartmentController(IDbConnectionFactory db)
        {
            _db = db;
        }

        /// <summary>
        /// List all departments
        /// </summary>
        /// <returns>API result</returns>
        [HttpGet]
        public IActionResult Get()
        {
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            // Aliased deliberately: the column is DepartmentId but the Angular client
            // reads DepartmentID. Because responses are DataTable serialised straight
            // to JSON, the column name IS the wire contract -- the original SELECT *
            // returned DepartmentId, so the client's id was silently undefined and
            // delete sent /api/Department/undefined.
            command.CommandText = "SELECT DepartmentId AS DepartmentID, DepartmentName FROM Department";

            connection.Open();
            using var reader = command.ExecuteReader();
            var table = new DataTable();
            table.Load(reader);

            return new JsonResult(table) { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Create a department
        /// </summary>
        /// <param name="department">Department to create</param>
        /// <returns>API result</returns>
        [HttpPost]
        public IActionResult Post(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
            {
                return BadRequest(new { Message = "DepartmentName is required" });
            }

            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Department (DepartmentName) VALUES (@DepartmentName)";
            command.AddParam("@DepartmentName", department.DepartmentName);

            connection.Open();
            command.ExecuteNonQuery();

            return new JsonResult(new { Message = "Department added successfully" })
            { StatusCode = StatusCodes.Status201Created };
        }

        /// <summary>
        /// Update a department
        /// </summary>
        /// <param name="department">Department to update</param>
        /// <returns>API result</returns>
        [HttpPut]
        public IActionResult Put(Department department)
        {
            if (string.IsNullOrWhiteSpace(department.DepartmentName))
            {
                return BadRequest(new { Message = "DepartmentName is required" });
            }

            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText =
                "UPDATE Department SET DepartmentName = @DepartmentName WHERE DepartmentId = @DepartmentId";
            command.AddParam("@DepartmentName", department.DepartmentName);
            command.AddParam("@DepartmentId", department.DepartmentID);

            connection.Open();
            // ExecuteNonQuery returns the rows affected; using it is what lets a
            // missing id report 404 rather than a misleading success.
            var affected = command.ExecuteNonQuery();

            if (affected == 0)
            {
                return NotFound(new { Message = $"No department with id {department.DepartmentID}" });
            }

            return new JsonResult(new { Message = "Department updated successfully" })
            { StatusCode = StatusCodes.Status200OK };
        }

        /// <summary>
        /// Delete department
        /// </summary>
        /// <param name="id">Department to delete</param>
        /// <returns>API result</returns>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            using var connection = _db.CreateConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Department WHERE DepartmentId = @DepartmentId";
            command.AddParam("@DepartmentId", id);

            connection.Open();
            var affected = command.ExecuteNonQuery();

            if (affected == 0)
            {
                return NotFound(new { Message = $"No department with id {id}" });
            }

            return new JsonResult(new { Message = "Department deleted successfully" })
            { StatusCode = StatusCodes.Status200OK };
        }
    }
}
