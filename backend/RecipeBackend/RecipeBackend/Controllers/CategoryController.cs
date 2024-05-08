using Microsoft.AspNetCore.Mvc;
using NuGet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RecipeBackend.Controllers
{
    [Route("api/category")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly CategoryManager categories;
        public CategoryController(IConfiguration config)
        {
            categories = new CategoryManager(@"Data Source=.\SQLExpress;Initial Catalog=recipes;");
        }

        [Route("create")]
        [HttpPost]
        public string Create()
        {
            if (ExpectToken(out string token) && ExpectFormString("name", out string name) && ExpectFormInt("color", out int color))
            {
                CategoryManager.CategoryResult result = categories.CreateCategory(token, name, color);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("delete")]
        [HttpPost]
        public string Delete()
        {
            if (ExpectToken(out string token) && ExpectFormInt("id", out int id))
            {
                CategoryManager.CategoryResult result = categories.DeleteCategory(token, id);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("assign")]
        [HttpPost]
        public string Assign()
        {
            if (ExpectToken(out string token) && ExpectFormInt("recipe", out int recipe) && ExpectFormInt("category", out int category))
            {
                CategoryManager.CategoryResult result = categories.AssignCategory(token, recipeID: recipe, categoryID: category);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("unassign")]
        [HttpPost]
        public string Unassign()
        {
            if (ExpectToken(out string token) && ExpectFormInt("recipe", out int recipe) && ExpectFormInt("category", out int category))
            {
                CategoryManager.CategoryResult result = categories.UnassignCategory(token, recipeID: recipe, categoryID: category);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("edit")]
        [HttpPost]
        public string Edit()
        {
            if (ExpectToken(out string token) && ExpectFormInt("id", out int id) && ExpectFormString("name", out string name) && ExpectFormInt("color", out int color))
            {
                CategoryManager.CategoryResult result = categories.EditCategory(token, id, name, color);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("list")]
        [HttpGet]
        public string List()
        {
            if (ExpectToken(out string token))
            {
                CategoryManager.CategoryListResult result = categories.GetCategories(token);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("recipe")]
        [HttpGet]
        public string Recipe()
        {
            if (ExpectToken(out string token) && int.TryParse(Request.Query["id"], out int recipeID))
            {
                CategoryManager.CategoryListResult result = categories.GetRecipeCategories(token, recipeID);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        [Route("set")]
        [HttpPost]
        public string Set()
        {
            if (ExpectToken(out string token) && ExpectFormInt("id", out int recipeID) && ExpectFormString("categories", out string categoryIDs))
            {
                CategoryManager.CategoryResult result = categories.SetRecipeCategories(token, recipeID, categoryIDs);
                return result.ToJson();
            }
            else
            {
                return "Malformed request";
            }
        }

        //BEWARE, COPIED FROM RECIPECONTROLLER
        bool ExpectToken(out string token)
        {
            string[] parts = Request.Headers.Authorization.ToString().Split("Bearer ");
            if (parts.Length != 2)
            {
                token = "";
                return false;
            }
            token = parts[1];
            return true;
        }
        bool ExpectFormString(string key, out string field)
        {
            if (Request.Form.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues value))
            {
                field = value;
                return true;
            }
            field = "";
            return false;
        }
        bool ExpectFormInt(string key, out int field)
        {
            if (ExpectFormString(key, out string value) && int.TryParse(value, out int result))
            {
                field = result;
                return true;
            }
            field = 0;
            return false;
        }
    }
}
