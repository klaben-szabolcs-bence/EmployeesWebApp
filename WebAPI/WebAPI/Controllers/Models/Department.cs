namespace WebAPI.Controllers.Models
{
    public record Department
    {
        /// <summary>Note the casing: the Angular client expects DepartmentID, while
        /// the database column is DepartmentId. The GET query aliases it.</summary>
        public int DepartmentID { get; set; }

        public string DepartmentName { get; set; } = string.Empty;
    }
}
