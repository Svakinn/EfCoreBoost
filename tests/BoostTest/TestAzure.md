# Azure SQL Testing (No Containers)

Unlike PostgreSQL, SQL Server (local), and MySQL, **Azure SQL Database cannot be tested using local containers** in a way that is representative and repeatable.  
For that reason, EfCore.Boost includes a dedicated Azure test path that targets a *real Azure SQL database*.

This doc explains:
- how the Azure test is wired
- what you need to provision in Azure
- how the test safely skips itself when Azure isn�t configured
- what the helper SQL scripts do

> Further reading: container-based testing is documented in [TestContainers.md](./tests/BoostTest/TestContainers.md).

---

## Why Azure is different

Azure SQL is a managed service:
- you cannot spin up �Azure SQL� locally as a faithful Docker container
- authentication is commonly integrated with **Microsoft Entra ID** (formerly Azure AD)
- database creation and permissions are controlled by Azure roles and firewall rules

So instead of container orchestration, the Azure test loads a connection definition from `AppSettings.json` and runs smoke tests against your Azure database.

---

## What you need in Azure

### 1) A SQL Server and an empty database

You need an Azure SQL **server** and an *empty* database named **`TestDb`**

If you must change the database name, ensure that you also update the 3 places in the test as well:
- `AppSettings.json` - connection string
- the `PrepareAzureAccess()` method
- `AzurePrepareDb.sql`

This differs from the container tests: we do **not** require a master-admin account like the container tests often do. We connect directly to the target database and keep the setup idempotent.

### 2) An identity for authentication

The tests authenticate using a service identity:
- either a managed identity (recommended for CI inside Azure)
- or an Entra ID app registration (service principal) with a client secret

Entra ID is Microsoft�s identity system used by Azure services.  
In practice, you create an identity, then grant it access to the SQL database.

---

## AppSettings.json connection example

Your repo includes a disabled Azure connection definition. The idea is:

- keep Azure settings present but inert by default
- enable them only when you actually want to run Azure tests

Example (disabled template):

```json
"TestAzure": {
  "ConnectionString": "Server=tcp:myserver123.database.windows.net,1433;Initial Catalog=TestDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
  "UseAzure": "True",
  "Provider": "SqlServer",
  "UseManagedIdentity": "False",
  "AzureTenantId": "<your tenant id>",
  "AzureClientId": "<your cient id>",
  "AzureClientSecret": "<your client secret set via global var>",
  "CommandTimeoutSeconds": "60",
  "RetryCount": "3",
  "RetryDelaySeconds": "5"
}
```

### Important security note

Do **not** commit real secrets to source control.

Prefer:
- store the secret as an environment variable
- load it at runtime into configuration
- keep `AppSettings.json` containing placeholders only

Example (scrambled/dummy values):

```json
"TestAzure": {
  "ConnectionString": "Server=tcp:yourserver123.database.windows.net,1433;Initial Catalog=TestDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
  "UseAzure": "True",
  "Provider": "SqlServer",
  "UseManagedIdentity": "False",
  "AzureTenantId": "f2b8d6a1-9c47-4e3a-a8f4-3c1e92d7b605",
  "AzureClientId": "8c1f3a92-6e4b-4d7e-b2a9-71c6f0e58d43",
  "AzureClientSecret": "Ll~9A~.qJjLkj235In2UwyOHIkePUR.EUmIlse",
  "CommandTimeoutSeconds": "60",
  "RetryCount": "3",
  "RetryDelaySeconds": "5"
}
```

---

## The Azure prepare method

The Azure tests use a prepare method that:
- loads `AppSettings.json`
- checks whether Azure is actually configured (or still placeholder)
- returns `null` to skip the test when not configured
- otherwise prepares the DB, applies migrations, and returns a ready `UOWTestDb`

```csharp
static async Task<UOWTestDb?> PrepareAzureAccess()
{
    const string dbName = "TestDb";
    const string connName = "TestAzure";

    var cc = new ConfigurationBuilder()
        .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
        .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: false)
        .Build();

    var dbTestCfg = DbConnectionCFG.Get(cc, connName);

    if (dbTestCfg == null || dbTestCfg.UseAzure == false
        || dbTestCfg.AzureClientSecret.Length < 2 || dbTestCfg.AzureClientSecret[..1] == "<")
        return null; // Skip test if not properly configured, no error thrown

    if (dbTestCfg.ConnectionString.IndexOf(dbName, StringComparison.OrdinalIgnoreCase) < 0)
        throw new Exception($"Azure test DB connection string must contain database name '{dbName}'");

    var uow = CreateUow(cc, connName);

    await uow.ExecSqlScriptAsync(await ReadSql("AzurePrepareDb.sql"), false); // Cleanup previous runs / ensure DB state
    await uow.ExecSqlScriptAsync(await ReadSql("Migrations/DbDeploy_MsSql.sql"), false); // Normal SQL Server migrations

    return uow;
}
```

### What this method is doing

- **Configuration gate**: if secrets are placeholders, it returns `null` so the Azure test can skip itself cleanly.
- **Safety check**: it enforces that the connection string points at `TestDb`.
- **Prepare + cleanup**: it runs `AzurePrepareDb.sql` to reset state between runs.
- **Migrations**: it applies the normal SQL Server migration script (`DbDeploy_MsSql.sql`).

---

## What the Azure SQL scripts do

### AzurePrepareDb.sql

This script is responsible for making sure the database is in a known good state for tests:

- grants the specific user access if needed
- **removes test data/objects from previous runs** so smoke tests start clean

It is designed to be safe to run repeatedly.

### Migrations/DbDeploy_MsSql.sql

- Applies the normal SQL Server schema produced from the EF Core model
- This is the same migration script used for non-Azure SQL Server testing (DDL compatible)

---

## How the Azure test runs

Typical pattern:

1. Azure test method calls `PrepareAzureAccess()`
2. If it returns `null`, the test is skipped (Azure not configured)
3. If it returns a UOW, the standard smoke tests run against it
4. Cleanup is handled by `AzurePrepareDb.sql`

---

## Troubleshooting checklist

- Azure SQL firewall allows your IP (or CI runner)
- The configured identity has access to `TestDb`
- `UseAzure` is set to true for the Azure connection entry
- Secrets are provided via environment variables, not placeholders
- Connection string includes `Initial Catalog=TestDb`
