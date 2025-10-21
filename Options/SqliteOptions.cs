namespace Simplz.Nutrition.Options;

public record SqliteOptions
{
    public string DatabaseFile { get; set; } = ":memory:";
    public int EmbeddingDimensions { get; set; } = 768;
    public string DatabasePath { get => $"Data Source={Path.Join("Temp", DatabaseFile)}"; }
}
