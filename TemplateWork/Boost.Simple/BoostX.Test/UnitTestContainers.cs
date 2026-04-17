//
// This test suite validates our Unit of Work (UOW) implementation against three database engines: SQL Server, PostgreSQL, and MySQL.
//
// Each test uses Testcontainers to spin up an isolated Docker instance of the target DB, applies EF Core migrations, executes
// an external SQL script to create a view, and runs a basic smoke test of UOW CRUD operations against that view.
//
// Connection strings are overridden in-memory using the project configuration AppSettings.json, so no on-disk config is modified during tests.
// This ensures a fully reproducible, read-only test configuration.
//
// Deployment SQL is borrowed from the BoostX.Migrate project
//
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using EfCore.Boost.CFG;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using BoostX.Test.Helpers;
using BoostX.Model;

namespace BoostX.Test
{
    [TestClass]
    public class BoostTestContainers
    {
        /// <summary>
        /// Builds configuration from the base AppSettings.json + in-memory overrides for test DB connection
        /// </summary>
        /// <param name="overrides"></param>
        /// <returns></returns>
        static IConfiguration BuildConfig(Dictionary<string, string?> overrides)
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return new ConfigurationBuilder().SetBasePath(basePath).AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false).AddInMemoryCollection(overrides).Build();
        }

        /// <summary>
        /// Creates our UOWTestDb instance for the given config and connection name
        /// </summary>
        /// <param name="cfg"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        static BoostXUow CreateUow(IConfiguration cfg, string name) => new(cfg, name);

        /// <summary>
        /// Test on Azure SQL Database, requires pre-configured access and will be skipped if not properly set up
        /// Note: We run only async smoke test since synchronous makes no sense on Azure
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Uow_Azure_Test()
        {
            var uow = await PrepareAzureAccess();
            if (uow == null )
                Assert.Inconclusive("Azure SQL access not properly configured. Skip this test!");
            using (uow)
            {
                await BasicSmokeAsync(uow);
            }
        }

        [TestMethod]
        public async Task Uow_MsSql_Test()
        {
            //we use uow2 for concurrency test against uow
            var (container, uow) = await PrepareMsSqlContainer();
            await using (container)
            using (uow)
            {
                await BasicSmokeAsync(uow);
            }
        }

        [TestMethod]
        public async Task Uow_Postgres_Test()
        {
            var (container, uow) = await PreparePgSqlContainer();
            await using (container)
            using (uow)
            {
                await BasicSmokeAsync(uow);
            }
        }

        [TestMethod]
        public async Task Uow_MySql_Test()
        {
            var (container, uow) = await PrepareMySqlContainer();
             await using (container)
            using (uow)
            {
                await BasicSmokeAsync(uow);
            }
        }

        static async Task ImportAsync(BoostXUow uow)
        {
            Console.WriteLine("--- Starting Import ---");
            await uow.RunInTransactionAsync(async (ct) =>
            {
                Console.WriteLine("Importing core data...");
                // Manual check for IpInfo (programmer decides how to check existence)
                var fileName = "IpInfo.csv";
                var csvPath = ImportHelper<BoostCTX.IpInfo>.GetCsvPath(fileName);
                if (File.Exists(csvPath))
                {
                    var helper = new ImportHelper<BoostCTX.IpInfo>(uow.IpInfos, csvPath);
                    var firstRow = await helper.ReadFirstRowAsync();
                    // Since we import with identities, we can use the ID to check if import was already done.
                    // Otherwise, some other unique condition would have been needed.
                    if (firstRow != null && await uow.IpInfos.RowByIdUnTrackedAsync(firstRow.Id) != null)
                        Console.WriteLine($"IpInfo data already exists (found ID {firstRow.Id}). Skipping import.");
                    else
                        await ImportHelper<BoostCTX.IpInfo>.ImportAsync(uow.IpInfos, fileName, 1000, true);
                }
                else
                    Console.WriteLine($"Warning: CSV file not found: {csvPath}. Skipping import for IpInfo.");
            });
            Console.WriteLine("--- Import Finished ---");
        }

        /// <summary>
        ///  Spin up temporary SQL Server in Docker
        ///  Note: we are sort of cheating on the usual connection string from appsettings.json
        ///  This is done by overriding the connection string in an overrides dictionary
        /// </summary>
        /// <returns></returns>
        static async Task<(MsSqlContainer Container, BoostXUow Uow)> PrepareMsSqlContainer()
        {
            const string dbName = "BoostXDb";
            const string connName = "BoostXMs";
            var msBuilder = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").WithPassword("MyPassword123!").Build();
            await msBuilder.StartAsync();
            var newConnString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(msBuilder.GetConnectionString()) { InitialCatalog = dbName };
            var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                [$"DBConnections:{connName}:ConnectionString"] = newConnString.ToString(),
                [$"DBConnections:{connName}:Provider"] = "SqlServer"
            };
            var cfg = BuildConfig(overrides);
            var uow = CreateUow(cfg, connName);

            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL/MsSqlCreateDb.sql")));
            await uow.ExecSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations/DbDeploy_MsSql.sql")));
            await ImportAsync(uow); //Import data from CSV files
            return (msBuilder, uow);
        }

        /// <summary>
        /// We have no container for Azure SQL we rly on prepared access by the tester (or skip the test otherwise)
        /// AppSettings.json determines if Azure test is run or skipped
        ///
        /// </summary>
        /// <returns></returns>
        static async Task<BoostXUow?> PrepareAzureAccess()
        {
            const string dbName = "BoostXDb";
            const string connName = "BoostXAzure";
            var cc = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false).Build();
            var dbTestCfg = DbConnectionCfg.Get(cc, connName);
            if (dbTestCfg == null || dbTestCfg.UseAzure == false || dbTestCfg.AzureClientSecret.Length < 2 || dbTestCfg.AzureClientSecret[..1] == "<")
                return null; //Skip test if not properly configured, no error thrown
            if (dbTestCfg.ConnectionString.IndexOf(dbName, StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception($"Azure test DB connection string must contain database name '{dbName}'");
            var uow = CreateUow(cc, connName);
            // OK, Azure is not like text-containers unless you drop the database after each test (perhaps not the best idea though to do so ;) ).
            // So this is perhaps not the way to do repeated tests.
            // You could run this once and then comment out the scripting, before further tests on Azure.
            // Perhaps daily testing should skip Azure all together.
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL/AzureCreateDb.sql")));
            await uow.ExecSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations/DbDeploy_MsSql.sql")));
            await ImportAsync(uow); //Import data from CSV files
            return (uow);
        }

        /// <summary>
        ///  Spin up temporary MySql Server in Docker
        ///  Note: we are sort of cheating on the usual connection string from appsettings.json
        ///  This is done by overriding the connection string in an overrides dictionary
        /// </summary>
        /// <returns></returns>
        static async Task<(MySqlContainer Container, BoostXUow Uow)> PrepareMySqlContainer()
        {
            const string dbName = "BoostXDb";
            const string connName = "BoostXMy";
            var myBuilder = new MySqlBuilder("mysql:8.0")
                .WithUsername("root")
                .WithPassword("root")
                .WithCommand("--default-authentication-plugin=mysql_native_password")
                .Build();
            await myBuilder.StartAsync();
            var newConnString = myBuilder.GetConnectionString();
            var connBuilder = new MySqlConnector.MySqlConnectionStringBuilder(newConnString) { Database = dbName };
            var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                [$"DBConnections:{connName}:ConnectionString"] = connBuilder.ToString(),
                [$"DBConnections:{connName}:Provider"] = "MySql"
            };
            var cfg = BuildConfig(overrides);
            var uow = CreateUow(cfg, connName);
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL/MySqlCreateDb.mysql")));
            await uow.ExecSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations/DbDeploy_MySql.mysql")));
            await ImportAsync(uow); //Import data from CSV files
            return (myBuilder, uow);
        }

        static async Task<string> ReadSql(string fileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir,  fileName);
            Assert.IsTrue(File.Exists(path), $"SQL file not found: {path}");
            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        ///  Spin up a temporary Postgres Server in Docker
        /// </summary>
        /// <returns></returns>
        static async Task<(PostgreSqlContainer Container, BoostXUow Uow)> PreparePgSqlContainer()
        {
            const string dbName = "BoostXDb";
            const string connName = "BoostXPg";
            var pgBuilder = new PostgreSqlBuilder("postgres:16.3")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithDatabase("postgres")
                .Build();
            await pgBuilder.StartAsync();
            var newConnString = pgBuilder.GetConnectionString();
            var newConnBuilder = new Npgsql.NpgsqlConnectionStringBuilder(newConnString) { Database = dbName };
            var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                // create/admin (postgres)
                [$"DBConnections:{connName}:ConnectionString"] = newConnBuilder.ToString(),
                [$"DBConnections:{connName}:Provider"] = "PostgreSql"
            };
            var cfg = BuildConfig(overrides);
            var uowMigrate = CreateUow(cfg, connName);
            await uowMigrate.ExecuteAdminDbSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SQL/PgSqlCreateDb.pgsql")));
            Npgsql.NpgsqlConnection.ClearAllPools();
            await uowMigrate.ExecSqlScriptAsync(await ReadSql(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Migrations/DbDeploy_PgSql.pgsql")));
            // Force Npgsql to refresh type mappings ("citext", etc.) for this database
            var dbConn = (Npgsql.NpgsqlConnection)uowMigrate.GetDbContext().Database.GetDbConnection();
            if (dbConn.State != ConnectionState.Open)
                await uowMigrate.GetDbContext().Database.OpenConnectionAsync();
            await dbConn.ReloadTypesAsync();
            Npgsql.NpgsqlConnection.ClearAllPools();
            var uow = CreateUow(cfg, connName);
            await ImportAsync(uow); //Import data from CSV files
            return (pgBuilder, uow);
        }

        /// <summary>
        /// Main smoke test, async routines only
        /// </summary>
        /// <param name="uow"></param>
        /// <returns></returns>
        static async Task BasicSmokeAsync(BoostXUow uow)
        {
            //
            // Test saving to a database
            //
            var myRow = await uow.IpInfos.QueryTracked().FirstOrDefaultAsync();
            Assert.IsNotNull(myRow); //do we have our seeded data?
            var nRow = new BoostCTX.IpInfo() { IpNo = "129.272.223.13", HostName = "The Grand Hoster", Processed = true, LastChangedUtc = DateTimeOffset.UtcNow};
            uow.IpInfos.Add(nRow);
            await uow.SaveChangesAsync(); //Wa can add refs to the previous row
            //
            // Test view lookup
            //
            var viewItem = await uow.IpInfoViews.QueryUnTracked().FirstOrDefaultAsync(tt => tt.Id == -1);
            Assert.IsNotNull(viewItem);
            //
            //Test calling SP/function for retrieving data from a sequence
            //
            var id = await uow.GetIpId("127.0.3.3");
            Assert.IsNotNull(id, "GetIpId failed");
            //
            // Bulk- insert & delete tests
            //
            var tt = new BoostCTX.IpInfo {  LastChangedUtc = DateTimeOffset.UtcNow, HostName = "Host one", Processed = true, IpNo = "127.33.3.3" };
            var tt2 = new BoostCTX.IpInfo { LastChangedUtc = DateTimeOffset.UtcNow, HostName = "Second host", Processed = true, IpNo = "130.242.226.133"};
            await uow.RunInTransactionAsync(async ct =>
            {
                await uow.IpInfos.BulkInsertAsync([tt, tt2], false, ct);
            }, ct: CancellationToken.None);
            //
            // OData Test and EDM model generation
            //
            var options = OdataTestHelper.CreateOptions<BoostCTX.IpInfoView>(uow, "$filter=Id lt 0");
            var baseQuery = uow.IpInfoViews.QueryUnTracked();
            var filteredResult = await uow.IpInfoViews.FilterODataAsync(baseQuery, options);
            Assert.IsTrue(filteredResult.InlineCount > 0 && filteredResult.Results.All(x => x.Id < 0), "We expect to negative id");
            // Verify that data exist with linQ
            var normRow = await uow.IpInfoViews.QueryUnTracked().Where(mm => mm.Id < 0).ToListAsync();
            Assert.IsNotEmpty(normRow);
        }
    }
}
