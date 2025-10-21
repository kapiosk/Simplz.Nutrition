using CsvHelper.Configuration.Attributes;

namespace Simplz.Nutrition.Data.CSV;
//"id","code","description"
public class FoodCategory
{
    [Name("id")] public long Id { get; set; }
    [Name("code")] public string Code { get; set; } = string.Empty;
    [Name("description")] public string Description { get; set; } = string.Empty;
}
