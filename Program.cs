using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Data;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;
using Simplz.Nutrition.Services;

//https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/sqlite-connector?pivots=programming-language-csharp
//https://learn.microsoft.com/en-us/semantic-kernel/concepts/text-search/text-search-plugins?source=recommendations&pivots=programming-language-csharp

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

builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    var vectorStore = sp.GetRequiredService<Microsoft.Extensions.VectorData.VectorStore>();
    var collection = vectorStore.GetCollection<long, Food>("Food");
    collection.EnsureCollectionExistsAsync().ConfigureAwait(false);
    var builder = Kernel.CreateBuilder();
    builder.AddOllamaChatCompletion(options.ChatModel, new Uri(options.Endpoint))
           .AddOllamaTextGeneration(options.ChatModel, new Uri(options.Endpoint))
           .AddOllamaEmbeddingGenerator(options.EmbeddingModel, new Uri(options.Endpoint));

    var vectorStoreTextSearch = new VectorStoreTextSearch<Food>(collection, embeddingGenerator);
    var kernel = builder.Build();
    var searchPlugin = vectorStoreTextSearch.CreateWithSearch("FoodSearchPlugin");
    kernel.Plugins.Add(searchPlugin);
    return kernel;
});

builder.Services.AddScoped<ImportService>();

var app = builder.Build();

// using (var scope = app.Services.CreateScope())
// {
//     using var db = scope.ServiceProvider.GetRequiredService<NutritionContext>();
//     db.Database.Migrate();
// }

app.UseHttpsRedirection();

app.MapGet("/", async (Kernel kernel, Microsoft.Extensions.VectorData.VectorStore vectorStore) =>
{
    var collection = vectorStore.GetCollection<long, Food>("Food");
    List<object> results = [];
    await foreach (var f in collection.SearchAsync("chicken raw", 5))
    {
        results.Add(new { f.Record.Name, f.Score });
    }
    return Results.Ok(results);
    // var foodSearchPlugin = kernel.Plugins["FoodSearchPlugin"];
    // var getTextSearchResults = foodSearchPlugin["Search"];
    // var response = await kernel.InvokeAsync(getTextSearchResults, new() { ["query"] = "Tell me a type of chicken" });
    // return Results.Ok(response.GetValue<string>());
});

app.MapGet("/import/food", async (ImportService importService, CancellationToken cancellationToken) =>
{
    await importService.ImportFoodDataAsync(cancellationToken);
    return Results.Ok();
});

app.Run();
