using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Simplz.Nutrition.Models;
using Simplz.Nutrition.Options;

namespace Simplz.Nutrition.Data;

public class NutritionContext : DbContext
{
    private readonly SqliteOptions _options;
    public NutritionContext(IOptions<SqliteOptions> options)
    {
        _options = options.Value;
    }

    public DbSet<Food> Foods => Set<Food>();
    public DbSet<FoodCategory> FoodCategories => Set<FoodCategory>();
    public DbSet<FoodNutrient> FoodNutrients => Set<FoodNutrient>();
    public DbSet<Nutrient> Nutrients => Set<Nutrient>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite(_options.DatabasePath);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Food>(entity =>
        {
            entity.ToTable("Food");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Name).IsRequired();
            entity.Property(f => f.FoodCategoryId).IsRequired();
            entity.Ignore(f => f.DescriptionEmbedding);
            // entity.HasOne<FoodCategory>()
            //       .WithMany()
            //       .HasForeignKey(f => f.FoodCategoryId)
            //       .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FoodCategory>(entity =>
        {
            entity.ToTable("FoodCategory");
            entity.HasKey(fc => fc.Id);
            entity.Property(fc => fc.Name).IsRequired();
        });

        modelBuilder.Entity<Nutrient>(entity =>
        {
            entity.ToTable("Nutrient");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Name).IsRequired();
            entity.Property(n => n.UnitName).IsRequired();
        });

        modelBuilder.Entity<FoodNutrient>(entity =>
        {
            entity.ToTable("FoodNutrient");
            entity.HasKey(fn => new { fn.FoodId, fn.NutrientId });
            entity.Property(fn => fn.Amount).IsRequired();
            // entity.HasOne<Food>()
            //       .WithMany()
            //       .HasForeignKey(fn => fn.FoodId)
            //       .OnDelete(DeleteBehavior.Cascade);
            // entity.HasOne<Nutrient>()
            //       .WithMany()
            //       .HasForeignKey(fn => fn.NutrientId)
            //       .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
