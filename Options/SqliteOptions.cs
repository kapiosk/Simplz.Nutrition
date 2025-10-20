namespace Simplz.Nutrition.Options;

public record SqliteOptions
{
    public string DatabasePath { get; set; } = "data/food.db";
    public int EmbeddingDimensions { get; set; } = 768;
}
