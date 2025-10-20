using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.SqliteVec;
using Microsoft.SemanticKernel.Data;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;

#pragma warning disable SKEXP0001 

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SqliteOptions>(builder.Configuration.GetSection("Sqlite"));
builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSqliteVectorStore(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SqliteOptions>>().Value;
    return options.DatabasePath;
},
 (sp) =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    var options = new SqliteVectorStoreOptions
    {
        EmbeddingGenerator = embeddingGenerator,
        VectorVirtualTableName = "FoodEmbedding",
    };
    return options;
});

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
builder.Services.AddSingleton( sp =>
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
    // var _executionSettings = new OllamaPromptExecutionSettings()
    // {
    //     FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
    // };
    var searchPlugin = vectorStoreTextSearch.CreateWithGetTextSearchResults("SearchPlugin");
    kernel.Plugins.Add(searchPlugin);
    return kernel;
});

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", (Kernel kernel) => {
    return "Hi";
});

app.Run();
