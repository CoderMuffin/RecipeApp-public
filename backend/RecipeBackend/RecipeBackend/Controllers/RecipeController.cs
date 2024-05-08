using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis;
using NuGet.Protocol;
using System.Net;
using System.Net.Mime;
using System.Web;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace RecipeBackend.Controllers {
    public struct CreateRequest {
        public string name { get; set; }
        public string description { get; set; }
    }
    [Route("api/recipe")]
    [ApiController]
    public class RecipeController : ControllerBase {
        private readonly RecipeManager recipes;
        public RecipeController(IConfiguration config) {
            recipes = new RecipeManager(@"Data Source=.\SQLExpress;Initial Catalog=recipes;");
        }

        [Route("search")]
        [HttpGet]
        public string Search() {
            if (ExpectToken(out string token)) {
                RecipeManager.SearchResult result = recipes.SearchRecipe(token, Request.Query["q"]);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("get")]
        [HttpGet]
        public string Get() {
            if (ExpectToken(out string token) && int.TryParse(Request.Query["id"], out int id)) {
                RecipeManager.GetResult result = recipes.GetRecipe(token, id);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("file")]
        [HttpGet]
        public ActionResult File() {
            if (ExpectToken(out string token) && int.TryParse(Request.Query["id"], out int id)) {
                RecipeManager.FileResult result = recipes.GetFile(token, id);
                if (!result.successful) {
                    return Problem(result.reason);
                }
                return PhysicalFile(result.path!, MimeTypes.GetMimeType(result.path!));
            } else {
                return Problem("Malformed request");
            }
        }

        [Route("image")]
        [HttpGet]
        public ActionResult Image() {
            if (ExpectToken(out string token) && int.TryParse(Request.Query["id"], out int id)) {
                RecipeManager.FileResult result = recipes.GetImage(token, id);
                if (!result.successful) {
                    Console.WriteLine(result.reason);
                    return Problem(result.reason);
                }
                return PhysicalFile(result.path!, MimeTypes.GetMimeType(result.path!));
            } else {
                return Problem("Malformed request");
            }
        }

        [Route("create")]
        [HttpPost]
        public async Task<string> Create() {
            if (!(ExpectToken(out string token) && ExpectFormString("title", out string title) && ExpectFormString("description", out string description))) {
                return "Malformed request";
            }

            RecipeManager.CreateResult result;
            if (ExpectFormFile("file", out IFormFile? file)) {
                result = recipes.CreateFileRecipe(token, title, description, Request.Form.Files["image"], file!);
            } else if (ExpectFormString("url", out string url)) {
                result = await recipes.CreateURLRecipe(token, title, description, Request.Form.Files["image"], url);
            } else {
                return "Malformed request";
            }
            return result.ToJson();
        }

        [Route("update")]
        [HttpPost]
        public string Update() {
            if (ExpectToken(out string token) && ExpectFormInt("id", out int id)) {
                RecipeManager.UpdateResult result = recipes.UpdateRecipe(token, id, MaybeFormString("title"), MaybeFormString("description"), Request.Form.Files["image"], Request.Form.Files["file"], MaybeFormString("url"));
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        [Route("delete")]
        [HttpPost]
        public string Delete() {
            if (ExpectToken(out string token) && ExpectFormInt("id", out int id)) {
                RecipeManager.DeleteResult result = recipes.DeleteRecipe(token, id);
                return result.ToJson();
            } else {
                return "Malformed request";
            }
        }

        bool ExpectToken(out string token) {
            string[] parts = Request.Headers.Authorization.ToString().Split("Bearer ");
            if (parts.Length != 2) {
                token = "";
                return false;
            }
            token = parts[1];
            return true;
        }
        bool ExpectFormString(string key, out string field) {
            if (Request.Form.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues value)) {
                field = value;
                return true;
            }
            field = "";
            return false;
        }
        string? MaybeFormString(string key) {
            if (Request.Form.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues value)) {
                return value;
            }
            return null;
        }
        bool ExpectFormInt(string key, out int field) {
            if (ExpectFormString(key, out string value) && int.TryParse(value, out int result)) {
                field = result;
                return true;
            }
            field = 0;
            return false;
        }

        bool ExpectFormFile(string key, out IFormFile? file) {
            file = Request.Form.Files[key];
            return file != null;
        }
    }
}
