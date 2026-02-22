# Database Configuration  

## Unit of work handles connections
Unit of Work (UOW) classes encapsulate database connection resolution. All you need to do is assign a unique name to each database connection and define its settings within the `DBConnections` section in `appsettings.json`.

## The `DbConnections` Structure

Projects using Boost define database connections using a dedicated **`DbConnections`** configuration block.
EfCore.Boost requires a structured view of database connectivity.
It must know which database provider is in use, how authentication is handled, and what capabilities apply.


This allows Boost to:

- distinguish between database providers

- switch database flavors without code changes

- support Azure and non-Azure databases consistently

- apply provider-specific behavior internally

- support migrations and tooling in a controlled way

By minimum each entry contains **ConnectionString** and **Provider**.  
The provider Provider must be one of **SQLServer**, **PostgreSQL**, or **MySQL**.     
Other optional settings control Azure behaviour.

---

## Defining Database Connections

The following example shows how we use a **DbConnections** section to define three provider flavors of the same logical Core database, and a separate Logs database.

```json
{
  "DefaultAppConnName": "PgCoreDb",

  "DbConnections": {
    "AzCoreDb": {
      "ConnectionString": "Server=tcp:myexample.database.windows.net,1433;Initial Catalog=TestDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
      "UseAzure": "True",
      "Provider": "SqlServer",
      "UseManagedIdentity": "False",
      "AzureTenantId": "3c9a5f7e-42a6-4bb3-9b0c-ef1a6a0d8f24",
      "AzureClientId": "d7f81c2b-63d4-4a42-b5aa-91d8c3a7e22f",
      "AzureClientSecret": "ZQ~4DkN8yP1LrWfT0c",
      "CommandTimeoutSeconds": "60",
      "RetryCount": "3",
      "RetryDelaySeconds": "5"
    },

    "PgCoreDb": {
      "Provider": "PostgreSql",
      "ConnectionString": "Host=127.0.0.1;Port=5432;Username=Core;Password=ThePsw2001.;Database=postgres"
    },

    "MyCoreDb": {
      "Provider": "MySql",
      "ConnectionString": "server=127.0.0.1;port=3306;database=TestDb;user=Core;password=ThePsw2001.;TreatTinyAsBoolean=false;SslMode=none;AllowPublicKeyRetrieval=True;"
    },

    "LogsDb": {
      "Provider": "MySql",
      "ConnectionString": "server=127.0.0.1;port=3306;database=Logs;user=Core;password=ThePsw2001.;TreatTinyAsBoolean=false;SslMode=none;AllowPublicKeyRetrieval=True;"
    }
  }
}
```

### What this example shows

- **CoreDb** exists in three different provider variants:
  - Azure SQL
  - PostgreSQL
  - MySQL
- All three represent the *same logical database*
- Only one CoreDb variant is typically active at a time; the Unit of Work constructor determines which configuration is used.
- **LogsDb** is deliberately separate and local in this example

---

## Design-Time DbContext Creation and Default Connection Selection

When generating migrations, EF requires a design-time DbContext factory.  
This factory is **written by the user**, not supplied automatically.

Example:

```csharp
public class DbTestContextFactory : IDesignTimeDbContextFactory<DbTest>
{
    public DbTest CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration =
            new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
        return SecureContextFactory.CreateDbContext<DbTest>(configuration);
    }
}
```
> ⚠️ **Important**  
> Design-time DbContext factories are used by EF tooling only.
> Runtime behavior is controlled via Units of Work.

See [this documentation](../Model/EfMigrationsCMD.md) on how to build migrations for multiple providers.

### Why the connection name is omitted

The call above does **not** specify the connection name.

When the connection name is omitted, `SecureContextFactory` resolves the connection using **`DefaultAppConnName`** from configuration.

This is intentional and useful when:

- more than one database flavor exists
- migrations must be generated for different providers
- scripts or users control which provider is active
- code should not be edited just to target another database

Specifying a connection name here would lock the factory to a single provider and require code changes to migrate other database flavors.

---

## Unit of Work Construction and Connection Selection

Each Unit of Work explicitly defines how its DbContext is created.

```csharp
public partial class UOWTestDb(IConfiguration cfg, string cfgName) UowFactory<DbTest>(cfg, cfgName) {..
```

### Usage Examples

```csharp
// 1. Hard-coded connection name
// Locks this Unit of Work to a specific database configuration
var uowAz = new UOWTestDb(configuration, "AzCoreDb");


// 2. Default connection
// Passing an empty or null value causes the DefaultAppConnName to be used
var uowDefault = new UOWTestDb(configuration);


// 3. Custom connection name resolved from configuration
// Allows caller or environment to decide which database to target
var uowCustom = new UOWTestDb(configuration, configuration["MyChosenOne"]);

// 4. Typical usage would be to inject factory for uow into the application like this:
b.Services.AddScoped<IUowTestDbFactory, UowTestDbFactory>();
//Where the factory code is something like this:
    public sealed class UowTestDbFactory(IConfiguration cfg) : IUowTestDbFactory
    {
        public IUowTestDb Create(string? cfgName = null) => 
           new UowTestDb(cfg, string.IsNullOrWhiteSpace(cfgName) ? cfg["DefaultAppConnName"]! : cfgName);
    }
```
---
## About Azure connections
There are multiple ways to connect to Azure SQL:

- Standard connection string (no Azure token) – set `UseAzure: false`.
- Azure AD Application Authentication – set `UseAzure: true` and provide:
  - `AzureClientId`
  - `AzureTenantId`
  - `AzureClientSecret`
- Managed Identity (MSI) – set both `UseAzure: true` and `UseManagedIdentity: true`. Only `AzureClientId` is required (if using user-assigned identity).

⚠️ Note: Managed Identity only works when running in an Azure-hosted environment (e.g., App Service, Function App, VM, etc.)  

---
## ✅ Appendix: Best Practices for Secrets

Secrets management is not glamorous. It is repetitive. It is sometimes boring. And it is never repeated enough.
Passwords, keys, and connection strings deserve deliberate handling. Most security incidents are not caused by exotic exploits but by carelessness, copy-paste accidents, 
or forgotten test credentials that quietly ship to production.

- Do **not** commit secrets (passwords/keys) into version-controlled config files.
- Use environment variables at deployment time.
- Set variables per user on Windows (especially with IIS); per process/service on Linux.
- Azure Key Vault integration is not currently included, as it is primarily relevant for Azure-only deployments. This can be added later if required.
- Support for Windows Credential Store is not implemented either. If you assign a real user to your App Pool, local environment variables provide sufficient isolation.
- Avoid using system-wide environment variables for sensitive data on production servers.
- Never reuse production passwords in development environments.
- Rotate service credentials periodically, especially for long-lived APIs and background workers.
---

**Environment variable override:**

```bash
DBConnections__MyDatabase__ConnectionString="Server=prod-db;Database=MyApp;User Id=svc;Password=secret;"
```

Below are some tips on how to set up environment variable overrides for different environments

---

## 💻 Setting Environment Variables

### 🔹 Windows (Dev Machine)

#### Option A — Temporary for session

```powershell
$env:DBConnections__MyDatabase__ConnectionString="..."
```

#### Option B — Persistent (recommended)

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "User")
```

---

### 🔹 Windows Server (IIS / App Pool User)

- ⚠️ If your connection string includes credentials, avoid using virtual identities (like the default IIS App Pool identity).
- Instead, create a dedicated local or domain user account and assign it to your IIS Application Pool.
- Then set the environment variable under that specific user’s context.

Example:

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "User")
```

Or for system-wide (not recommended unless unavoidable):

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "Machine")
```

---

### 🔹 Linux / Bash

#### Temporary session override:

```bash
export DBConnections__MyDatabase__ConnectionString="..."
```

#### Persistent override:

Add to one of the following:

- `~/.bashrc`
- `~/.bash_profile`
- `/etc/environment` (system-wide)

Note: On Linux, web services typically run under managed process supervisors (like `systemd`), where environment variables must be defined per service.

---

### 🔹 Docker

```dockerfile
ENV DBConnections__MyDatabase__ConnectionString="..."
```

Or in `docker-compose.yml`:

```yaml
environment:
  - DBConnections__MyDatabase__ConnectionString=...
  - ConnectionStrings__MyConnName=...
```

---

### 🔹 Kubernetes (K8s)

```yaml
env:
  - name: DBConnections__MyDatabase__ConnectionString
    valueFrom:
      secretKeyRef:
        name: my-secret
        key: mydatabase-conn-string
```

---

## Summary

- `DbConnections` defines available logical database connections
- Multiple provider flavors can represent the same logical database
- `DefaultAppConnName` selects which connection is active
- Design-time factories intentionally omit connection names for flexibility
- Users or scripts change `DefaultAppConnName` when targeting different providers
- Units of Work explicitly select connections when needed

This approach supports multi-database development while keeping configuration, tooling, and code responsibilities clearly separated.
