using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Simplz.Nutrition.Data;
using Simplz.Nutrition.Models;

namespace Simplz.Nutrition.Services;

public class ImportService
{
    private readonly ICSVReader _csvReader;
    private readonly NutritionContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly VectorStore _vectorStore;

    public ImportService(ICSVReader csvReader, NutritionContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, VectorStore vectorStore)
    {
        _csvReader = csvReader;
        _context = context;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = vectorStore;
    }

    public async Task ImportFoodDataAsync(CancellationToken cancellationToken = default)
    {
        var collection = _vectorStore.GetCollection<long, Food>("Food");
        var file = Path.Combine("Temp", "FoodData_Central_sr_legacy_food_csv_2018-04", "food.csv");
        var items = _csvReader.ReadRecords<Data.CSV.Food>(file).ToList();

        foreach (var f in items)
        {
            var food = new Food
            {
                Id = f.Id,
                Name = f.Description,
                DescriptionEmbedding = await _embeddingGenerator.GenerateVectorAsync(f.Description, cancellationToken: cancellationToken),
            };
            await collection.UpsertAsync(food, cancellationToken);
        }
    }

    public async Task ImportFoodCategoryDataAsync(CancellationToken cancellationToken = default)
    {
        var file = Path.Combine("Temp", "FoodData_Central_sr_legacy_food_csv_2018-04", "food_category.csv");
        var items = _csvReader.ReadRecords<Data.CSV.FoodCategory>(file).ToList();
        await _context.FoodCategories.AddRangeAsync(items.Select(fc => new FoodCategory
        {
            Id = fc.Id,
            Name = fc.Description,
        }), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // public async Task ImportFoodNutrientDataAsync(CancellationToken cancellationToken = default)
    // {
    //     var file = Path.Combine("Temp", "FoodData_Central_sr_legacy_food_csv_2018-04", "food_nutrient.csv");
    //     var items = _csvReader.ReadRecords<Data.CSV.FoodNutrient>(file).ToList();
    //     await _context.FoodNutrients.AddRangeAsync(items.Select(fn => new FoodNutrient
    //     {
    //         FoodId = fn.FoodId,
    //         NutrientId = fn.NutrientId,
    //         Amount = fn.Amount,
    //     }), cancellationToken);
    //     await _context.SaveChangesAsync(cancellationToken);
    // }

    // public async Task ImportNutrientDataAsync(CancellationToken cancellationToken = default)
    // {
    //     var file = Path.Combine("Temp", "FoodData_Central_sr_legacy_food_csv_2018-04", "nutrient.csv");
    //     var items = _csvReader.ReadRecords<Data.CSV.Nutrient>(file).ToList();
    //     await _context.Nutrients.AddRangeAsync(items.Select(n => new Nutrient
    //     {
    //         Id = n.Id,
    //         Name = n.Name,
    //         UnitName = n.UnitName,
    //     }), cancellationToken);
    //     await _context.SaveChangesAsync(cancellationToken);
    // }
}
