# Manual Installation & Integration

> **Note:** The quickest way to get started is by using the **EfCore.Boost project templates**. Simply create a new project from a template and copy the relevant parts into your application.

If you prefer not to start from a template, you can integrate **EfCore.Boost** into an existing project. This guide demonstrates how to apply EfCore.Boost to a pre-existing database project or `DbContext`.

## 1. Install the NuGet Package

Add the `EfCore.Boost` package to your project:

```powershell
dotnet add package EfCore.Boost
```

---

## 2. Update Your Models (Entities)

EfCore.Boost encourages using attributes for model definition, which can avoid much of the need for complex Fluent API configurations in `OnModelCreating`.

### Conversion Guide

When integrating with an existing database, you typically face two scenarios:

1.  **Make the current model portable**: You want your model to work across different database providers (SQL Server, PostgreSQL, etc.). Use EfCore.Boost attributes to abstract away provider-specific types.
2.  **Work with current database (Non-portable)**: You want to keep your existing database schema exactly as is. Use attributes to match your current column types and sizes.

#### Attribute Checklist for Existing Models

*   **Identifiers**: Use `[DbUid]` for primary keys or unique identifiers.
*   **Precision vs. Decimal**: Use precision attributes to match your database column definitions, or saving to it can get messy.
*   **String Lengths**: Ensure your `string` property lengths match your database columns.
    *   If you decide to update your database, we recommend standardizing on "slot sizes": **50, 80, 256, & 512**.
    *   Use `[StrCode]` (50), `[StrShort]` (80), `[StrMed]` (256), or `[StrLong]` (512) and attach `strsize` if needed.
*   **PostgreSQL Case-Insensitivity**: EfCore.Boost expects PostgreSQL to use `citext` columns for case-insensitive searching.
    *   *Best Practice*: Move your PostgreSQL database to use `citext` if possible.
    *   *Alternative*: If you cannot change the column type, use Fluent API to override the database type (see example in the Attribute Section below).

### Add EfCore.Boost Attributes
At a minimum, you should identify your primary keys or unique identifiers using the `DbUid` attribute if you want to leverage EfCore.Boost's repository features effectively.

```csharp
using EfCore.Boost;

public class Customer
{
    [DbUid] // Marks this as a unique identifier for repository operations
    public Guid Id { get; set; }
    
    [Name] // Provider-agnostic string length
    public string Name { get; set; } = string.Empty;
}
```
You don't need to change every attribute except if you have a string without a length specification, and the data needs to be searchable.
Then you select `[StrCode]`, `[StrMed]`, or  `[StrLong]` to indicate the string length, or just like above where we use the `[Name]` attribute.


### Attribute Section & Fluent API

While attributes are preferred, the **Fluent API** is fully supported and sometimes necessary for fine-grained control or when you cannot modify the entity classes.

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
   modelBuilder.ApplyEfBoostConventions(this, "core");
    // Example: Using Fluent API to mark a property as a unique identifier
    modelBuilder.Entity<Customer>()
        .Property(c => c.Id).HasDbUid();
    // Example: Overriding database type for PostgreSQL (if not using citext)
    modelBuilder.Entity<Customer>()
        .Property(c => c.Name).HasColumnType("varchar(255)"); 
}
```

### Benefits of Attributes vs. Fluent API
- **Readability**: The schema intent is visible directly on the entity.

---

## 3. Configure Your DbContext

Your `DbContext` needs to call the EfCore.Boost convention builder.

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Apply EfCore.Boost conventions
        // "core" is the default schema name for your tables/views
        modelBuilder.ApplyEfBoostConventions(this, "core");
    }
}
```

By calling `ApplyEfBoostConventions`, you enable:
- Automatic naming conventions (SnakeCase, PascalCase, etc., handled per provider).
- Automatic mapping of `DbUid`, `DbLen`, and other attributes.
- Consistent handling of deletes and relations.

---

## 4. Configuration (appsettings.json)

EfCore.Boost relies on a structured way to handle connection strings and provider types.

### Update Connection Strings
In your `appsettings.json`, ensure your connection string includes the provider type if you want to support multiple databases easily.

```json
{
  "DefaultAppConnName": "BoostXMs",
  "DBConnections": {
    "BoostXMs": {
      "ConnectionString": "data source=localhost;initial catalog=BoostXDb;integrated security=True;TrustServerCertificate=True",
      "Provider": "SqlServer"
    },
    "BoostXPg": {
      "ConnectionString": "Host=127.0.0.1;Port=5432;Username=Core;Password=MyPsw2001.;Database=BoostXDb",
      "Provider": "Postgres"
    }
  }
}
```
*Supported providers: `SqlServer`, `PostgreSql`, `MySql`.*

### New Connection String Format (Azure-specific)
For Azure-hosted databases, EfCore.Boost supports additional parameters to handle Azure-specific settings and managed identities.

```json
"BoostXAzure": {
  "ConnectionString": "Server=tcp:myserver123.database.windows.net,1433;Initial Catalog=BoostXDb;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;",
  "UseAzure": "True",
  "Provider": "SqlServer",
  "UseManagedIdentity": "False",
  "AzureTenantId": "<your tenant id>",
  "AzureClientId": "<your client id>",
  "AzureClientSecret": "<your client secret set via global var>",
  "CommandTimeoutSeconds": "60",
  "RetryCount": "3",
  "RetryDelaySeconds": "5"
}
```

---

## 5. Create the Unit of Work (UOW)

The Unit of Work is the gateway to your data. Instead of injecting `DbContext` everywhere, you inject a UOW factory.

### Define the UOW
```csharp
public class AppUow(DbContext ctx, EfDbType dbType) : DbUow(ctx, dbType)
{
    public IAsyncRepo<Customer> Customers => new EfRepo<Customer>(Ctx, DbType);
}
```

### Create the UOW Factory
The factory handles the creation of the `DbContext` and the UOW.

```csharp
public interface IAppUowFactory : IUowFactory<AppUow>;

public class AppUowFactory(IConfiguration cfg) 
    : UowFactory<AppDbContext, AppUow>(cfg, "Default"), IAppUowFactory
{
    // The base class handles DbContext creation and provider selection
    protected override AppUow CreateUow(AppDbContext context, EfDbType dbType) 
        => new(context, dbType);
}
```

---

## 6. Dependency Injection

Register your factory in `Program.cs`:

```csharp
builder.Services.AddSingleton<IAppUowFactory, AppUowFactory>();
```

Usage within a service:
```csharp
public class CustomerService(IAppUowFactory uowFactory)
{
    public async Task CreateCustomer(string name)
    {
        using var uow = uowFactory.Create();
        await uow.Customers.AddAsync(new Customer { Name = name });
        await uow.SaveChangesAsync();
    }
}
```

---

## 7. Migrations

### Keeping Migrations in Place
If you already have migrations in your existing project, they will continue to work. However, after applying `ApplyEfBoostConventions`, your next migration might contain significant changes as naming conventions and defaults are normalized.

### Moving Migrations (Recommended)
We recommend moving migrations to a separate console project (similar to the `BoostX.Migrate` project in our templates). This allows you to:
- Keep your model project clean of migration metadata.
- Run migrations against different database providers using the same model.
- Include seed data management (CSV imports) and custom SQL (views/routines) in one deployment tool.

You can "borrow" the code from the `BoostX.Migrate` project in the `boostsimple` template to set this up.

---

## Summary of Changes
- **Attributes**: Use `[DbUid]`, `[Name]`, etc.
- **Conventions**: Call `modelBuilder.ApplyEfBoostConventions(this, "schema")`.
- **UOW**: Wrap your data access in a `DbUow` and use a `UowFactory`.
- **Defaults**: Enjoy predictable cascading deletes and provider-agnostic mapping.
