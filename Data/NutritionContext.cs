using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Simplz.Nutrition.Options;

namespace Simplz.Nutrition.Data;

public class NutritionContext : DbContext
{
    private readonly SqliteOptions _options;
    public NutritionContext(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    public DbSet<Models.Food> Foods => Set<Models.Food>();
    public DbSet<Models.FoodCategory> FoodCategories => Set<Models.FoodCategory>();
    public DbSet<Models.FoodNutrient> FoodNutrients => Set<Models.FoodNutrient>();
    public DbSet<Models.Nutrient> Nutrients => Set<Models.Nutrient>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite(_options.DatabasePath);
}
