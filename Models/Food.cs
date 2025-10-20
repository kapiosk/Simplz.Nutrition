using Microsoft.Extensions.VectorData;

namespace Simplz.Nutrition.Models;

public record Food
{
    [VectorStoreKey]
    public ulong Id { get; set; }
    [VectorStoreData(StorageName = "hotel_name")]
    public string Name { get; set; } = string.Empty;
    public ulong FoodCategoryId { get; set; }

    [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}

public record FoodCategory
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public record FoodNutrient
{
    public ulong FoodId { get; set; }
    public ulong NutrientId { get; set; }
    public double Amount { get; set; }
}

public record Nutrient
{
    public ulong Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}