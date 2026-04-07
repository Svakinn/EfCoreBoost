using System.Globalization;
using EfCore.Boost.DbRepo;

namespace BoostX.Migrate;

/// <summary>
/// Provides generic helper methods for importing data from CSV files into a repository.
/// </summary>
/// <typeparam name="T">The entity type being imported.</typeparam>
public class ImportHelper<T>(IRepo<T> repo, string csvFilePath, int bulkSize = 1000, bool includeIdentities = false)
    where T : class, new()
{
    /// <summary>
    /// Gets the absolute path for a CSV file located in the 'CSV' directory.
    /// </summary>
    /// <param name="fileName">The name of the CSV file.</param>
    /// <returns>The full path to the CSV file.</returns>
    public static string GetCsvPath(string fileName) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV", fileName);

    /// <summary>
    /// Static helper to perform a full import of a CSV file for a given repository.
    /// Handles file existence checks and logging.
    /// </summary>
    /// <param name="repo">The repository to insert data into.</param>
    /// <param name="fileName">The name of the CSV file.</param>
    /// <param name="bulkSize">Number of records per bulk insert operation.</param>
    /// <param name="includeIdentities">True to include identity column values during insertion.</param>
    public static async Task ImportAsync(IRepo<T> repo, string fileName, int bulkSize = 1000, bool includeIdentities = false)
    {
        var entityName = typeof(T).Name;
        var csvPath = GetCsvPath(fileName);
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"Warning: CSV file not found: {csvPath}. Skipping import for {entityName}.");
            return;
        }

        var helper = new ImportHelper<T>(repo, csvPath, bulkSize, includeIdentities);
        Console.WriteLine($"Importing {entityName}s from {fileName}...");
        await helper.ImportAsync();
    }

    /// <summary>
    /// Performs the import operation for the current helper instance.
    /// Reads the CSV file, parses lines into entities, and performs bulk inserts.
    /// </summary>
    private async Task ImportAsync()
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

    /// <summary>
    /// Reads and parses the first data row (after the header) from the CSV file.
    /// Useful for existence checks or data preview.
    /// </summary>
    /// <returns>The first entity from the CSV file, or null if empty or file missing.</returns>
    public async Task<T?> ReadFirstRowAsync()
    {
        if (!File.Exists(csvFilePath)) return null;
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanWrite)
            .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);
        using var reader = new StreamReader(csvFilePath);
        var header = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(header)) return null;
        var columns = header.Split(',').Select(c => c.Trim().ToLowerInvariant()).ToArray();
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) return null;
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
        return entity;
    }

    /// <summary>
    /// Simple CSV line parser that handles quoted values.
    /// </summary>
    /// <param name="line">The raw CSV line string.</param>
    /// <returns>An array of parsed values.</returns>
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

    /// <summary>
    /// Converts a string value from CSV to the target property type.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <param name="targetType">The type to convert to.</param>
    /// <returns>The converted value, or null if the input is empty.</returns>
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
