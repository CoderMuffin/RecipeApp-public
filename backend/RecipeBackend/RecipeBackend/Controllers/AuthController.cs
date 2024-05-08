using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using NuGet.Protocol;
using System.Net.Http.Headers;

namespace RecipeBackend.Controllers {
    public struct LoginRequest {
        public string email { get; set; }
        public string password { get; set; }
    }

    public struct RegisterRequest {
        public string email { get; set; }
        public string password { get; set; }
    }

    public struct ConfirmRegisterRequest {
        public string email { get; set; }
        public string code { get; set; }
    }

    public struct ResetRequest {
        public string email { get; set; }
    }
    public struct ConfirmResetRequest {
        public string email { get; set; }
        public string password { get; set; }
        public string code { get; set; }
    }

    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase {
        private readonly Auth auth;
        private readonly string connectionString;

        public AuthController(IConfiguration config, ILogger<AuthController> logger) {
            connectionString = @"Data Source=.\SQLExpress;Initial Catalog=recipes;";
            auth = new Auth(connectionString);
        }

        [Route("login")]
        [HttpPost]
        public string Login() {
            if (DeserializeRequest(out LoginRequest request)) {
                Auth.LoginResult result = auth.Login(request.email, request.password);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("register/code")]
        [HttpPost]
        public string Register() {
            if (DeserializeRequest(out RegisterRequest request)) {
                Auth.RegisterResult result = auth.Register(request.email, request.password);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("register/confirm")]
        [HttpPost]
        public string ConfirmRegister() {
            if (DeserializeRequest(out ConfirmRegisterRequest request)) {
                Auth.ConfirmRegisterResult result = auth.ConfirmRegister(request.email, request.code);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("reset/code")]
        [HttpPost]
        public string Reset() {
            if (DeserializeRequest(out ResetRequest request)) {
                Auth.ResetPasswordResult result = auth.ResetPassword(request.email);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("reset/confirm")]
        [HttpPost]
        public string ConfirmReset() {
            if (DeserializeRequest(out ConfirmResetRequest request)) {
                Auth.ConfirmResetPasswordResult result = auth.ConfirmResetPassword(request.email, request.password, request.code);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("token")]
        [HttpGet]
        public string Token() {
            if (Request.Headers.TryGetValue("Authorization", out StringValues auth)) {
                var splitAuth = auth.ToString().Split("Bearer ");
                string token = splitAuth.Length switch {
                    1 => "",
                    2 => splitAuth[1],
                    _ => "Malformed request"
                };

                //this token can't be generated anyway so it's chill
                if (token == "Malformed request") {
                    return token;
                }

                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                return Auth.GetTokenInfo(connection, token).ToJson();
            } else {
                return "Missing authorization header";
            }
        }

        public bool DeserializeRequest<T>(out T result) where T : struct {
            try {
                result = System.Text.Json.JsonSerializer.Deserialize<T>(Request.Body);
                return true;
            } catch {
                result = default(T);
                return false;
            }
        }
    }
}

