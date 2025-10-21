namespace Simplz.Nutrition.Models;

public record Nutrient
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}