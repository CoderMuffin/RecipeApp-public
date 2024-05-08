using iTextSharp.text.pdf;
using Microsoft.Data.SqlClient;
using PuppeteerSharp;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Drawing;
using Microsoft.Office.Interop.Word;
using Xceed.Words.NET;
using Spire.Doc;

namespace RecipeBackend {
    public class RecipeManager {
        public class RecipeResult {
            public bool successful = false;
            public string? reason = null;
        }
        public class CreateResult : RecipeResult {
            public int? id = null;
        }
        public class UpdateResult : RecipeResult { }
        public class GetResult : RecipeResult {
            public string? title = null;
            public string? description = null;
            public string? url = null;
            public string? type = null;
            public DateTime? created = null;
            public DateTime? modified = null;
        }
        public class FileResult : RecipeResult {
            public string? path = null;
        }
        public class DeleteResult : RecipeResult { }
        public class SearchResult : RecipeResult {
            public RecipeInfo[]? recipes = null;
        }
        public class RecipeInfo {
            public int id;
            public string? title = null;
            public string? hint = null;
            public DateTime modified;
            public List<CategoryManager.Category>? categories = null;
        }
        public const string recipeStore = @"C:\Working\recipes.rectanglered.com\recipestore";
        private string connectionString;

        public RecipeManager(string connectionString) {
            this.connectionString = connectionString;
        }

        public string ExtractSearchables(string path) {
            try {
                path = GetFullPath(path);
                string extension = Path.GetExtension(path);
                if (extension == ".pdf") {
                    StringBuilder sb = new StringBuilder();
                    var strategy = new iTextSharp.text.pdf.parser.SimpleTextExtractionStrategy();
                    using (var reader = new PdfReader(path)) {
                        for (var i = 1; i <= reader.NumberOfPages; i++) {
                            var currentText = iTextSharp.text.pdf.parser.PdfTextExtractor.GetTextFromPage(reader, i, strategy);
                            sb.Append(currentText);
                        }
                    }
                    return sb.ToString();
                } else if (extension == ".docx") {
                    using (WordprocessingDocument doc = WordprocessingDocument.Open(path, false)) {
                        StringBuilder sb = new StringBuilder();

                        var paragraphs = doc.MainDocumentPart?.Document.Body?.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>() ?? Array.Empty<DocumentFormat.OpenXml.Drawing.Paragraph>();

                        foreach (var paragraph in paragraphs) {
                            foreach (var run in paragraph.Elements<Run>()) {
                                foreach (var text in run.Elements<Text>()) {
                                    sb.Append(text.Text);
                                }
                            }

                            // Add a line break between paragraphs
                            sb.AppendLine();
                        }

                        return sb.ToString();
                    }
                } else {
                    Console.Error.WriteLine("Unknown file format");
                    return ""; //???
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return "";
            }
        }

        int CreateRecipe(SqlConnection connection, int owner, string title, string description, string searchables, string filePath, IFormFile? image) {
            string imageSavePath = SaveImage(filePath, image);
            using SqlCommand commandRecipe = new SqlCommand("INSERT INTO [recipe] (owner, title, description, searchables, path, image, created, modified) OUTPUT INSERTED.id VALUES (@owner, @title, @description, @searchables, @path, @image, @created, @modified)", connection);
            commandRecipe.Parameters.AddWithValue("owner", owner);
            commandRecipe.Parameters.AddWithValue("title", title);
            commandRecipe.Parameters.AddWithValue("description", description);
            commandRecipe.Parameters.AddWithValue("searchables", searchables);
            commandRecipe.Parameters.AddWithValue("path", filePath);
            commandRecipe.Parameters.AddWithValue("image", imageSavePath);
            commandRecipe.Parameters.AddWithValue("created", DateTime.Now);
            commandRecipe.Parameters.AddWithValue("modified", DateTime.Now);
            return (int)commandRecipe.ExecuteScalar();
        }

        string SaveImage(string filePath, IFormFile? image)
        {
            if (image != null)
            {
                string imageSavePath = GenerateSavePath(image);
                using (FileStream fs = new FileStream(GetFullPath(imageSavePath), FileMode.CreateNew))
                {
                    image.CopyTo(fs);
                }
                return imageSavePath;
            }
            else
            {
                try
                {
                    if (filePath.EndsWith(".pdf"))
                    {
                        using (PdfReader pdfReader = new PdfReader(GetFullPath(filePath)))
                        {
                            iTextSharp.text.pdf.parser.PdfReaderContentParser parser = new(pdfReader);
                            ImageRenderListener listener = new ImageRenderListener();

                            parser.ProcessContent(1, listener);

                            Image pdfImage = listener.GetImage();

                            // Save the image preview
                            string imageFilePath = GenerateSavePath(".png");
                            pdfImage.Save(GetFullPath(imageFilePath), System.Drawing.Imaging.ImageFormat.Png);
                            return imageFilePath;
                        }
                    }
                } catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }

                //if we ended up here then either not .pdf or error occured
                string imageSavePath = GenerateSavePath(".empty");
                File.Create(GetFullPath(imageSavePath)).Close();
                return imageSavePath;
            }
        }

        class ImageRenderListener : iTextSharp.text.pdf.parser.IRenderListener
        {

            private Image? _image;

            public void BeginTextBlock() { }
            public void EndTextBlock() { }
            public void RenderImage(iTextSharp.text.pdf.parser.ImageRenderInfo renderInfo)
            {
                iTextSharp.text.pdf.parser.PdfImageObject imageObject = renderInfo.GetImage();
                if (imageObject != null)
                {
                    _image = imageObject.GetDrawingImage();
                }
            }

            public void RenderText(iTextSharp.text.pdf.parser.TextRenderInfo renderInfo) { }

            public Image GetImage()
            {
                return _image;
            }
        }

        string SaveIFormFile(IFormFile file) {
            string path = GenerateSavePath(file);
            using (FileStream fs = new FileStream(GetFullPath(path), FileMode.CreateNew)) {
                file.CopyTo(fs);
            }
            return path;
        }

        string SaveRecipeFile(IFormFile file)
        {
            var path = SaveIFormFile(file);
            if (path.EndsWith(".doc"))
            {
                path = ConvertDocToDocx(path);
            }
            return path;
        }

        public CreateResult CreateFileRecipe(string token, string title, string description, IFormFile? image, IFormFile file) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);

                if (!tokenInfo.valid) {
                    return new CreateResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                string fileSavePath = SaveRecipeFile(file);

                string searchables = ExtractSearchables(fileSavePath);
                int recipeID = CreateRecipe(connection, (int)tokenInfo.userID!, title, description, searchables, fileSavePath, image);

                return new CreateResult {
                    successful = true,
                    id = recipeID
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new CreateResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public async Task<CreateResult> CreateURLRecipe(string token, string title, string description, IFormFile? image, string url) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);

                if (!tokenInfo.valid) {
                    return new CreateResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                string fileSavePath = GenerateSavePath(".pdf");

                try
                {
                    await ConvertWebPageToPdf(url, GetFullPath(fileSavePath));
                } catch (PuppeteerSharp.NavigationException e)
                {
                    return new CreateResult
                    {
                        successful = false,
                        reason = "Could not locate resource at URL"
                    };
                }
                string searchables = ExtractSearchables(fileSavePath);
                int recipeID = CreateRecipe(connection, (int)tokenInfo.userID!, title, description, searchables, fileSavePath, image);

                using SqlCommand commandFile = new SqlCommand("INSERT INTO [urlrecipe] (recipe, url) VALUES (@recipe, @url)", connection);
                commandFile.Parameters.AddWithValue("recipe", recipeID);
                commandFile.Parameters.AddWithValue("url", url);
                commandFile.ExecuteNonQuery();

                return new CreateResult {
                    successful = true,
                    id = recipeID
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new CreateResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public UpdateResult UpdateRecipe(string token, int id, string? title, string? description, IFormFile? image, IFormFile? file, string? url) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);

                if (!tokenInfo.valid) {
                    return new UpdateResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                using (SqlCommand commandRecipe = new SqlCommand("UPDATE [recipe] SET title = ISNULL(@title, title), description = ISNULL(@description, description), modified = @modified WHERE id = @id", connection)) {
                    commandRecipe.Parameters.AddWithValue("id", id);

                    if (title != null) commandRecipe.Parameters.AddWithValue("title", title);
                    else commandRecipe.Parameters.AddWithValue("title", DBNull.Value);

                    if (description != null) commandRecipe.Parameters.AddWithValue("description", description);
                    else commandRecipe.Parameters.AddWithValue("description", DBNull.Value);

                    commandRecipe.Parameters.AddWithValue("modified", DateTime.Now);
                    commandRecipe.ExecuteNonQuery();
                }

                if (image != null) {
                    string newImage = SaveIFormFile(image);

                    using (SqlCommand commandImage = new SqlCommand("UPDATE [recipe] SET image=@image OUTPUT DELETED.image WHERE id=@id", connection)) {
                        commandImage.Parameters.AddWithValue("id", id);
                        commandImage.Parameters.AddWithValue("image", newImage);

                        string oldImage = (string)commandImage.ExecuteScalar();
                        if (oldImage != null) {
                            File.Delete(oldImage);
                        }
                    }
                }
                if (file != null) {
                    string newFile = SaveRecipeFile(file);
                    string searchables = ExtractSearchables(newFile);

                    using (SqlCommand commandFile = new SqlCommand("UPDATE [recipe] SET [searchables]=@searchables, [path]=@path OUTPUT DELETED.[path] WHERE id=@id", connection)) {
                        commandFile.Parameters.AddWithValue("id", id);
                        commandFile.Parameters.AddWithValue("path", newFile);
                        commandFile.Parameters.AddWithValue("searchables", searchables);

                        string oldFile = (string)commandFile.ExecuteScalar();
                        if (oldFile != null) {
                            File.Delete(oldFile);
                        }
                    }
                }

                if (url != null) {
                    using (SqlCommand commandUrl = new SqlCommand("UPDATE [urlrecipe] SET [url]=@url OUTPUT DELETED.url WHERE recipe=@id", connection)) {
                        commandUrl.Parameters.AddWithValue("id", id);
                        commandUrl.Parameters.AddWithValue("url", url);
                        commandUrl.ExecuteNonQuery();
                    }
                }

                return new UpdateResult {
                    successful = true
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new UpdateResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        private string GenerateSavePath(IFormFile? file) {
            return DateTime.Now.Ticks + "." + Guid.NewGuid().ToString() + (Path.GetExtension(file?.FileName) ?? ".unknown");
        }
        private string GenerateSavePath(string ext) {
            return DateTime.Now.Ticks + "." + Guid.NewGuid().ToString() + ext;
        }

        private string GetFullPath(string path) {
            return Path.GetFullPath(Path.Join(recipeStore, path));
        }

        public GetResult GetRecipe(string token, int id) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);

                if (!tokenInfo.valid) {
                    return new GetResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                string type = "file";
                string? url = null;

                using (SqlCommand command = new SqlCommand("SELECT url FROM [urlrecipe] WHERE recipe=@id", connection)) {
                    command.Parameters.AddWithValue("id", id);
                    using (SqlDataReader reader = command.ExecuteReader()) {
                        if (reader.Read()) {
                            type = "url";
                            url = (string)reader["url"];
                        }
                    }
                }
           

                using SqlCommand commandRecipe = new SqlCommand("SELECT owner, title, description, created, modified FROM [recipe] WHERE id=@id", connection);
                commandRecipe.Parameters.AddWithValue("id", id);
                using SqlDataReader readerRecipe = commandRecipe.ExecuteReader();
                if (!readerRecipe.Read()) {
                    return new GetResult {
                        successful = false,
                        reason = "No such recipe",
                    };
                }

                int owner = (int)readerRecipe["owner"];
                if (owner != tokenInfo.userID) {
                    return new GetResult {
                        successful = false,
                        reason = "You do not own this recipe."
                    };
                }

                return new GetResult {
                    successful = true,
                    title = (string)readerRecipe["title"],
                    description = (string)readerRecipe["description"],
                    created = (DateTime)readerRecipe["created"],
                    modified = (DateTime)readerRecipe["modified"],
                    type = type,
                    url = url
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new GetResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public static Util.OwnershipInfo GetOwnershipInfo(SqlConnection connection, int userID, int recipeID)
        {
            using SqlCommand command = new SqlCommand("SELECT [owner] FROM [recipe] WHERE id=@id", connection);
            command.Parameters.AddWithValue("id", recipeID);
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

        public FileResult GetFile(string token, int id) {
            return GetFileField(token, id, "path");
        }
        public FileResult GetImage(string token, int id) {
            return GetFileField(token, id, "image");
        }

        private FileResult GetFileField(string token, int id, string field) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);

                if (!tokenInfo.valid) {
                    return new FileResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                using SqlCommand commandRecipe = new SqlCommand("SELECT owner, " + field + " FROM [recipe] WHERE id=@id", connection);
                commandRecipe.Parameters.AddWithValue("id", id);
                using SqlDataReader readerRecipe = commandRecipe.ExecuteReader();
                if (!readerRecipe.Read()) {
                    return new FileResult {
                        successful = false,
                        reason = "No such recipe",
                    };
                }

                int owner = (int)readerRecipe["owner"];
                if (owner != tokenInfo.userID) {
                    return new FileResult {
                        successful = false,
                        reason = "You do not own this recipe."
                    };
                }

                if (readerRecipe[field] == DBNull.Value) {
                    return new FileResult {
                        successful = false,
                        reason = "No file set"
                    };
                }

                string path = GetFullPath((string)readerRecipe[field]);
                if (!File.Exists(path)) {
                    return new FileResult {
                        successful = false,
                        reason = "File not present on this server"
                    };
                }

                return new FileResult {
                    successful = true,
                    path = path
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new FileResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        private const string categoryPattern = @"\[[^\]]+\]";
        public SearchResult SearchRecipe(string token, string query) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid) {
                    return new SearchResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                MatchCollection categories = Regex.Matches(query, categoryPattern);
                query = Regex.Replace(query, categoryPattern, "");

                using SqlCommand command = new SqlCommand( //good luck
                    "SELECT recipe.id AS id, recipe.title AS title, LEFT(recipe.description, 100) AS description, LEFT(searchables, 100) AS searchables, recipe.modified AS modified " +
                    "FROM [recipe] " +
                    
                    (categories.Count > 0 ?
                        "JOIN categoryrecipe ON recipe.id=categoryrecipe.recipe JOIN categories ON categoryrecipe.category=categories.id " :
                        ""
                    ) +

                    "WHERE recipe.owner=@owner AND " +
                    (categories.Count > 0 ? "categories.name IN (@categories) AND " : "" ) +
                    "((recipe.description LIKE '%' + @q + '%') OR (recipe.title LIKE '%' + @q + '%') OR (recipe.searchables LIKE '%' + @q + '%')) " +
                    
                    "ORDER BY CASE " +
                        "WHEN recipe.title LIKE '%' + @q + '%' THEN 1 " +
                        "WHEN recipe.description LIKE '%' + @q + '%' THEN 2 " +
                        "WHEN recipe.searchables LIKE '%' + @q + '%' THEN 3 " +
                        "ELSE 4 " +
                    "END", connection
                );

                command.Parameters.AddWithValue("owner", tokenInfo.userID);
                command.Parameters.AddWithValue("q", query);
                command.AddArrayParameters("categories", categories.Select(x => x.Value.Replace("[", "").Replace("]", "")));

                List<RecipeInfo> recipeInfo = new List<RecipeInfo>();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        recipeInfo.Add(new RecipeInfo
                        {
                            id = (int)reader["id"],
                            title = (string)reader["title"],
                            hint = (string)reader["description"] + "\n" + (string)reader["searchables"],
                            modified = (DateTime)reader["modified"]
                        });
                    }
                }

                foreach (RecipeInfo recipe in recipeInfo) {
                    CategoryManager.CategoryListResult result = CategoryManager.GetRecipeCategories(connection, token, recipe.id);
                    if (result.successful)
                    {
                        recipe.categories = result.categories;
                    } else
                    {
                        Console.Error.WriteLine("Read recipe categories failed: " + result.reason + "\n\n");
                    }
                }

                return new SearchResult {
                    successful = true,
                    recipes = recipeInfo.ToArray()
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new SearchResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }

        public DeleteResult DeleteRecipe(string token, int id) {
            try {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                Auth.TokenInfo tokenInfo = Auth.GetTokenInfo(connection, token);
                if (!tokenInfo.valid) {
                    return new DeleteResult {
                        successful = false,
                        reason = tokenInfo.reason
                    };
                }

                Util.OwnershipInfo ownershipInfo = GetOwnershipInfo(connection, tokenInfo.userID!.Value, id);
                if (!ownershipInfo.owns)
                {
                    return new DeleteResult
                    {
                        successful = false,
                        reason = ownershipInfo.reason
                    };
                }

                CategoryManager.SetRecipeCategories(connection, token, id, "");

                using SqlCommand commandURL = new SqlCommand("DELETE FROM [urlrecipe] WHERE recipe=@id", connection);
                commandURL.Parameters.AddWithValue("id", id);
                commandURL.ExecuteNonQuery();

                using SqlCommand commandDeleteRecipe = new SqlCommand("DELETE FROM [recipe] OUTPUT DELETED.image, DELETED.[path] WHERE id=@id", connection);
                commandDeleteRecipe.Parameters.AddWithValue("id", id);
                using SqlDataReader reader = commandDeleteRecipe.ExecuteReader();
                while (reader.Read()) {
                    Console.WriteLine(reader.ToString());
                    if (reader["path"] != DBNull.Value) File.Delete((string) reader["path"]);
                    if (reader["image"] != DBNull.Value) File.Delete((string) reader["image"]);
                }

                return new DeleteResult {
                    successful = true
                };
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                return new DeleteResult {
                    successful = false,
                    reason = "Internal error"
                };
            }
        }
        public string ConvertDocToDocx(string name)
        {
            string newName = name + "x";

            Spire.Doc.Document document = new();

            document.LoadFromFile(GetFullPath(name));
            document.SaveToFile(GetFullPath(newName), FileFormat.Docx);

            File.Delete(name);
            return newName;
        }
        public async System.Threading.Tasks.Task ConvertWebPageToPdf(string url, string outputPath) {
            await new BrowserFetcher().DownloadAsync();

            var launchOptions = new LaunchOptions { Headless = true };
            using (var browser = await Puppeteer.LaunchAsync(launchOptions))
            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(url);
                await page.PdfAsync(outputPath);
            }
        }
    }
}
