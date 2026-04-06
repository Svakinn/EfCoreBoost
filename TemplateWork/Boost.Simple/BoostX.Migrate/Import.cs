using Microsoft.Extensions.Configuration;
using EfCore.Boost.DbRepo;
using EfCore.Boost.UOW;
using BoostX.Model;

namespace BoostX.Migrate;

public class Import
{
    public async Task ExecuteAsync(BoostXUow uow, BoostXUow createUow, string[] args)
    {
        // Placeholder for custom import operations
        // Future implementation could handle commands like 'import', 'CreateDb', 'Migrate'

        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "import";
        Console.WriteLine($"--- Starting Import: {command} ---");

        switch (command)
        {
            case "import":
                await uow.RunInTransactionAsync(async (ct) =>
                {
                    await ImportCoreAsync(uow);
                });
                break;
            default:
                Console.WriteLine($"Import command '{command}' not recognized.");
                break;
        }

        Console.WriteLine("--- Import Finished ---");
    }

    private async Task ImportCoreAsync(BoostXUow uow)
    {
        Console.WriteLine("Importing core data...");
        await ImportTableAsync<BoostXDbContext.IpInfo>(uow.IpInfos, "IpInfo.csv");
    }

    private async Task ImportTableAsync<T>(IRepo<T> repo, string fileName, int bulkSize = 1000,
        bool includeIdentities = false)
        where T : class, new()
    {
        var entityName = typeof(T).Name;
        Console.WriteLine($"Importing {entityName}s from {fileName}...");
        var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CSV", fileName);
        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"Warning: CSV file not found: {csvPath}. Skipping import for {entityName}.");
            return;
        }
        var helper = new ImportHelper<T>(repo, csvPath, bulkSize, includeIdentities);
        await helper.ImportAsync();
    }


 }

