using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;

namespace Simplz.Nutrition.Services;

public interface IFoodDataImportService
{
    Task ImportAsync(string datasetRootPath, CancellationToken cancellationToken = default);
}

public sealed class FoodDataImportService : IFoodDataImportService
{
    private const string FoodCollectionName = "FoodEmbedding";

    private readonly SqliteOptions _sqliteOptions;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly VectorStore _vectorStore;
    private readonly ILogger<FoodDataImportService> _logger;

    public FoodDataImportService(
        IOptions<SqliteOptions> sqliteOptions,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        VectorStore vectorStore,
        ILogger<FoodDataImportService> logger)
    {
        _sqliteOptions = sqliteOptions.Value;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task ImportAsync(string datasetRootPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(datasetRootPath))
        {
            throw new ArgumentException("Dataset path must be provided.", nameof(datasetRootPath));
        }

        if (!Directory.Exists(datasetRootPath))
        {
            throw new DirectoryNotFoundException($"Dataset directory '{datasetRootPath}' was not found.");
        }

        _logger.LogInformation("Starting import from {DatasetRootPath}", datasetRootPath);

        await using var connection = new SqliteConnection($"Data Source={_sqliteOptions.DatabasePath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureTablesAsync(connection, cancellationToken);

        var categories = await LoadCategoriesAsync(datasetRootPath, cancellationToken);
        await UpsertCategoriesAsync(connection, categories, cancellationToken);

        var nutrients = await LoadNutrientsAsync(datasetRootPath, cancellationToken);
        await UpsertNutrientsAsync(connection, nutrients, cancellationToken);

        var foods = await LoadFoodsAsync(datasetRootPath, cancellationToken);
        await UpsertFoodsAsync(connection, foods, cancellationToken);

        await UpsertFoodNutrientsAsync(connection, datasetRootPath, cancellationToken);
        await connection.CloseAsync();

        await UpsertFoodEmbeddingsAsync(foods, cancellationToken);

        _logger.LogInformation("Import completed successfully.");
    }

    private static CsvConfiguration CreateCsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            BadDataFound = null,
        };
    }

    private static async Task EnsureTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var commands = new[]
        {
            "CREATE TABLE IF NOT EXISTS FoodCategory (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS Nutrient (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, UnitName TEXT NOT NULL);",
            "CREATE TABLE IF NOT EXISTS Food (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, FoodCategoryId INTEGER NOT NULL REFERENCES FoodCategory(Id));",
            "CREATE TABLE IF NOT EXISTS FoodNutrient (FoodId INTEGER NOT NULL REFERENCES Food(Id), NutrientId INTEGER NOT NULL REFERENCES Nutrient(Id), Amount REAL NOT NULL, PRIMARY KEY (FoodId, NutrientId));",
        };

        foreach (var sql in commands)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<FoodCategory>> LoadCategoriesAsync(string datasetRootPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(datasetRootPath, "wweia_food_category.csv");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Unable to locate category file at {Path}", path);
            return Array.Empty<FoodCategory>();
        }

        var categories = new List<FoodCategory>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = csv.GetRecord<WweiaCategoryRecord>();
            if (!long.TryParse(record.WweiaFoodCategory, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                continue;
            }

            categories.Add(new FoodCategory
            {
                Id = id,
                Name = record.WweiaFoodCategoryDescription,
            });
        }

        _logger.LogInformation("Loaded {Count} categories", categories.Count);
        return categories;
    }

    private async Task<IReadOnlyList<Nutrient>> LoadNutrientsAsync(string datasetRootPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(datasetRootPath, "nutrient.csv");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Unable to locate nutrient file at {Path}", path);
            return Array.Empty<Nutrient>();
        }

        var nutrients = new List<Nutrient>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = csv.GetRecord<NutrientRecord>();
            nutrients.Add(new Nutrient
            {
                Id = record.Id,
                Name = record.Name,
                UnitName = record.UnitName,
            });
        }

        _logger.LogInformation("Loaded {Count} nutrients", nutrients.Count);
        return nutrients;
    }

    private async Task<IReadOnlyList<Food>> LoadFoodsAsync(string datasetRootPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(datasetRootPath, "food.csv");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Unable to locate food file at {Path}", path);
            return Array.Empty<Food>();
        }

        var foods = new List<Food>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = csv.GetRecord<FoodRecord>();
            foods.Add(new Food
            {
                Id = record.Id,
                Name = record.Description,
                FoodCategoryId = record.FoodCategoryId ?? 0,
            });
        }

        _logger.LogInformation("Loaded {Count} foods", foods.Count);
        return foods;
    }

    private static async Task UpsertCategoriesAsync(SqliteConnection connection, IReadOnlyList<FoodCategory> categories, CancellationToken cancellationToken)
    {
        if (categories.Count == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = "INSERT OR REPLACE INTO FoodCategory (Id, Name) VALUES ($id, $name);";
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "$id";
        command.Parameters.Add(idParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        command.Parameters.Add(nameParameter);
        command.Prepare();

        foreach (var category in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            idParameter.Value = category.Id;
            nameParameter.Value = category.Name;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpsertNutrientsAsync(SqliteConnection connection, IReadOnlyList<Nutrient> nutrients, CancellationToken cancellationToken)
    {
        if (nutrients.Count == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = "INSERT OR REPLACE INTO Nutrient (Id, Name, UnitName) VALUES ($id, $name, $unit);";
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "$id";
        command.Parameters.Add(idParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        command.Parameters.Add(nameParameter);
        var unitParameter = command.CreateParameter();
        unitParameter.ParameterName = "$unit";
        command.Parameters.Add(unitParameter);
        command.Prepare();

        foreach (var nutrient in nutrients)
        {
            cancellationToken.ThrowIfCancellationRequested();
            idParameter.Value = nutrient.Id;
            nameParameter.Value = nutrient.Name;
            unitParameter.Value = nutrient.UnitName;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task UpsertFoodsAsync(SqliteConnection connection, IReadOnlyList<Food> foods, CancellationToken cancellationToken)
    {
        if (foods.Count == 0)
        {
            return;
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = "INSERT OR REPLACE INTO Food (Id, Name, FoodCategoryId) VALUES ($id, $name, $categoryId);";
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "$id";
        command.Parameters.Add(idParameter);
        var nameParameter = command.CreateParameter();
        nameParameter.ParameterName = "$name";
        command.Parameters.Add(nameParameter);
        var categoryParameter = command.CreateParameter();
        categoryParameter.ParameterName = "$categoryId";
        command.Parameters.Add(categoryParameter);
        command.Prepare();

        foreach (var food in foods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            idParameter.Value = food.Id;
            nameParameter.Value = food.Name;
            categoryParameter.Value = food.FoodCategoryId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task UpsertFoodNutrientsAsync(SqliteConnection connection, string datasetRootPath, CancellationToken cancellationToken)
    {
        var path = Path.Combine(datasetRootPath, "food_nutrient.csv");
        if (!File.Exists(path))
        {
            _logger.LogWarning("Unable to locate food nutrient file at {Path}", path);
            return;
        }

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (await csv.ReadAsync())
        {
            csv.ReadHeader();
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction?)transaction;
        command.CommandText = "INSERT OR REPLACE INTO FoodNutrient (FoodId, NutrientId, Amount) VALUES ($foodId, $nutrientId, $amount);";
        var foodIdParameter = command.CreateParameter();
        foodIdParameter.ParameterName = "$foodId";
        command.Parameters.Add(foodIdParameter);
        var nutrientIdParameter = command.CreateParameter();
        nutrientIdParameter.ParameterName = "$nutrientId";
        command.Parameters.Add(nutrientIdParameter);
        var amountParameter = command.CreateParameter();
        amountParameter.ParameterName = "$amount";
        command.Parameters.Add(amountParameter);
        command.Prepare();

        var processed = 0;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = csv.GetRecord<FoodNutrientRecord>();

            foodIdParameter.Value = record.FoodId;
            nutrientIdParameter.Value = record.NutrientId;
            amountParameter.Value = record.Amount ?? 0d;
            await command.ExecuteNonQueryAsync(cancellationToken);

            processed++;
            if (processed % 10000 == 0)
            {
                _logger.LogInformation("Inserted {Count} food nutrients", processed);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Inserted {Count} food nutrient rows", processed);
    }

    private async Task UpsertFoodEmbeddingsAsync(IReadOnlyList<Food> foods, CancellationToken cancellationToken)
    {
        if (foods.Count == 0)
        {
            return;
        }

        var collection = _vectorStore.GetCollection<long, Food>(FoodCollectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        var batch = new List<Food>(32);
        var processed = 0;

        foreach (var food in foods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding = await _embeddingGenerator.GenerateEmbeddingAsync(food.Name, options: null, cancellationToken);
            var enrichedFood = new Food
            {
                Id = food.Id,
                Name = food.Name,
                FoodCategoryId = food.FoodCategoryId,
                DescriptionEmbedding = embedding.Vector,
            };

            batch.Add(enrichedFood);
            processed++;

            if (batch.Count >= 32)
            {
                await collection.UpsertBatchAsync(batch, cancellationToken);
                batch.Clear();
            }

            if (processed % 100 == 0)
            {
                _logger.LogInformation("Embedded {Count} foods", processed);
            }
        }

        if (batch.Count > 0)
        {
            await collection.UpsertBatchAsync(batch, cancellationToken);
        }

        _logger.LogInformation("Embedded {Count} foods", processed);
    }
}
