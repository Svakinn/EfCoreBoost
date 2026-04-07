using BoostX.Model;

namespace BoostX.Migrate;

/// <summary>
/// Orchestrates the data import process, providing a higher-level service for Program.cs.
/// Here is where you would add additional import steps for other tables.
/// </summary>
public static class ImportService
{
    /// <summary>
    /// Executes the full data import workflow within a transaction.
    /// </summary>
    /// <param name="uow">The Unit of Work providing access to repositories.</param>
    public static async Task ExecuteAsync(BoostXUow uow)
    {
        Console.WriteLine("--- Starting Import ---");

        await uow.RunInTransactionAsync(async (ct) =>
        {
            Console.WriteLine("Importing core data...");

            // Manual check for IpInfo (programmer decides how to check existence)
            var fileName = "IpInfo.csv";
            var csvPath = ImportHelper<BoostXDbContext.IpInfo>.GetCsvPath(fileName);
            if (File.Exists(csvPath))
            {
                var helper = new ImportHelper<BoostXDbContext.IpInfo>(uow.IpInfos, csvPath);
                var firstRow = await helper.ReadFirstRowAsync();
                if (firstRow != null && await uow.IpInfos.RowByIdUnTrackedAsync(firstRow.Id) != null)
                {
                    Console.WriteLine($"IpInfo data already exists (found ID {firstRow.Id}). Skipping import.");
                }
                else
                {
                    await ImportHelper<BoostXDbContext.IpInfo>.ImportAsync(uow.IpInfos, fileName);
                }
            }
            else
            {
                Console.WriteLine($"Warning: CSV file not found: {csvPath}. Skipping import for IpInfo.");
            }
        });

        Console.WriteLine("--- Import Finished ---");
    }
}
