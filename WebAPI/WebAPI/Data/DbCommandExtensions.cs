using System.Data.Common;

namespace WebAPI.Data
{
    public static class DbCommandExtensions
    {
        /// <summary>
        /// Provider-neutral replacement for SqlCommand.Parameters.AddWithValue.
        /// AddWithValue exists on the concrete parameter collections but not on
        /// DbParameterCollection, so it cannot be used once the code is written
        /// against the ADO.NET base types.
        /// </summary>
        public static DbCommand AddParam(this DbCommand command, string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
            return command;
        }
    }
}
