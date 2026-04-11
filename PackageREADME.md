# EfCore.Boost.Template.Simple

This is a project template for **EfCore.Boost**, providing a clean, production-oriented starting point for database-backed applications using Entity Framework Core.

EfCore.Boost extends EF Core with a structured Unit of Work and Repository pattern, multi-database support (SQL Server, PostgreSQL, MySQL), and conventions that promote consistent, maintainable, and vendor-neutral data access.

The template generates a minimal, ready-to-use solution with Model and Migrate projects, demonstrating core patterns such as DbContext configuration, schema management, and SQL-based deployment workflows.

## Features

- **BoostX.Model**: Contains the `DbContext`, entity models, and Unit of Work logic.
- **BoostX.Migrate**: A console utility for database schema management, multi-provider migrations (SQL Server, PostgreSQL, MySQL), and seed data management.
- Pre-configured patterns for schema names, normalized `DbContext` naming, and SQL deployment script generation.

## Getting Started

To install the template from NuGet:
```powershell
dotnet new install EfCore.Boost.Template.Simple
```

To create a new project:
```powershell
dotnet new boostsimple -n YourProjectName
```

Create with explicit DbContext and schema configuration:
```powershell
dotnet new boostsimple -n YourProjectName --Schema YourSchemaName --Context YourDbContextName
```

Example:
```powershell
dotnet new boostsimple -n SalesCtx --Schema sales --Context DbSalesContext
```

For more details on usage, see the `Usage.md` file in the generated project.

## About EfCore.Boost

EfCore.Boost is an extension library for Entity Framework Core focused on structured data access and cross-database support.

- 📦 NuGet: https://www.nuget.org/packages/EfCore.Boost
- 📚 Documentation & source: https://github.com/Svakinn/EfCoreBoost

EfCore.Boost provides:
- Unit of Work and Repository patterns out of the box
- Multi-provider support (SQL Server, PostgreSQL, MySQL)
- Convention-based model configuration
- Integrated support for views, routines, and bulk operations

For deeper architectural details and advanced scenarios, refer to the official documentation.
