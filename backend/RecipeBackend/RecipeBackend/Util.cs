using Microsoft.Data.SqlClient;

namespace RecipeBackend
{
    public static class Util
    {
        public class OwnershipInfo
        {
            public bool owns;
            public string? reason;
        }
        public static System.Data.DataTable CreateDataTable<T>(IEnumerable<T> values)
        {
            System.Data.DataTable dataTable = new System.Data.DataTable();
            dataTable.Columns.Add("Value", typeof(T));

            foreach (T value in values)
            {
                dataTable.Rows.Add(value);
            }

            return dataTable;
        }

        public static void AddArrayParameters<T>(this SqlCommand cmd, string name, IEnumerable<T> values)
        {
            name = name.StartsWith("@") ? name : "@" + name;
            var names = string.Join(", ", values.Select((value, i) => {
                var paramName = name + i;
                cmd.Parameters.AddWithValue(paramName, value);
                return paramName;
            }));
            cmd.CommandText = cmd.CommandText.Replace(name, names);
        }
    }
}
