using System.Globalization;
using EfCore.Boost.DbRepo;

namespace BoostX.Migrate;

public class ImportHelper<T>(IRepo<T> repo, string csvFilePath, int bulkSize = 1000, bool includeIdentities = false)
    where T : class, new()
{
    public async Task ImportAsync()
    {
        if (!File.Exists(csvFilePath)) throw new FileNotFoundException("CSV file not found", csvFilePath);
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);
        using var reader = new StreamReader(csvFilePath);
        var header = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(header)) return;
        var columns = header.Split(',').Select(c => c.Trim().ToLowerInvariant()).ToArray();
        var chunk = new List<T>(bulkSize);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var values = ParseCsvLine(line);
            var entity = new T();
            for (var i = 0; i < columns.Length && i < values.Length; i++)
            {
                if (properties.TryGetValue(columns[i], out var prop))
                {
                    var value = ConvertValue(values[i], prop.PropertyType);
                    prop.SetValue(entity, value);
                }
            }
            chunk.Add(entity);
            if (chunk.Count >= bulkSize)
            {
                await repo.BulkInsertAsync(chunk, includeIdentities);
                chunk.Clear();
            }
        }
        if (chunk.Count > 0)
            await repo.BulkInsertAsync(chunk, includeIdentities);
    }

    private string[] ParseCsvLine(string line)
    {
        // Simple CSV parser that handles quotes
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (var c in line)
        {
            if (c == '\"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
                current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result.ToArray();
    }

    private object? ConvertValue(string value, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlyingType == typeof(string)) return value;
        if (underlyingType == typeof(int)) return int.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(long)) return long.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(decimal)) return decimal.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(double)) return double.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(bool)) return bool.Parse(value);
        if (underlyingType == typeof(DateTime)) return DateTime.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
        if (underlyingType == typeof(Guid)) return Guid.Parse(value);
        return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
    }
}
