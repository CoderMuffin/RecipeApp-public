using Microsoft.Data.SqlClient;

namespace RecipeBackend {
    public struct AuthCode {
        public int id;
        public string code;

        public static AuthCode Generate(SqlConnection connection) {
            using SqlCommand command = new SqlCommand("INSERT INTO [codes] ([value], [expiry]) OUTPUT INSERTED.[id] VALUES (@value, @expiry)", connection);

            string code = Auth.RandomString(8);
            command.Parameters.AddWithValue("value", Convert.ToBase64String(Auth.HashText(code)));
            command.Parameters.AddWithValue("expiry", DateTime.Now + TimeSpan.FromMinutes(10)); //codes last 10 minutes

            int id = (int)command.ExecuteScalar();
            return new AuthCode {
                code = code,
                id = id
            };
        }
        public static void Invalidate(SqlConnection connection, int codeID) {
            using SqlCommand command = new SqlCommand("UPDATE [codes] SET expiry='19700101' WHERE id=@id", connection); //YYYYMMDD
            command.Parameters.AddWithValue("id", codeID);
            command.ExecuteNonQuery();
        }
    }
}
