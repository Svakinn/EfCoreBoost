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

There is one “prepare” method per database flavor:

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
4. Create a UOW for “create/admin”, run the create-db script
5. Create a UOW for “normal/app”, run the provider migration script
6. Return `(container, uow)`

The details differ slightly per engine, but the structure is the same.

## Example: PostgreSQL container setup

This is the PostgreSQL prepare method (trimmed to the essential idea):

```csharp
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
    var dbCs = adminBuilder.ToString();

    var overrides = new Dictionary<string, string?>
    {
        ["DefaultAppConnName"] = connName,

        // create/admin
        [$"DBConnections:{connNameCreate}:ConnectionString"] = adminCs,
        [$"DBConnections:{connNameCreate}:Provider"] = "PostgreSql",

        // normal/app
        [$"DBConnections:{connName}:ConnectionString"] = dbCs,
        [$"DBConnections:{connName}:Provider"] = "PostgreSql"
    };

    var cfg = BuildConfig(overrides);

    var uowCreate = CreateUow(cfg, connNameCreate);
    await uowCreate.ExecSqlScriptAsync(await ReadSql("PgSqlCreateDb.pgsql"), false);

    var uow = CreateUow(cfg, connName);
    await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_PgSql.pgsql"), false);

    return (pg, uow);
}
```

Notes:
- Connection settings are overridden in-memory; no files are modified on disk.
- Provider-specific migration scripts are used because DDL rules differ between engines.
- PostgreSQL requires a type-mapping refresh step in the full implementation.

## SQL Server and MySQL

SQL Server and MySQL follow the same pattern:
- start container
- create database via admin connection
- apply migrations
- return a ready UOW

The only differences are the container image, provider name, and SQL scripts used.
