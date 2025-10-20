using Microsoft.Extensions.VectorData;

namespace Simplz.Nutrition.Models;

public record Food
{
    [VectorStoreKey]
    public long Id { get; set; }
    [VectorStoreData(StorageName = "hotel_name")]
    public string Name { get; set; } = string.Empty;
    public long FoodCategoryId { get; set; }

    [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}

public record FoodCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public record FoodNutrient
{
    public long FoodId { get; set; }
    public long NutrientId { get; set; }
    public double Amount { get; set; }
}

public record Nutrient
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}