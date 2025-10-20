using Simplz.Nutrition.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SqliteOptions>(builder.Configuration.GetSection("Sqlite"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => "Hi");

app.Run();
