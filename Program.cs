using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Data;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;
using Simplz.Nutrition.Services;
using Simplz.Nutrition.Data;

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

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    using var db = scope.ServiceProvider.GetRequiredService<Simplz.Nutrition.Data.NutritionContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();

app.MapGet("/", async (Kernel kernel) =>
{
    // var query = "What is the Semantic Kernel?";
    // var prompt = "{{FoodSearchPlugin.Search $query}}. {{$query}}";
    // KernelArguments arguments = new() { { "query", query } };
    // Console.WriteLine(await kernel.InvokePromptAsync(prompt, arguments));
    return "Hi";
});

app.MapGet("/import/food", async (ICSVReader csvReader, NutritionContext context, CancellationToken cancellationToken) =>
{
    var file = Path.Combine("Temp", "FoodData_Central_sr_legacy_food_csv_2018-04", "food.csv");
    var items = csvReader.ReadRecords<Simplz.Nutrition.Data.CSV.Food>(file).ToList();
    await context.Foods.AddRangeAsync(items.Select(f => new Food
    {
        Id = f.Id,
        Name = f.Description,
        FoodCategoryId = f.FoodCategoryId,
    }), cancellationToken);
    await context.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { Imported = true, Source = file, Count = items.Count });
});

app.Run();
