using Microsoft.Extensions.VectorData;

namespace Simplz.Nutrition.Models;

public record Food
{
    [VectorStoreKey]
    public long Id { get; set; }
    [VectorStoreData()]
    public string Name { get; set; } = string.Empty;
    public long? FoodCategoryId { get; set; }

    [VectorStoreVector(Dimensions: 384, DistanceFunction = DistanceFunction.CosineDistance)]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}
