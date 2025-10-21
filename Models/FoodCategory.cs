namespace Simplz.Nutrition.Models;

public record FoodCategory
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
