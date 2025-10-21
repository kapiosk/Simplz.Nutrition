using CsvHelper.Configuration;

namespace Simplz.Nutrition.Services;

public interface ICSVReader
{
    IEnumerable<string?[]> ReadRecords(string filePath);
    IEnumerable<T> ReadRecords<T>(string filePath);
}

public class CSVReader : ICSVReader
{
    public IEnumerable<string?[]> ReadRecords(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvHelper.CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        while (csv.Read())
        {
            var record = new string?[headers.Length];
            for (var i = 0; i < headers.Length; i++)
                record[i] = csv.GetField(i);
            yield return record;
        }
    }

    public IEnumerable<T> ReadRecords<T>(string filePath)
    {
        using var reader = new StreamReader(filePath);
        using var csv = new CsvHelper.CsvReader(reader, new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
        });
        foreach (var record in csv.GetRecords<T>())
        {
            yield return record;
        }
    }
}
