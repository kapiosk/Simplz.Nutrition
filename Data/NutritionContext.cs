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

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite(_options.DatabasePath);
}
