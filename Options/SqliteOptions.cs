namespace Simplz.Nutrition.Options;

public record SqliteOptions
{
    public string DatabasePath { get; set; } = "data/food.db";

    public string? SqliteVecPath { get; set; }

    public int EmbeddingDimensions { get; set; } = 384;
}
