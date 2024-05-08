using Microsoft.AspNetCore.Server.Kestrel.Core;

AppDomain.CurrentDomain.SetData("DataDirectory", @"C:\Working\recipes.rectanglered.com\recipestore");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

// If using Kestrel:
builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.AllowSynchronousIO = true;
});

// If using IIS:
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AllowSynchronousIO = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.UseSwagger();
    app.UseSwaggerUI();
}

object logLock = new object();
app.Use(async (context, next) => {
    context.Response.Headers["Cache-Control"] = "no-cache, no-store, max-age=0";
    context.Response.Headers["Pragma"] = "no-cache";

    await next.Invoke();

    try
    {
        lock (logLock)
        {
            string logMessage = $"{DateTime.UtcNow} - {context.Request.Method} {context.Request.Path}";
            File.AppendAllText(@"C:\Working\recipes.rectanglered.com\log.txt", logMessage + Environment.NewLine);
        }
    } catch (Exception e)
    {
        Console.Error.WriteLine(e);
    }
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseCors(builder =>
                builder.AllowAnyOrigin()
                       .AllowAnyHeader()
                       .AllowAnyMethod());

app.MapControllers();

Console.WriteLine(Path.GetFullPath("."));

app.Run();
// "Urls":  "http://*:5000;https://*:7000"
