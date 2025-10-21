using CsvHelper.Configuration.Attributes;

namespace Simplz.Nutrition.Data.CSV;

public class Food
{
    [Name("fdc_id")] public long Id { get; set; }
    [Name("data_type")] public string DataType { get; set; } = string.Empty;
    [Name("description")] public string Description { get; set; } = string.Empty;
    [Name("food_category_id")] public long FoodCategoryId { get; set; }
    [Name("publication_date")] public DateTime PublicationDate { get; set; }
}
