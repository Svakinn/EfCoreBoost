# Usage of Testcontainers

This test project uses **Testcontainers** to validate EfCore.Boost against real database engines (PostgreSQL, SQL Server, MySQL).

A *test container* is a disposable database server:
- started on demand (per test)
- configured by code (image, user/pass, database)
- used for one run
- then shut down and discarded

That gives us integration tests that are repeatable and provider-authentic, without requiring developers to install and maintain three database servers locally.

## Where it happens

All container orchestration lives in `BoostTestContainers` (often referred to as `UnitTestContainers.cs` in discussion).

There is one ´prepare´ method per database flavor:

- `PreparePgSqlContainer()`
- `PrepareMsSqlContainer()`
- `PrepareMySqlContainer()`

Each method returns:
- the running container instance
- a ready-to-use `UOWTestDb`

So each test can do:

```csharp
var (container, uow) = await PreparePgSqlContainer();
await using (container)
using (uow)
{
    await BasicSmokeAsync(uow);
}
```

Same test flow, different database reality.

## The common lifecycle (per provider)

Every provider follows the same overall ritual:

1. Start a DB server container
2. Build two connection strings:
   - **admin/create** connection (for creating the test DB or running bootstrap SQL)
   - **app/test** connection (pointing at the actual `TestDb`)
3. Build an `IConfiguration` by overlaying in-memory connection overrides on top of `AppSettings.json`
4. Create a UOW for ´create/admin´, run the create-db script
5. Create a UOW for ´normal/app´, run the provider migration script
6. Return `(container, uow)`

The details differ slightly per engine, but the structure is the same.

## Example: MySQL container setup

This is the MySQL prepare method.
We more or less build our own connection string to the test container and run our migrations on it via SQL scripts:

```csharp
        static async Task<(MySqlContainer Container, UOWTestDb Uow, UOWTestDb Uow2, UOWTestView UowView)> PrepareMySqlContainer()
        {
            const string dbName = "TestDb";
            const string connName = "TestMy";
            var myBuilder = new MySqlBuilder()
                .WithImage("mysql:8.0")
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
            await uow.ExecuteAdminDbSqlScriptAsync(await ReadSql("Sql/MySqlCreateDb.mysql"));
            var uow2 = CreateUow(cfg, connName);
            await uow.ExecSqlScriptAsync(await ReadSql("Migrate/DbDeploy_MySql.mysql")); //Mysql does not handle any ddl in transactions
            var uowV = CreateUowView(cfg, connName);
            return (myBuilder, uow, uow2, uowV);
        }
```

Notes:
- Connection settings are overridden in-memory; no files are modified on disk.
- Provider-specific migration scripts are used because DDL rules differ between engines.
- PostgreSQL requires a type-mapping refresh step in the full implementation.

## SQL Server, Postgres and MySQL

Test for all providers follow the same pattern:
- start container
- create database via admin connection
- apply migrations
- return a ready UOW