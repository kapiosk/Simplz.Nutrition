namespace Simplz.Nutrition.Options;

public record SqliteOptions
{
    public string DatabaseFile { get; set; } = ":memory:";
    public int EmbeddingDimensions { get; set; }
    public string DatabasePath { get => $"Data Source={Path.Join("Temp", DatabaseFile)}"; }
}
