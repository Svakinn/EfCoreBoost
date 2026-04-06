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

        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "all";
        Console.WriteLine($"--- Starting Import: {command} ---");
        await uow.RunInTransactionAsync(async (ct) =>
        {
            switch (command)
            {
                case "import":
                    await ImportCoreAsync(uow);
                    break;
                case "CreateDb":
                    await CreateDb(createUow);
                    break;
                case "Migrate":
                    await Migrate(uow);
                    break;
                default:
                    Console.WriteLine($"Import command '{command}' not recognized.");
                    break;
            }
        });

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
        var helper = new ImportHelper<T>(repo, csvPath, bulkSize, includeIdentities);
        await helper.ImportAsync();
    }

    private async Task Migrate(BoostXUow uow)
    {
        throw new NotImplementedException();
    }

    private async Task CreateDb(BoostXUow uow)
    {
        throw new NotImplementedException();
    }

 }

