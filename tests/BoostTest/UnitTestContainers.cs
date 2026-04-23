//
// This test suite validates our Unit of Work (UOW) implementation against three database engines: SQL Server, PostgreSQL, and MySQL.
//
// Each test uses Testcontainers to spin up an isolated Docker instance of the target DB, applies EF Core migrations, executes
// an external SQL script to create a view, and runs a basic smoke test of UOW CRUD operations against that view.
//
// We also verify that database-generated GuId-values (RowIds) are returned and populated correctly when reading from the view, including
// ensuring that RowId values are valid GUIDs.
//
// Connection strings are overridden in-memory using the project configuration
// AppSettings.json, so no on-disk config is modified during tests. This ensures a fully reproducible, read-only test configuration.
//
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using EfCore.Boost.DbRepo;
using EfCore.Boost.CFG;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using EfCore.Boost.EDM;
using BoostTest.Helpers;
using TestDb;

namespace BoostTest
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
        static UOWTestDb CreateUow(IConfiguration cfg, string name) => new(cfg, name);
        static UOWTestView CreateUowView(IConfiguration cfg, string name) => new(cfg, name);

        /// <summary>
        /// Test on Azure SQL Database, requires pre-configured access and will be skipped if not properly set up
        /// Note: We run only async smoke test since synchronous makes no sense on Azure
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task Uow_Azure_Test()
        {
            var (uow, uow2, uowV) = await PrepareAzureAccess();
            if (uow == null || uow2 == null || uowV == null)
                Assert.Inconclusive("Azure SQL access not properly configured. Skip this test!");
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeAsync(uow, uow2, uowV);
            }
        }

        [TestMethod]
        public async Task Uow_MsSql_Test()
        {
            //we use uow2 for concurrency test against uow
            var (container, uow, uow2, uowV) = await PrepareMsSqlContainer();
            await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeAsync(uow, uow2, uowV);
            }
        }

        [TestMethod]
        public async Task Uow_MsSql_Test_Synchronized()
        {
            var (container, uow, uow2, uowV) = await PrepareMsSqlContainer();
            await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeSynchronous(uow, uow2, uowV);
            }
        }

        [TestMethod]
        public async Task Uow_Postgres_Test()
        {
            var (container, uow, uow2, uowV) = await PreparePgSqlContainer();
            await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeAsync(uow, uow2, uowV);
            }
        }

        [TestMethod]
        public async Task Uow_Postgres_Test_Synchronized()
        {
            var (container, uow, uow2, uowV) = await PreparePgSqlContainer();
            await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeSynchronous(uow, uow2, uowV);
            }
        }

        /*
        [TestMethod]
        public async Task Uow_MySql_Test()
        {
            var (container, uow, uow2, uowV) = await PrepareMySqlContainer();
             await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeAsync(uow, uow2, uowV);
            }
        }

        [TestMethod]
        public async Task Uow_MySql_Test_Synchronized()
        {
            var (container, uow, uow2, uowV) = await PrepareMySqlContainer();
            await using (container)
            using (uowV)
            using (uow2)
            using (uow)
            {
                await BasicSmokeSynchronous(uow, uow2, uowV);
            }
        }
        */

        /// <summary>
        ///  Spin up temporary SQL Server in Docker
        ///  Note: we are sort fo cheeting on the usual connection string from appsettings.json
        ///  This is done by overriding the connection string in an overrides dictionary
        /// </summary>
        /// <returns></returns>
        static async Task<(MsSqlContainer Container, UOWTestDb Uow, UOWTestDb Uow2, UOWTestView UowV)> PrepareMsSqlContainer()
        {
            const string dbName = "TestDb";
            const string connName = "TestMs";

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
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql("Sql/MsSqlCreateDb.sql"));
            var uow2 = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MsSql.sql")); //Script contains own transactions, therefore, cannot run in transaction here
            var uowV = CreateUowView(cfg, connName);
            return (msBuilder, uow, uow2, uowV);
        }

        /// <summary>
        /// We have no container for Azure SQL we rly on prepared access by the tester (or skip the test otherwise)
        /// AppSettings.json determines if Azure test is run or skipped
        ///
        /// </summary>
        /// <returns></returns>
        static async Task<(UOWTestDb? Uow, UOWTestDb? Uow2, UOWTestView? UowView)> PrepareAzureAccess()
        {
            const string dbName = "TestDb";
            const string connName = "TestAzure";
            var cc = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false).Build();
            var dbTestCfg = DbConnectionCfg.Get(cc, connName);
            if (dbTestCfg == null || dbTestCfg.UseAzure == false || dbTestCfg.AzureClientSecret.Length < 2 || dbTestCfg.AzureClientSecret[..1] == "<")
                return (null, null, null); //Skip test if not properly configured, no error thrown
            if (dbTestCfg.ConnectionString.IndexOf(dbName, StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception($"Azure test DB connection string must contain database name '{dbName}'");
            var uow = CreateUow(cc, connName);
            //NOTE: you may want to create the db manually and skip this call instead
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql("Sql/AzureCreateDb.sql"));
            var uow2 = CreateUow(cc, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Sql/AzurePrepareDb.sql")); //Clean up previous runs
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MsSql.sql")); //Normal SQL-Server Migrations
            var uowV = CreateUowView(cc, connName);
            return (uow, uow2, uowV);
        }

        /*
        /// <summary>
        ///  Spin up temporary MySql Server in Docker
        ///  Note: we are sort fo cheeting on the usual connection string from appsettings.json
        ///  This is done by overriding the connection string in an overrides dictionary
        /// </summary>
        /// <returns></returns>
        static async Task<(MySqlContainer Container, UOWTestDb Uow, UOWTestDb Uow2, UOWTestView UowView)> PrepareMySqlContainer()
        {
            const string dbName = "TestDb";
            const string connName = "TestMy";
            var myBuilder = new MySqlBuilder("mysql:8.0")
                .WithUsername("root").WithPassword("root")
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
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql("Sql/MySqlCreateDb.mysql"));
            var uow2 = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MySql.mysql")); //Mysql does not handle any ddl in transactions
            var uowV = CreateUowView(cfg, connName);
            return (myBuilder, uow, uow2, uowV);
        }
        */

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
        static async Task<(PostgreSqlContainer Container, UOWTestDb Uow, UOWTestDb Uow2, UOWTestView UowView)> PreparePgSqlContainer()
        {
            const string dbName = "TestDb";
            const string connName = "TestPg";
            var pgBuilder = new PostgreSqlBuilder("postgres:16.3")
                .WithUsername("postgres").WithPassword("postgres").WithDatabase("postgres")
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
            await uowMigrate.ExecuteAdminDbSqlScriptAsync(await ReadSql("Sql/PgSqlCreateDb.pgsql"));
            Npgsql.NpgsqlConnection.ClearAllPools();
            await uowMigrate.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_PgSql.pgsql")); // Note: The migration script itself contains transactions, so we do not run in transaction here
            // Force Npgsql to refresh type mappings ("citext", etc.) for this database
            var dbConn = (Npgsql.NpgsqlConnection)uowMigrate.GetDbContext().Database.GetDbConnection();
            if (dbConn.State != ConnectionState.Open)
                await uowMigrate.GetDbContext().Database.OpenConnectionAsync();
            await dbConn.ReloadTypesAsync();
            Npgsql.NpgsqlConnection.ClearAllPools();
            var uow = CreateUow(cfg, connName);
            var uow2 = CreateUow(cfg, connName);
            var uowV = CreateUowView(cfg, connName);
            return (pgBuilder, uow, uow2, uowV);
        }

        /// <summary>
        /// Main smoke test, async routines only
        /// </summary>
        /// <param name="uow"></param>
        /// <param name="uow2"></param>
        /// <param name="uowV"></param>
        /// <returns></returns>
        static async Task BasicSmokeAsync(UOWTestDb uow, UOWTestDb uow2, UOWTestView uowV)
        {
            //
            // Test saving to a database
            //
            var myRow = await uow.MyTables.QueryTracked().FirstOrDefaultAsync();
            Assert.IsNotNull(myRow); //do we have our seeded data?
            var refRow = new DbTest.MyTableRef { MyInfo = "ref", LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "Philip" };
            myRow.MyTableRefs.Add(refRow);
            await uow.SaveChangesAsync(); //Wa can add refs to the previous row
            //
            //Test if the auto-incrementor attribute worked [AutoIncrement]:
            //
            var origRowId = myRow.RowVersion;
            var savedRef = await uow.MyTableRefs.RowTrackedAsync(tt => tt.ParentId == myRow.Id && tt.MyInfo == "ref");
            Assert.IsNotNull(savedRef, "Failed fetching tracked row, just added");
            savedRef.MyInfo = "Ref2";
            await uow.SaveChangesAsync();
            var found = await uow.MyTableRefs.RowUnTrackedAsync(tt => tt.Id == savedRef.Id);
            Assert.IsNotNull(found,"Failed fetching untracked row, just saved");
            Assert.IsGreaterThan(origRowId, found.RowVersion, "Rowversion not incremented in the saved row " + found.RowVersion);
            //
            // Test Actual Concurrency exception [AutoIncrementConcurrency] (on MyTable.RowVersion)
            //
            bool errorThrown = false;
            var myRow2 = await uow2.MyTables.QueryTracked().FirstOrDefaultAsync(tt => tt.Id == myRow.Id);
            myRow.Status += 1;
            await uow.SaveChangesAsync();
            try
            {
                myRow2!.Status += 2; //Modify myRow2 on UOW2 and then try to save after UOW has modified and saved the same row
                await uow2.SaveChangesAsync();
            }
            catch (Exception)
            {
                errorThrown = true;
            }
            Assert.IsTrue(errorThrown, "Concurrency check failed for MyTable.RowVersion");
            //
            // Test view lookup
            //
            var viewItem = await uowV.MyTableRefViews.QueryUnTracked().FirstOrDefaultAsync(tt => tt.RefId == refRow.Id);
            Assert.IsNotNull(viewItem);
            Assert.IsTrue((viewItem.RowID != Guid.Empty), "RowID should not be empty");
            //
            //Test calling SP/function for retrieving data from a sequence
            //
            var idList = await uow.GetNextSequenceIds(10);
            Assert.HasCount(10, idList, "Did not get 10 rows from sequence function");
            //
            // Bulk- insert & delete tests
            //
            var tt = new DbTest.MyTable { Id = 10, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm" };
            var tt2 = new DbTest.MyTable { Id = 11, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm2" };
            await uow.RunInTransactionAsync(async ct =>
            {
                await uow.MyTables.BulkInsertAsync([tt, tt2], true, ct);
            }, ct: CancellationToken.None);

            //
            // Testing that autogenerated sequences function as expected (should be no. 12)
            //
            uow.MyTables.Add(new DbTest.MyTable() { LastChanged = DateTime.UtcNow, LastChangedBy = "swarm", RowID = Guid.NewGuid() });
            await uow.SaveChangesAndNewAsync();
            //
            // Now delete row no. 10
            //
            await uow.MyTables.BulkDeleteByIdsAsync([10]);
            //
            // Now test your ids are ok
            //
            var currIds = await uow.MyTables.QueryUnTracked().Select(xx => xx.Id).ToListAsync();
            Assert.IsFalse(currIds.Any(xx => xx == 10), "Bulk delete failed, row 10 still exists");
            Assert.IsTrue(currIds.Any(xx => xx == 11), "Bulk inserted row not found");
            Assert.IsTrue(currIds.Any(aa => aa == 12), "Sequence not resetting after bulk-insert");
            //
            // Bulk-insert without identity, no transaction
            //
            await uow.MyTables.BulkDeleteByIdsAsync([11]);
            await uow.MyTables.BulkInsertAsync([tt, tt2]);
            var row2 = await uow.MyTables.RowByKeyUnTrackedAsync(13);
            Assert.IsNotNull(row2, "Bulk-insert without identities fail");
            //While at it test the tracked by key lookup (slightly different key handling there)
            var row3 = await uow.MyTables.RowByKeyTrackedAsync(13);
            Assert.IsNotNull(row3, "Tracked key lookup by Id, failed");
            //
            // Test scalar lookup
            //
            var fId = await uow.GetMaxIdByChanger("Stefan");
            Assert.AreEqual(-2, fId, "Scalar routine did not return valid id");
            //
            // Test transaction rollback, by insertin legal and then unlegal row that should trigger rollback of the whole transaction
            //
            var sameUniqueGuId = Guid.NewGuid();
            var rb = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
            var rb2 = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
            try
            {
                await uow.RunInTransactionAsync(async ct =>
                {
                    uow.MyTables.Add(rb);
                    await uow.SaveChangesAsync(ct);
                    var insideExists = await uow.MyTables.QueryUnTracked().AnyAsync(t => t.Id == rb.Id, cancellationToken: ct);
                    Assert.IsTrue(insideExists, "Row should be visible inside active transaction before rollback");
                    //Now add duplicate that should trigger rollback
                    uow.MyTables.Add(rb2);
                    await uow.SaveChangesAsync(ct);
                }, ct: CancellationToken.None);
            }
            catch (Exception) { }
            var afterRollbackExists = await uow.MyTables.QueryUnTracked().AnyAsync(t => t.Id == rb.Id);
            Assert.IsFalse(afterRollbackExists, "Row should not exist after rollback");
            //
            // OData Test and EDM model generation
            //
            var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=LastChangedBy eq 'Stefan'");
            var baseQuery = uow.MyTables.QueryUnTracked();
            var filteredResult = await uow.MyTables.FilterODataAsync(baseQuery, options);
            Assert.IsTrue(filteredResult.InlineCount > 0 && filteredResult.Results.All(x => x.LastChangedBy == "Stefan"), "We expect to find Stefan's, but only Stefan's");
            // Verify that data exist with linQ
            var normRow = await uow.MyTables.QueryUnTracked().Where(mm => mm.Id == -1).Include(xx => xx.MyTableRefs.Where(r => r.MyInfo == "BigData")).ToListAsync();
            Assert.IsNotEmpty(normRow);
            //
            // Expand Odata test, remember to allow expanding with policy:
            //
            var bq = uow.MyTables.QueryUnTracked();
            var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan = uow.MyTables.BuildODataQueryPlan(bq, options2, new ODataPolicy(AllowExpand: true), true);
            var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);
            Assert.AreEqual(1, plan2.Report.Count(zz => zz == "ExpandInnerFilterIgnored:MyTableRefs"), "We did not find $filter warning within AsInclude query");
            var res = await uow.MyTables.MaterializeODataAsync(plan2);
            //we received our MyTableRefs records inline (but unfiltered)
            Assert.IsTrue(res.InlineCount is > 0 && res.Results.FirstOrDefault() != null && res.Results.FirstOrDefault()!.MyTableRefs.Count > 0,
                "$expand as include failed to produce data for MyTableRefs");
            //
            // Now shaped OData tests:
            //
            var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$select=Id");
            var plan3 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowSelect: true), true);
            var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);
            var res3 = await uow.MyTables.MaterializeODataShapedAsync(plan3, shapedQuery3);
            Assert.IsNotEmpty(res3.Results, "Filtered and selected query failed");
            var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);
            Assert.Contains("\"Id\"", json, $"$select=Id expected 'Id' in shaped JSON.\nJSON: {json}");
            Assert.DoesNotContain("LastChangedBy", json, $"$select=Id should not include 'LastChangedBy'.\nJSON: {json}");
            Assert.DoesNotContain("MyTableRefs", json, $"$select=Id should not include navigation 'MyTableRefs'.\nJSON: {json}");
            //Inner filter test for shaped expansion
            var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan4 = uow.MyTables.BuildODataQueryPlan(bq, opts4, new ODataPolicy(AllowExpand: true), true);
            var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
            var res4 = await uow.MyTables.MaterializeODataShapedAsync(plan4, shapedQuery4);
            Assert.IsNotEmpty(res4.Results, "Expected at least one result from $filter=Id eq -1 with expanded MyTableRefs, but none were returned.");
            //
            // Test UOW EDM generation
            //
            var xmlUow = EdmBuilder.BuildXmlModelFromUow(uow);
            Assert.Contains("<EntitySet Name=\"MyTables\"", xmlUow, "XML model for UOW should contain EntitySet name MyTables");
            Assert.Contains("<EntitySet Name=\"MyTableRefs\"", xmlUow, "XML model for UOW should contain EntitySet name MyTableRefs");
            Assert.Contains("<EntityType Name=\"MyTable\"", xmlUow, "XML model for UOW should contain EntityType name MyTable");
            Assert.Contains("<EntityType Name=\"MyTableRef\"", xmlUow, "XML model for UOW should contain EntityType name MyTableRef");

            // Test generic UOW EDM generation (new simplified API)
            var xmlUowGeneric = EdmBuilder.BuildXmlModelFromUow<UOWTestDb>();
            Assert.Contains("<EntitySet Name=\"MyTables\"", xmlUowGeneric);

            // Test custom EDM configuration (functions/actions)
            var xmlCustom = EdmBuilder.BuildXmlModelFromUow(uow, builder => {
                builder.EntityType<DbTest.MyTable>().Collection.Function("MyCustomFunction").Returns<string>();
            });
            Assert.Contains("<Function Name=\"MyCustomFunction\"", xmlCustom, "XML model should contain custom function");
            //
            // Test GetModel() and Metadata() from UOW
            //
            var xmlFromMetadata = uow.Metadata();
            Assert.Contains("<EntitySet Name=\"MyTables\"", xmlFromMetadata, "Metadata() from UOW should contain MyTables");
            Assert.Contains("<EntitySet Name=\"MyTableRefs\"", xmlFromMetadata, "Metadata() from UOW should contain MyTableRefs");
            // In UOWTestDb, MyTableRefViews is commented out or missing, but let's check what's NOT there
            Assert.IsFalse(xmlFromMetadata.Contains("<EntitySet Name=\"MyTableRefViews\""), "Metadata() from UOW should NOT contain MyTableRefViews as it's not exposed in UOWTestDb");
            var modelFromGetModel = uow.GetModel();
            Assert.IsNotNull(modelFromGetModel.EntityContainer.FindEntitySet("MyTables"), "GetModel() should contain MyTables");
            Assert.IsNotNull(modelFromGetModel.EntityContainer.FindEntitySet("MyTableRefs"), "GetModel() should contain MyTableRefs");
            Assert.IsNull(modelFromGetModel.EntityContainer.FindEntitySet("MyTableRefViews"), "GetModel() should NOT contain MyTableRefViews");
       }

        /// <summary>
        /// Just a part of what we do for async, no need to repeat all tests
        /// </summary>
        /// <param name="uow"></param>
        /// <param name="uow2"></param>
        /// <param name="uowV"></param>
        static Task BasicSmokeSynchronous(UOWTestDb uow, UOWTestDb uow2, UOWTestView uowV)
        {
            //
            // Test saving to a database
            //
            var myRow = uow.MyTables.QueryTracked().FirstOrDefault();
            Assert.IsNotNull(myRow); //do we have our seeded data?
            var refRow = new DbTest.MyTableRef { MyInfo = "ref", LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "Philip" };
            myRow.MyTableRefs.Add(refRow);
            uow.SaveChangesSynchronized(); //Wa can add refs to the previous row
            //
            // Test if the auto-incrementor attribute worked [AutoIncrement]:
            //
            var origRowId = myRow.RowVersion;
            var savedRef = uow.MyTableRefs.RowTrackedSynchronized(tt => tt.ParentId == myRow.Id && tt.MyInfo == "ref");
            Assert.IsNotNull(savedRef, "Failed fetching tracked row, just added");
            savedRef.MyInfo = "Ref2";
            uow.SaveChangesSynchronized();
            var found = uow.MyTableRefs.RowUntTackedSynchronized(tt => tt.Id == savedRef.Id);
            Assert.IsNotNull(found,"Failed fetching untracked row, just saved");
            Assert.IsGreaterThan(origRowId, found.RowVersion, "Rowversion not incremented in the saved row " + found.RowVersion);
            //
            // Test Actual Concurrency exception [AutoIncrementConcurrency] (on MyTable.RowVersion)
            //
            bool errorThrown = false;
            var myRow2 = uow2.MyTables.QueryTracked().FirstOrDefault(tt => tt.Id == myRow.Id);
            myRow.Status += 1;
            uow.SaveChangesSynchronized();
            try
            {
                myRow2!.Status += 2; //Modify myRow2 on UOW2 and then try to save after UOW has modified and saved the same row
                uow2.SaveChangesSynchronized();
            }
            catch (Exception)
            {
                errorThrown = true;
            }
            Assert.IsTrue(errorThrown, "Concurrency check failed for MyTable.RowVersion");
            //
            // Test view lookup
            //
            var viewItem = uowV.MyTableRefViews.QueryUnTracked().FirstOrDefault(tt => tt.RefId == refRow.Id);
            Assert.IsNotNull(viewItem);
            Assert.IsTrue((viewItem.RowID != Guid.Empty), "RowID should not be empty");
            //
            //Test calling SP/function for retrieving data from a sequence
            //
            var idList = uow.GetNextSequenceIdsSynchronized(10);
            Assert.HasCount(10, idList, "Did not get 10 rows from sequence function");
            //
            //Bulk- insert & delete tests
            //
            var tt = new DbTest.MyTable { Id = 10, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm", RowID = Guid.NewGuid() };
            var tt2 = new DbTest.MyTable { Id = 11, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm2", RowID = Guid.NewGuid() };
            uow.RunInTransactionSynchronized(() =>
            {
                uow.MyTables.BulkInsertSynchronized([tt, tt2], true);
            });
            //
            // Testing that autogenerated sequences function as expected (should be no. 12)
            //
            uow.MyTables.Add(new DbTest.MyTable() { LastChanged = DateTime.UtcNow, LastChangedBy = "swarm", RowID = Guid.NewGuid() });
            uow.SaveChangesAndNewSynchronized();
            //
            //Now delete row no. 10
            //
            uow.MyTables.BulkDeleteByIdsSynchronized([10]);
            //
            // Now test your ids are ok
            //
            var currIds = uow.MyTables.QueryUnTracked().Select(xx => xx.Id).ToList();
            Assert.IsFalse(currIds.Any(xx => xx == 10), "Bulk delete failed, row 10 still exists");
            Assert.IsTrue(currIds.Any(xx => xx == 11), "Bulk inserted row not found");
            Assert.IsTrue(currIds.Any(xx => xx == 12), "Sequence not resetting after bulk-insert");
            //
            //Bulk-insert without identity, no transaction
            //
            uow.MyTables.BulkDeleteByIdsSynchronized([11]);
            uow.MyTables.BulkInsertSynchronized([tt, tt2]);
            var row2 = uow.MyTables.RowByKeyUnTrackedSynchronized(13);
            Assert.IsNotNull(row2, "Bulk-insert without identities fail");
            //While at it test the tracked by key lookup (slightly different key handling there)
            var row3 = uow.MyTables.RowByKeyTrackedSynchronized(13);
            Assert.IsNotNull(row3, "Tracked key lookup by Id, failed");
            //
            // Test scalar lookup
            //
            var fId = uow.GetMaxIdByChangerSynchronized("Stefan");
            Assert.AreEqual(-2, fId, "Scalar routine did not return valid id");
            //
            // Test transaction rollback, by insertin legal and then illegal row that should trigger rollback of the whole transaction
            //
            var sameUniqueGuId = Guid.NewGuid();
            var rb = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
            var rb2 = new DbTest.MyTable { LastChanged = DateTime.UtcNow, LastChangedBy = "rollback", RowID = sameUniqueGuId };
            try
            {
                uow.RunInTransactionSynchronized(() =>
                {
                    uow.MyTables.Add(rb);
                    uow.SaveChangesSynchronized();
                    var insideExists = uow.MyTables.QueryUnTracked().Any(t => t.Id == rb.Id);
                    Assert.IsTrue(insideExists, "Row should be visible inside active transaction before rollback");
                    //Now add duplicate that should trigger rollback
                    uow.MyTables.Add(rb2);
                    uow.SaveChangesSynchronized();
                });
            }
            catch (Exception) { }
            var afterRollbackExists = uow.MyTables.QueryUnTracked().Any(t => t.Id == rb.Id);
            Assert.IsFalse(afterRollbackExists, "Row should not exist after rollback");
            //
            // Odata Test and EDM model generation
            //
            var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=LastChangedBy eq 'Stefan'");
            var baseQuery = uow.MyTables.QueryUnTracked();
            var filteredResults = uow.MyTables.FilterODataSynchronized(baseQuery, options);
            Assert.IsTrue(filteredResults.InlineCount > 0 && filteredResults.Results.All(x => x.LastChangedBy == "Stefan"), "We expect to find Stefan's, but only Stefan's");
            // Verify that data exist with linQ
            var normRow = uow.MyTables.QueryUnTracked().Where(xx => xx.Id == -1).Include(zz => zz.MyTableRefs.Where(r => r.MyInfo == "BigData")).ToList();
            Assert.IsNotEmpty(normRow, "Include failed");
            //
            // Expand test, remember to allow expanding with policy:
            //
            var bq = uow.MyTables.QueryUnTracked();
            var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan = uow.MyTables.BuildODataQueryPlan(bq, options2, new ODataPolicy(AllowExpand: true), true);
            var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);
            Assert.AreEqual(1, plan2.Report.Count(xx => xx == "ExpandInnerFilterIgnored:MyTableRefs"), "We did not find $filter warning within AsInclude query");
            var res = uow.MyTables.MaterializeODataSynchronized(plan2);
            //we received our MyTableRefs records inline (but unfiltered)
            Assert.IsTrue(res.InlineCount is > 0 && res.Results.FirstOrDefault() != null && res.Results.FirstOrDefault()!.MyTableRefs.Count > 0,
                "$expand as include failed to produce data for MyTableRefs");
            //
            //Now shaped tests:
            //
            var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$select=Id");
            var plan3 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowSelect: true), true);
            var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);
            var res3 = uow.MyTables.MaterializeODataShapedSynchronized(plan3, shapedQuery3);
            Assert.IsNotEmpty(res3.Results, "Filtered and selected query failed");
            var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);
            Assert.Contains("\"Id\"", json, $"$select=Id expected 'Id' in shaped JSON.\nJSON: {json}");
            Assert.DoesNotContain("LastChangedBy", json, $"$select=Id should not include 'LastChangedBy'.\nJSON: {json}");
            Assert.DoesNotContain("MyTableRefs", json, $"$select=Id should not include navigation 'MyTableRefs'.\nJSON: {json}");
            //
            // Inner filter test for shaped expansion
            //
            var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan4 = uow.MyTables.BuildODataQueryPlan(bq, opts4, new ODataPolicy(AllowExpand: true), true);
            var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
            var res4 = uow.MyTables.MaterializeODataShapedSynchronized(plan4, shapedQuery4);
            Assert.IsNotEmpty(res4.Results, "Expected at least one result from $filter=Id eq -1 with expanded MyTableRefs, but none were returned.");
            return Task.CompletedTask;
        }
    }
}
