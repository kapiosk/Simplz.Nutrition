namespace Simplz.Nutrition.Models;

public record FoodNutrient
{
    public long FoodId { get; set; }
    public long NutrientId { get; set; }
    public double Amount { get; set; }
}
