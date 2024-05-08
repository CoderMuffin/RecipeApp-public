using Microsoft.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace RecipeBackend {
    public class Auth {
        public class AuthResult {
            public bool successful = false;
            public string? reason = null;
        }
        public class LoginResult: AuthResult {
            public string? token = null;
        }
        public class RegisterResult : AuthResult { }
        public class ConfirmRegisterResult : AuthResult { }
        public class ResetPasswordResult: AuthResult { }
        public class ConfirmResetPasswordResult : AuthResult { }
        public struct TokenInfo {
            public bool valid = false;
            public string? reason = null;
            public int? userID = null;

            public TokenInfo() { }
        }

        public struct UserInfo {
            public int id;
            public string email;
            public byte[] passwordHash;
        }

        private string connectionString;
        public Auth(string connectionString) {
            this.connectionString = connectionString;
        }
    
        private const string salt = "redacted";
        public LoginResult Login(string email, string password) {
            try {
                using var connection = new SqlConnection(connectionString);
                connection.Open();
                var user = GetUser(connection, email);

                if (user == null) {
                    return new LoginResult {
                        successful = false,
                        reason = "No such user"
                    };
                }

                if (!CryptographicOperations.FixedTimeEquals(user.Value.passwordHash, HashText(password))) {
                    return new LoginResult {
                        successful = false,
                        reason = "Either the user or the password are incorrect"
                    };
                }
                string token = AcquireSession(connection, user.Value.id);
                return new LoginResult {
                    successful = true,
                    token = token
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new LoginResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public RegisterResult Register(string email, string password) {
            try {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                if (GetUser(connection, email) != null) {
                    return new RegisterResult {
                        successful = false,
                        reason = "Email already registered"
                    };
                }

                string code = GenerateRegistrationCode(connection, email, password);
                SendEmail(email, "Registration code", "Your code is below:\n" + code);

                return new RegisterResult {
                    successful = true
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new RegisterResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public ConfirmRegisterResult ConfirmRegister(string email, string code) {
            try {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                if (GetUser(connection, email) != null) {
                    return new ConfirmRegisterResult {
                        successful = false,
                        reason = "Email already registered"
                    };
                }

                //codes from 1 year+ ago aren't counted
                using SqlCommand command = new SqlCommand("SELECT codes.id AS code_id, codes.value AS code_value, codes.expiry AS code_expiry, registrations.id AS registrations_id, registrations.* " +
                    "FROM [registrations] JOIN [codes] ON codes.id=registrations.code " +
                    "WHERE registrations.email=@email AND codes.expiry > DATEADD(YEAR, -1, GETDATE())", connection);
                command.Parameters.AddWithValue("email", email);

                using SqlDataReader reader = command.ExecuteReader();

                int results = 0;

                while (reader.Read()) {
                    int registrationID = (int)reader["registrations_id"];
                    string storedPassword = (string)reader["password"];
                    int codeID = (int)reader["code_id"];
                    string storedCode = (string)reader["code_value"];
                    DateTime storedExpiry = (DateTime)reader["code_expiry"];

                    if (CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(storedCode), HashText(code))) { //if valid
                        results++;
                        if (storedExpiry > DateTime.Now) { //if unexpired
                            reader.Close(); //allow write operations

                            using SqlCommand insertCommand = new SqlCommand("INSERT INTO [user] ([email], [password]) VALUES (@email, @password)", connection);
                            insertCommand.Parameters.AddWithValue("email", email);
                            insertCommand.Parameters.AddWithValue("password", storedPassword);

                            insertCommand.ExecuteNonQuery();

                            AuthCode.Invalidate(connection, codeID);

                            return new ConfirmRegisterResult {
                                successful = true
                            };
                        }
                    }
                }

                return new ConfirmRegisterResult {
                    successful = false,
                    reason = results == 0 ? "No such code" : "Code expired"
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new ConfirmRegisterResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public ResetPasswordResult ResetPassword(string email) {
            try {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var user = GetUser(connection, email);
                if (user == null) {
                    return new ResetPasswordResult {
                        successful = false,
                        reason = "No user registered with that email"
                    };
                }
                string code = GeneratePasswordResetCode(connection, user.Value.id);
                SendEmail(email, "Password reset code", "Your code is below:\n" + code);
                return new ResetPasswordResult {
                    successful = true
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new ResetPasswordResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public ConfirmResetPasswordResult ConfirmResetPassword(string email, string password, string code) {
            try {
                using var connection = new SqlConnection(connectionString);
                connection.Open();

                var user = GetUser(connection, email);
                if (user == null) {
                    return new ConfirmResetPasswordResult {
                        successful = false,
                        reason = "No such user"
                    };
                }

                //codes from 1 year+ ago aren't counted
                using SqlCommand command = new SqlCommand("SELECT codes.id AS code_id, codes.value AS code_value, codes.expiry AS code_expiry, passwordresets.id AS passwordresets_id, passwordresets.* FROM [passwordresets] JOIN [codes] ON codes.id=passwordresets.code WHERE passwordresets.[user]=@id AND codes.expiry > DATEADD(YEAR, -1, GETDATE())", connection);
                command.Parameters.AddWithValue("id", user.Value.id);

                using SqlDataReader reader = command.ExecuteReader();
                int results = 0;
                while (reader.Read()) {
                    int codeID = (int)reader["code_id"];
                    string storedCode = (string)reader["code_value"];
                    DateTime storedExpiry = (DateTime)reader["code_expiry"];

                    if (CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(storedCode), HashText(code))) { //if valid
                        results++;
                        if (storedExpiry > DateTime.Now) { //if unexpired
                            reader.Close();

                            using SqlCommand passwordResetCommand = new SqlCommand("UPDATE [user] SET password=@password WHERE id=@id", connection);
                            passwordResetCommand.Parameters.AddWithValue("id", user.Value.id);
                            passwordResetCommand.Parameters.AddWithValue("password", Convert.ToBase64String(HashText(password)));
                            passwordResetCommand.ExecuteNonQuery();

                            AuthCode.Invalidate(connection, codeID);

                            return new ConfirmResetPasswordResult {
                                successful = true
                            };
                        }
                    }
                }

                return new ConfirmResetPasswordResult {
                    successful = false,
                    reason = results == 0 ? "No such code" : "Code expired"
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new ConfirmResetPasswordResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public static UserInfo? GetUser(SqlConnection connection, string email) {
            using SqlCommand command = new SqlCommand("SELECT * FROM [user] WHERE email=@email", connection);
            command.Parameters.AddWithValue("email", email);

            using SqlDataReader reader = command.ExecuteReader();
            
            if (reader.Read()) {
                return new UserInfo {
                    id = (int)reader["id"],
                    email = (string)reader["email"],
                    passwordHash = Convert.FromBase64String((string)reader["password"])
                };
            }

            return null;
        }

        public static void SendEmail(string email, string subject, string body) {
            string senderEmail = "codermuffin@gmail.com";
            string senderPassword = "bWNsVhERkjxw65Bn";

            // Configure the SMTP client
            SmtpClient smtpClient = new SmtpClient("smtp-relay.sendinblue.com", 587);
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);
            smtpClient.EnableSsl = true;

            // Create the mail message
            MailMessage mailMessage = new MailMessage(senderEmail, email, subject, body);

            // Send the email
            smtpClient.Send(mailMessage);
        }

        public static string GenerateRegistrationCode(SqlConnection connection, string email, string password) {
            using SqlCommand command = new SqlCommand("INSERT INTO [registrations] ([email], [password], [code]) VALUES (@email, @password, @code)", connection);

            AuthCode code = AuthCode.Generate(connection);
            command.Parameters.AddWithValue("code", code.id);
            command.Parameters.AddWithValue("email", email);
            command.Parameters.AddWithValue("password", Convert.ToBase64String(HashText(password)));
            command.ExecuteNonQuery();
            return code.code;
        }

        public static string GeneratePasswordResetCode(SqlConnection connection, int userID) {
            using SqlCommand command = new SqlCommand("INSERT INTO [passwordresets] ([user], [code]) VALUES (@user, @code)", connection);

            AuthCode code = AuthCode.Generate(connection);
            command.Parameters.AddWithValue("code", code.id);
            command.Parameters.AddWithValue("user", userID);
            command.ExecuteNonQuery();
            return code.code;
        }

        public static string AcquireSession(SqlConnection connection, int userID) {
            using SqlCommand command = new SqlCommand("INSERT INTO [sessions] ([user], [token], [start], [end]) VALUES (@user, @token, @start, @end)", connection);
            
            string token = GenerateSessionToken();
            command.Parameters.AddWithValue("token", token);
            command.Parameters.AddWithValue("user", userID);
            command.Parameters.AddWithValue("start", DateTime.Now);
            command.Parameters.AddWithValue("end", DateTime.Now + TimeSpan.FromHours(1)); //sessions last 1hr
            command.ExecuteNonQuery();
            return token;
        }

        public static TokenInfo GetTokenInfo(SqlConnection connection, string token) {
            using SqlCommand command = new SqlCommand("SELECT * FROM [sessions] WHERE token=@token", connection);
            command.Parameters.AddWithValue("token", token);

            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read()) {
                DateTime expiry = (DateTime)reader["end"];
                int userID = (int)reader["user"];

                if (expiry > DateTime.Now) {
                    return new TokenInfo {
                        valid = true,
                        userID = userID
                    };
                } else {
                    return new TokenInfo {
                        valid = false,
                        reason = "Token expired"
                    };
                }
            }

            return new TokenInfo {
                valid = false,
                reason = "Bad token"
            };
        }

        public static string RandomString(int length) {
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[length/4*3]; //base64 has 4/3 increase in size
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }

        public static string GenerateSessionToken() {
            long now = DateTime.Now.Ticks;
            return now + "@" + RandomString(32);
        }

        public static byte[] HashText(string password) {
            using SHA256 hasher = SHA256.Create();
            return hasher.ComputeHash(Encoding.UTF8.GetBytes(salt + password + salt));
        }
    }
}
