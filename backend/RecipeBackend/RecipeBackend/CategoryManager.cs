using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Data.SqlClient;
using System.Linq;
using static RecipeBackend.RecipeManager;

namespace RecipeBackend
{
    public class CategoryManager //you can assign categories you dont own but cant read them
    {

        public class CategoryResult
        {
            public bool successful;
            public string? reason;
        }
        public class CategoryListResult
        {
            public bool successful;
            public string? reason;
            public List<Category>? categories;
        }
        public struct Category
        {
            public int id;
            public string name;
            public int color;
        }

        private string connectionString;

        public CategoryManager(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public CategoryResult CreateCategory(string token, string name, int color)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                using (SqlCommand command = new SqlCommand("INSERT INTO [categories] VALUES (@owner, @name, @color)", connection))
                {
                    command.Parameters.AddWithValue("owner", tokenInfo.userID);
                    command.Parameters.AddWithValue("name", name.Replace("[", "").Replace("]", "").Trim());
                    command.Parameters.AddWithValue("color", color);
                    command.ExecuteNonQuery();
                }

                return new CategoryResult
                {
                    successful = true
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public CategoryResult AssignCategory(string token, int recipeID, int categoryID)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = RecipeManager.GetOwnershipInfo(connection, tokenInfo.userID!.Value, recipeID);
                if (!ownershipInfo.owns)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                using (SqlCommand command = new SqlCommand("INSERT INTO [categoryrecipe] VALUES (@recipeID, @categoryID)", connection))
                {
                    command.Parameters.AddWithValue("recipeID", recipeID);
                    command.Parameters.AddWithValue("categoryID", categoryID);
                    command.ExecuteNonQuery();
                }

                return new CategoryResult
                {
                    successful = true
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public CategoryResult UnassignCategory(string token, int recipeID, int categoryID) //i dont care i think its a word
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = RecipeManager.GetOwnershipInfo(connection, tokenInfo.userID!.Value, recipeID);
                if (!ownershipInfo.owns)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                using (SqlCommand command = new SqlCommand("DELETE FROM [categoryrecipe] WHERE recipeID=@recipeID, categoryID=@categoryID", connection))
                {
                    command.Parameters.AddWithValue("recipeID", recipeID);
                    command.Parameters.AddWithValue("categoryID", categoryID);
                    if (command.ExecuteNonQuery() > 0)
                    {
                        return new CategoryResult
                        {
                            successful = true
                        };
                    } else
                    {
                        return new CategoryResult
                        {
                            successful = false,
                            reason = "Category not on recipe"
                        };
                    }
                }

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public CategoryListResult GetCategories(string token)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryListResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                List<Category> results = new List<Category>();
                using (SqlCommand command = new SqlCommand("SELECT * FROM [categories] WHERE owner=@userID", connection))
                {
                    command.Parameters.AddWithValue("userID", tokenInfo.userID!.Value);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new Category
                            {
                                name = (string)reader["name"],
                                id = (int)reader["id"],
                                color = (int)reader["color"]
                            });
                        }
                    }
                }

                return new CategoryListResult
                {
                    successful = true,
                    categories = results
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryListResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public CategoryResult SetRecipeCategories(string token, int recipeID, string categoryList)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return SetRecipeCategories(connection, token, recipeID, categoryList);
        }

        public static CategoryResult SetRecipeCategories(SqlConnection connection, string token, int recipeID, string categoryList)
        {
            try
            {
                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = RecipeManager.GetOwnershipInfo(connection, tokenInfo.userID!.Value, recipeID);
                if (!ownershipInfo.owns)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                IEnumerable<int> categoryIDs = categoryList.Split(',')
                                     .Select<string, int?>(s => int.TryParse(s.Trim(), out int num) ? num : null)
                                     .Where(num => num.HasValue)
                                     .Select(num => num!.Value);

                using (SqlCommand command = new SqlCommand("DELETE FROM [categoryrecipe] WHERE recipe=@recipeID", connection))
                {
                    command.Parameters.AddWithValue("recipeID", recipeID);
                    command.ExecuteNonQuery();
                }

                if (categoryIDs.Any()) //if 0 elements syntax error
                {
                    string commandString = "INSERT INTO [categoryrecipe] (recipe, category) VALUES ";
                    commandString += string.Join(", ", categoryIDs.Select(categoryID => $"({recipeID},{categoryID})")); //i dont care theyre strings its safe
                    using (SqlCommand command = new SqlCommand(commandString, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                return new CategoryResult
                {
                    successful = true
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public CategoryListResult GetRecipeCategories(string token, int recipeID)
        {

            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                return GetRecipeCategories(connection, token, recipeID);
            } catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryListResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public static CategoryListResult GetRecipeCategories(SqlConnection connection, string token, int recipeID) {
            try
            {

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryListResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = RecipeManager.GetOwnershipInfo(connection, tokenInfo.userID!.Value, recipeID);
                if (!ownershipInfo.owns)
                {
                    return new CategoryListResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                List<int> categories = new List<int>();
                using (SqlCommand command = new SqlCommand("SELECT category FROM [categoryrecipe] WHERE recipe=@recipeID", connection))
                {
                    command.Parameters.AddWithValue("recipeID", recipeID);
                    using (SqlDataReader reader = command.ExecuteReader()) {
                        while (reader.Read())
                        {
                            categories.Add((int)reader["category"]);
                        }
                    }
                }

                if (categories.Count == 0)
                {
                    return new CategoryListResult
                    {
                        successful = true,
                        categories = new List<Category>()
                    };
                }

                List<Category> results = new List<Category>();
                using (SqlCommand command = new SqlCommand("SELECT * FROM [categories] WHERE [owner]=@userID AND [id] IN (@categoryIDs)", connection))
                {
                    command.AddArrayParameters("categoryIDs", categories);
                    command.Parameters.AddWithValue("userID", tokenInfo.userID!.Value);
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            results.Add(new Category {
                                name = (string)reader["name"],
                                id = (int)reader["id"],
                                color = (int)reader["color"]
                            });
                        }
                    }
                }

                return new CategoryListResult
                {
                    successful = true,
                    categories = results
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryListResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public static Util.OwnershipInfo GetOwnershipInfo(SqlConnection connection, int userID, int categoryID)
        {
            using SqlCommand command = new SqlCommand("SELECT [owner] FROM [categories] WHERE id=@id", connection);
            command.Parameters.AddWithValue("id", categoryID);
            using SqlDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new Util.OwnershipInfo
                {
                    owns = false,
                    reason = "No such category",
                };
            }

            int owner = (int)reader["owner"];
            if (owner != userID)
            {
                return new Util.OwnershipInfo
                {
                    owns = false,
                    reason = "ID mismatch"
                };
            }

            return new Util.OwnershipInfo
            {
                owns = true
            };
        }
        public CategoryResult EditCategory(string token, int id, string name, int color)
        {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = GetOwnershipInfo(connection, tokenInfo.userID!.Value, id);
                if (!ownershipInfo.owns)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                using (SqlCommand command = new SqlCommand("UPDATE [categories] SET name=@name, color=@color WHERE id=@id", connection))
                {
                    command.Parameters.AddWithValue("id", id);
                    command.Parameters.AddWithValue("name", name);
                    command.Parameters.AddWithValue("color", color);
                    command.ExecuteNonQuery();
                }

                return new CategoryResult
                {
                    successful = true
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public CategoryResult DeleteCategory(string token, int categoryID)
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = GetOwnershipInfo(connection, tokenInfo.userID!.Value, categoryID);
                if (!ownershipInfo.owns)
                {
                    return new CategoryResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                using (SqlCommand command = new SqlCommand("DELETE FROM [categoryrecipe] WHERE category=@categoryID", connection))
                {
                    command.Parameters.AddWithValue("categoryID", categoryID);
                    command.ExecuteNonQuery();
                }

                using (SqlCommand command = new SqlCommand("DELETE FROM [categories] WHERE id=@categoryID", connection))
                {
                    command.Parameters.AddWithValue("categoryID", categoryID);
                    command.ExecuteNonQuery();
                }

                return new CategoryResult
                {
                    successful = true
                };
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return new CategoryResult
                {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
    }
}
