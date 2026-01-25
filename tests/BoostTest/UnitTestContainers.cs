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
// (AppSettings.json) so no on-disk config is modified during tests. This ensures fully reproducible, read-only test configuration.
//


using BoostTest.Helpers;
using BoostTest.TestDb;
using EfCore.Boost;
using EfCore.Boost.CFG;
using Microsoft;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OData.Edm;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Testcontainers.MsSql;
using Testcontainers.MySql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BoostTest
{
    /// <summary>
    /// 
    /// </summary>
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

        /// <summary>
        /// Test on Azure SQL Database, requires pre-configured access and will be skipped if not properly set up
        /// Note: We run only async smoke test since synchronous makes no sense on Azure
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Uow_Azure_Test()
        {
            try
            {
                var uow = await PrepareAzureAccess();
                using (uow)
                {
                    if (uow == null)
                    {
                        Console.WriteLine("Azure SQL access not properly configured, skipping test !");
                        return;
                    }
                    await BasicSmokeAsync(uow);
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }


        [Fact]
        public async Task Uow_MsSql_Test()
        {
            try
            {
                var (container, uow) = await PrepareMsSqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeAsync(uow);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        [Fact]
        public async Task Uow_MsSql_Test_Synchronized()
        {
            try
            {
                var (container, uow) = await PrepareMsSqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeSynchronous(uow);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        [Fact]
        public async Task Uow_Postgres_Test()
        {
            try
            {
                var (container, uow) = await PreparePgSqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeAsync(uow);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        [Fact]
        public async Task Uow_Postgres_Test_Synchronized()
        {
            try
            {
                var (container, uow) = await PreparePgSqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeSynchronous(uow);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        [Fact]
        public async Task Uow_MySql_Test()
        {
            try
            {
                var (container, uow) = await PrepareMySqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeAsync(uow);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        [Fact]
        public async Task Uow_MySql_Test_Synchronized()
        {
            try
            {
                var (container, uow) = await PrepareMySqlContainer();
                await using (container)
                using (uow)
                {
                    await BasicSmokeSynchronous(uow);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message.ToString());
                throw;
            }
        }

        /// <summary>
        ///  Spin up temporary SQL Server in Docker
        /// </summary>
        /// <returns></returns>
        static async Task<(MsSqlContainer Container, UOWTestDb Uow)> PrepareMsSqlContainer()
        {
            const string dbName = "TestDb";
            const string connNameCreate = "TestMsCreate";
            const string connName = "TestMs";
            var sql = new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").WithPassword("MyPassword123!").Build();
            await sql.StartAsync();
            var adminCs = sql.GetConnectionString();
            var dbCs = adminCs + $";Database={dbName}";
            var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                [$"DBConnections:{connNameCreate}:ConnectionString"] = adminCs,
                [$"DBConnections:{connNameCreate}:Provider"] = "SqlServer",
                [$"DBConnections:{connName}:ConnectionString"] = dbCs,
                [$"DBConnections:{connName}:Provider"] = "SqlServer"
            };
            var cfg = BuildConfig(overrides);
            var uowCreate = CreateUow(cfg, connNameCreate);
            await uowCreate.ExecSqlScriptAsync(await ReadSql("MsSqlCreateDb.sql"), false);
            var uow = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MsSql.sql"), false); //Script contains own transactions, therefore cannot run in transaction here
            return (sql, uow);
        }

        /// <summary>
        /// We have no container for Azure SQL we rly on prepared access by the tester (or skip the test otherwise)
        /// AppSettings.json determines if Azure test is run or skipped
        /// 
        /// </summary>
        /// <returns></returns>
        static async Task<UOWTestDb?> PrepareAzureAccess()
        {
            const string dbName = "TestDb";
            const string connName = "TestAzure";
            var cc = new ConfigurationBuilder().SetBasePath(AppDomain.CurrentDomain.BaseDirectory).AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false).Build();
            var dbTestCfg = DbConnectionCFG.Get(cc, connName);
            if ( dbTestCfg == null || dbTestCfg.UseAzure == false || dbTestCfg.AzureClientSecret.Length < 2 || dbTestCfg.AzureClientSecret[..1] == "<" )
                return null; //Skip test if not properly configured, no error thrown
            if (dbTestCfg.ConnectionString.IndexOf(dbName, StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception($"Azure test DB connection string must contain database name '{dbName}'");
            var uow = CreateUow(cc, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("AzurePrepareDb.sql"), false); //Cleanup previous runs
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MsSql.sql"), false); //Normal SQL-Server Migrations
            return uow;
        }

        /// <summary>
        ///  Spin up temporary MySql Server in Docker
        /// </summary>
        /// <returns></returns>
        static async Task<(MySqlContainer Container, UOWTestDb Uow)> PrepareMySqlContainer()
        {
            const string dbName = "TestDb";
            const string connNameCreate = "TestMyCreate";
            const string connName = "TestMy";
            var my = new MySqlBuilder()
                .WithImage("mysql:8.0")
                .WithUsername("root")
                .WithPassword("root")
                .WithCommand("--default-authentication-plugin=mysql_native_password")
                .Build();
            await my.StartAsync();
            var adminCs = my.GetConnectionString();
            var adminBuilder = new MySqlConnector.MySqlConnectionStringBuilder(adminCs) { Database = dbName };
            var dbCs = adminBuilder.ToString();
            var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                // create/admin
                [$"DBConnections:{connNameCreate}:ConnectionString"] = adminCs,
                [$"DBConnections:{connNameCreate}:Provider"] = "MySql",
                // normal/app
                [$"DBConnections:{connName}:ConnectionString"] = dbCs,
                [$"DBConnections:{connName}:Provider"] = "MySql"
            };
            var cfg = BuildConfig(overrides);
            var uowCreate = CreateUow(cfg, connNameCreate);
            await uowCreate.ExecSqlScriptAsync(await ReadSql("MySqlCreateDb.mysql"), false);
            var uow = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MySql.mysql"), false); //Mysql does not hanndle any ddl in transactions
            return (my, uow);
        }

        static async Task<string> ReadSql(string fileName)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var path = Path.Combine(baseDir, "TestDb", fileName);
            Assert.True(File.Exists(path), $"SQL file not found: {path}");
            return await File.ReadAllTextAsync(path);
        }

        /// <summary>
        ///  Spin up temporary Postgres Server in Docker
        /// </summary>
        /// <returns></returns>
        static async Task<(PostgreSqlContainer Container, UOWTestDb Uow)> PreparePgSqlContainer()
        {
            const string dbName = "TestDb";
            const string connNameCreate = "TestPgCreate";
            const string connName = "TestPg";
            var pg = new PostgreSqlBuilder()
                .WithImage("postgres:16.3")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithDatabase("postgres")
                .Build();
            await pg.StartAsync();
            var adminCs = pg.GetConnectionString();
                var adminBuilder = new Npgsql.NpgsqlConnectionStringBuilder(adminCs) { Database = dbName };
            var dbCs = adminBuilder.ToString(); var overrides = new Dictionary<string, string?>
            {
                ["DefaultAppConnName"] = connName,
                // create/admin (postgres)
                [$"DBConnections:{connNameCreate}:ConnectionString"] = adminCs,
                [$"DBConnections:{connNameCreate}:Provider"] = "PostgreSql",
                // normal/app (svak2)
                [$"DBConnections:{connName}:ConnectionString"] = dbCs,
                [$"DBConnections:{connName}:Provider"] = "PostgreSql"
            };
            var cfg = BuildConfig(overrides);
            var uowCreate = CreateUow(cfg, connNameCreate);
            await uowCreate.ExecSqlScriptAsync(await ReadSql("PgSqlCreateDb.pgsql"), false);
            var uow = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_PgSql.pgsql"), false); // Note: Migration script itself contains transactions so we do not run in transaction here
            // Force Npgsql to refresh type mappings (citext, etc.) for this database
            var dbConn = (Npgsql.NpgsqlConnection)uow.GetDbContext().Database.GetDbConnection();
            if (dbConn.State != ConnectionState.Open)
                await uow.GetDbContext().Database.OpenConnectionAsync();
            await dbConn.ReloadTypesAsync();
            Npgsql.NpgsqlConnection.ClearAllPools();
            var uowNew = CreateUow(cfg, connName);
            return (pg, uowNew);
        }

        /// <summary>
        /// Main moke test, async routines only
        /// </summary>
        /// <param name="uow"></param>
        /// <returns></returns>
        static async Task BasicSmokeAsync(UOWTestDb uow)
        {
            //
            //Test saving to database
            //
            var myRow = await uow.MyTables.Query().FirstOrDefaultAsync();
            Assert.NotNull(myRow); //do we have our seeded data ?
            var refRow = new DbTest.MyTableRef { MyInfo = "ref", LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "Philip" };
            myRow.MyTableRefs.Add(refRow);
            await uow.SaveChangesAsync(); //Wa can add refst to previous row
            var found = await uow.MyTableRefs.QueryNoTrack().FirstOrDefaultAsync(t => t.Id == refRow.Id);
            Assert.NotNull(found);
            //
            //Test view lookup
            //
            var viewItem = await uow.MyTableRefViews.QueryNoTrack().FirstOrDefaultAsync(tt => tt.RefId == refRow.Id);
            Assert.NotNull(viewItem);
            Assert.True((viewItem.RowID != Guid.Empty), "RowID should not be empty");
            //
            //Test calling SP/function for retreiving data from sequence
            //
            var IdList = await uow.GetNextSequenceIds(10);
            Assert.True(10 == IdList.Count, "Did not get 10 rows from sequence function");
            //
            //Bulk- insert & delete tests
            //
            var tt = new DbTest.MyTable { Id = 10, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm", RowID = Guid.NewGuid() };
            var tt2 = new DbTest.MyTable { Id = 11, LastChanged = DateTime.UtcNow, LastChangedBy = "gorm2", RowID = Guid.NewGuid() };
            await uow.RunInTransactionAsync(async ct =>
            {
                await uow.MyTables.BulkInsertAsync([tt, tt2], true,  ct);
            }, ct: CancellationToken.None);

            //
            //Testing that autogenerated sequences function as expected (should be no 12)
            //
            uow.MyTables.Add(new DbTest.MyTable() { LastChanged = DateTime.UtcNow, LastChangedBy = "swarm" });
            await uow.SaveChangesAndNewAsync();
            //
            //Now delete row no 10
            //
            await uow.MyTables.BulkDeleteByIdsAsync([10]);
            //
            //Now test your id´s are ok
            //
            var currIds = await uow.MyTables.QueryNoTrack().Select( tt => tt.Id ).ToListAsync();
            Assert.False(currIds.Where(tt => tt ==  10).Any(),"Bulkdelete failed, row 10 still exists");
            Assert.True(currIds.Where(tt => tt == 11).Any(), "Bulk inserted row not found");
            Assert.True(currIds.Where(tt => tt == 12).Any(), "Sequence not resetting after bulk-insert");
            //
            //Bulk-insert without identity, no transaction
            //
            await uow.MyTables.BulkDeleteByIdsAsync([11]);
            await uow.MyTables.BulkInsertAsync([tt,tt2]);
            var row2 = await uow.MyTables.ByKeyNoTrackAsync(13);
            Assert.True(row2 != null, "Bulk-insert without identies fail");
            //
            //Test scalar lookup
            //
            var fId = await uow.GetMaxIdByChanger("Stefan");
            Assert.True(fId == -2, "Scalar routine did not return valid id");
            //
            // Test transaction rollback, by insertin leagal and then illeagel row that sould trigger rollback of the whole transaction
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
                    var insideExists = await uow.MyTables.QueryNoTrack().AnyAsync(t => t.Id == rb.Id, cancellationToken: ct);
                    Assert.True(insideExists, "Row should be visible inside active transaction before rollback");
                    //Now add duplicate that sould trigger rollback
                    uow.MyTables.Add(rb2);
                    await uow.SaveChangesAsync(ct);
                }, ct: CancellationToken.None);
            }
            catch (Exception) { }
            var afterRollbackExists = await uow.MyTables.QueryNoTrack().AnyAsync(t => t.Id == rb.Id);
            Assert.False(afterRollbackExists, "Row should not exist after rollback");
            //
            // Odata Test and EDM model generation
            //
            var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow,"$filter=LastChangedBy eq 'Stefan'" );
            var baseQuery = uow.MyTables.QueryNoTrack();
            var filtResult = await uow.MyTables.FilterODataAsync(baseQuery,options,null,true);
             Assert.True(filtResult.InlineCount > 0 && !filtResult.Results.Any(x => x.LastChangedBy != "Stefan"), "We expect to find Stefans, but only Stefans" );
            // Verify that data exist with linQ
            var normRow = await uow.MyTables.QueryNoTrack().Where(tt => tt.Id == -1).Include(tt => tt.MyTableRefs.Where(r => r.MyInfo == "BigData")).ToListAsync();
            Assert.True(normRow.Count > 0, "");
            // Expand test, remember to allow expand with policy:
            var bq = uow.MyTables.QueryNoTrack();
            var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan = uow.MyTables.BuildODataQueryPlan(bq, options2, new ODataPolicy(AllowExpand: true), true);
            var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);
            Assert.True(plan2.Report.Where(tt => tt == "ExpandInnerFilterIgnored:MyTableRefs").Count() == 1, "We did not find $filter warning within AsInclude query"); 
            var res = await uow.MyTables.MaterializeODataAsync(plan2);
            //we received our MyTableRefs records inline (but unfiltered)
            Assert.True(res.InlineCount != null && res.InlineCount > 0 && res.Results != null &&  res.Results.FirstOrDefault() != null && res.Results.FirstOrDefault()!.MyTableRefs.Count > 0, 
                "$expand as include failed to produce data for MyTableRefs") ;
            //Now shaped tests:
            var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$select=Id");
            var plan3 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowSelect: true), true);
            var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);
            var res3 = await uow.MyTables.MaterializeODataShapedAsync(plan3,shapedQuery3);
            Assert.True(res3.Results != null && res3.Results.Count > 0, "Filtered and selected query failed");
            var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);
            Assert.True(json.Contains("\"Id\""), $"$select=Id expected 'Id' in shaped JSON.\nJSON: {json}");
            Assert.True(!json.Contains("LastChangedBy"), $"$select=Id should not include 'LastChangedBy'.\nJSON: {json}");
            Assert.True(!json.Contains("MyTableRefs"), $"$select=Id should not include navigation 'MyTableRefs'.\nJSON: {json}");
            //Inner filter test for shaped expansion
            var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow,"$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan4 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowExpand: true), true);
            var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
            var res4 = await uow.MyTables.MaterializeODataShapedAsync(plan4, shapedQuery4);
            Assert.True(res4.Results != null && res4.Results.Count > 0, "Expected at least one result from $filter=Id eq -1 with expanded MyTableRefs, but none were returned.");
        }

        /// <summary>
        /// Just a part of what we do for async, no need to repeat all tests
        /// </summary>
        /// <param name="uow"></param>
        static async Task BasicSmokeSynchronous(UOWTestDb uow)
        {
            //
            //Test saving to database
            //
            var myRow = uow.MyTables.Query().FirstOrDefault();
            Assert.NotNull(myRow); //do we have our seeded data ?
            var refRow = new DbTest.MyTableRef { MyInfo = "ref", LastChanged = DateTimeOffset.UtcNow, LastChangedBy = "Philip" };
            myRow.MyTableRefs.Add(refRow);
            uow.SaveChangesSynchronized(); //Wa can add refst to previous row
            var found = uow.MyTableRefs.QueryNoTrack().FirstOrDefault(t => t.Id == refRow.Id);
            Assert.NotNull(found);
            //
            //Test view lookup
            //
            var viewItem = uow.MyTableRefViews.QueryNoTrack().FirstOrDefault(tt => tt.RefId == refRow.Id);
            Assert.NotNull(viewItem);
            Assert.True((viewItem.RowID != Guid.Empty), "RowID should not be empty");
            //
            //Test calling SP/function for retreiving data from sequence
            //
            var IdList = uow.GetNextSequenceIdsSynchronized(10);
            Assert.True(10 == IdList.Count, "Did not get 10 rows from sequence function");
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
            //Testing that autogenerated sequences function as expected (should be no 12)
            //
            uow.MyTables.Add(new DbTest.MyTable() { LastChanged = DateTime.UtcNow, LastChangedBy = "swarm" });
            uow.SaveChangesAndNewSynchronized();
            //
            //Now delete row no 10
            //
            uow.MyTables.BulkDeleteByIdsSynchronized([10]);
            //
            //Now test your id´s are ok
            //
            var currIds = uow.MyTables.QueryNoTrack().Select(tt => tt.Id).ToList();
            Assert.False(currIds.Where(tt => tt == 10).Any(), "Bulkdelete failed, row 10 still exists");
            Assert.True(currIds.Where(tt => tt == 11).Any(), "Bulk inserted row not found");
            Assert.True(currIds.Where(tt => tt == 12).Any(), "Sequence not resetting after bulk-insert");
            //
            //Bulk-insert without identity, no transaction
            //
            uow.MyTables.BulkDeleteByIdsSynchronized([11]);
            uow.MyTables.BulkInsertSynchronized([tt, tt2]);
            var row2 = uow.MyTables.ByKeyNoTrackSynchronized(13);
            Assert.True(row2 != null, "Bulk-insert without identies fail");
            //
            //Test scalar lookup
            //
            var fId = uow.GetMaxIdByChangerSynchronized("Stefan");
            Assert.True(fId == -2, "Scalar routine did not return valid id");
            //
            // Test transaction rollback, by insertin leagal and then illeagel row that sould trigger rollback of the whole transaction
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
                    var insideExists = uow.MyTables.QueryNoTrack().Any(t => t.Id == rb.Id);
                    Assert.True(insideExists, "Row should be visible inside active transaction before rollback");
                    //Now add duplicate that sould trigger rollback
                    uow.MyTables.Add(rb2);
                    uow.SaveChangesSynchronized();
                });
            }
            catch (Exception) { }
            var afterRollbackExists = uow.MyTables.QueryNoTrack().Any(t => t.Id == rb.Id);
            Assert.False(afterRollbackExists, "Row should not exist after rollback");
            //
            // Odata Test and EDM model generation
            //
            var options = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=LastChangedBy eq 'Stefan'");
            var baseQuery = uow.MyTables.QueryNoTrack();
            var filtResult = uow.MyTables.FilterODataSynchronized(baseQuery, options, null, true);
            Assert.True(filtResult.InlineCount > 0 && !filtResult.Results.Any(x => x.LastChangedBy != "Stefan"), "We expect to find Stefans, but only Stefans");
            // Verify that data exist with linQ
            var normRow = uow.MyTables.QueryNoTrack().Where(tt => tt.Id == -1).Include(tt => tt.MyTableRefs.Where(r => r.MyInfo == "BigData")).ToList();
            Assert.True(normRow.Count > 0, "");
            // Expand test, remember to allow expand with policy:
            var bq = uow.MyTables.QueryNoTrack();
            var options2 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan = uow.MyTables.BuildODataQueryPlan(bq, options2, new ODataPolicy(AllowExpand: true), true);
            var plan2 = uow.MyTables.ApplyODataExpandAsInclude(plan);
            Assert.True(plan2.Report.Where(tt => tt == "ExpandInnerFilterIgnored:MyTableRefs").Count() == 1, "We did not find $filter warning within AsInclude query");
            var res = uow.MyTables.MaterializeODataSynchronized(plan2);
            //we received our MyTableRefs records inline (but unfiltered)
            Assert.True(res.InlineCount != null && res.InlineCount > 0 && res.Results != null && res.Results.FirstOrDefault() != null && res.Results.FirstOrDefault()!.MyTableRefs.Count > 0,
                "$expand as include failed to produce data for MyTableRefs");
            //Now shaped tests:
            var opts = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$select=Id");
            var plan3 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowSelect: true), true);
            var shapedQuery3 = uow.MyTables.ApplyODataSelectExpand(plan3);
            var res3 = uow.MyTables.MaterializeODataShapedSynchronized(plan3, shapedQuery3);
            Assert.True(res3.Results != null && res3.Results.Count > 0, "Filtered and selected query failed");
            var json = System.Text.Json.JsonSerializer.Serialize(res3.Results[0]);
            Assert.True(json.Contains("\"Id\""), $"$select=Id expected 'Id' in shaped JSON.\nJSON: {json}");
            Assert.True(!json.Contains("LastChangedBy"), $"$select=Id should not include 'LastChangedBy'.\nJSON: {json}");
            Assert.True(!json.Contains("MyTableRefs"), $"$select=Id should not include navigation 'MyTableRefs'.\nJSON: {json}");
            //Inner filter test for shaped expansion
            var opts4 = OdataTestHelper.CreateOptions<DbTest.MyTable>(uow, "$filter=Id eq -1&$expand=MyTableRefs($filter=MyInfo eq 'BigData')");
            var plan4 = uow.MyTables.BuildODataQueryPlan(bq, opts, new ODataPolicy(AllowExpand: true), true);
            var shapedQuery4 = uow.MyTables.ApplyODataSelectExpand(plan4);
            var res4 = uow.MyTables.MaterializeODataShapedSynchronized(plan4, shapedQuery4);
            Assert.True(res4.Results != null && res4.Results.Count > 0, "Expected at least one result from $filter=Id eq -1 with expanded MyTableRefs, but none were returned.");
        }
    }
}
