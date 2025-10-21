using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore; // Needed for db.Database.Migrate()
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Data;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;
using Simplz.Nutrition.Services;
// using Simplz.Nutrition.Services;

#pragma warning disable SKEXP0001 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Simplz.Nutrition.Data.NutritionContext>();
builder.Services.Configure<SqliteOptions>(builder.Configuration.GetSection("Sqlite"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSqliteVectorStore(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqliteOptions>>().Value;
    return options.DatabasePath;
},
sp =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    var options = new SqliteVectorStoreOptions
    {
        EmbeddingGenerator = embeddingGenerator,
        VectorVirtualTableName = "FoodEmbedding",
    };
    return options;
});
builder.Services.AddSingleton<ICSVReader, CSVReader>();
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    return new OllamaSharp.OllamaApiClient(new Uri(options.Endpoint), options.EmbeddingModel);
});
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    return new OllamaSharp.OllamaApiClient(new Uri(options.Endpoint), options.ChatModel);
});
// builder.Services.AddScoped<IFoodDataImportService, FoodDataImportService>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    var vectorStore = sp.GetRequiredService<Microsoft.Extensions.VectorData.VectorStore>();
    var collection = vectorStore.GetCollection<long, Food>("FoodEmbedding");
    collection.EnsureCollectionExistsAsync().ConfigureAwait(false);
    var builder = Kernel.CreateBuilder();
    builder.AddOllamaChatCompletion(options.ChatModel, new Uri(options.Endpoint))
           .AddOllamaTextGeneration(options.ChatModel, new Uri(options.Endpoint))
           .AddOllamaEmbeddingGenerator(options.EmbeddingModel, new Uri(options.Endpoint));

    var vectorStoreTextSearch = new VectorStoreTextSearch<Food>(collection, embeddingGenerator);
    var kernel = builder.Build();
    var searchPlugin = vectorStoreTextSearch.CreateWithSearch("SearchPlugin");
    kernel.Plugins.Add(searchPlugin);
    return kernel;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    using var db = scope.ServiceProvider.GetRequiredService<Simplz.Nutrition.Data.NutritionContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();

app.MapGet("/", async (Kernel kernel) =>
{
    var query = "What is the Semantic Kernel?";
    var prompt = "{{SearchPlugin.Search $query}}. {{$query}}";
    KernelArguments arguments = new() { { "query", query } };
    Console.WriteLine(await kernel.InvokePromptAsync(prompt, arguments));
    return "Hi";
});

app.MapGet("/import", async (ICSVReader csvReader, CancellationToken cancellationToken) =>
{
    var res = new List<object>();
    foreach (var file in Directory.GetFiles("Temp", "*.csv"))
    {
        var items = csvReader.ReadRecords(file);
        res.Add(new { Imported = true, Source = file, Items = items.First(), Count = items.Count() });
    }
    return Results.Ok(res);
});

app.Run();
