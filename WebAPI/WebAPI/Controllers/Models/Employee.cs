namespace WebAPI.Controllers.Models
{
    public record Employee
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;

        /// <summary>ISO date (yyyy-MM-dd). Kept as a string end to end: the Angular
        /// client binds it to &lt;input type="date"&gt;, which emits that format.</summary>
        public string DateOfJoining { get; set; } = string.Empty;

        public string PhotoFileName { get; set; } = "anonymous.png";
    }
}
