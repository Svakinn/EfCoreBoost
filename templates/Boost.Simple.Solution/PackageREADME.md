# EfCore.Boost.Template.Simple.Solution

This is a **solution template** for **EfCore.Boost**, providing a complete, production-oriented starting point for database-backed applications using Entity Framework Core.

EfCore.Boost extends EF Core with a structured Unit of Work and Repository pattern, multi-database support (SQL Server, PostgreSQL, MySQL), and conventions that promote consistent, maintainable, and vendor-neutral data access.

Unlike basic project templates, this is a **solution template**, meaning it generates a full `.sln` file containing four projects: **BoostX.Model**, **BoostX.Migrate**, **BoostX.Api**, and **BoostX.Test**. This makes it an excellent choice for starting a new application from scratch, as it sets up the entire architecture following our recommended database-first or model-first approach.

## IDE Integration

This template is fully compatible with modern IDEs:
- **JetBrains Rider**: Appears in the "New Solution" dialog.
- **Visual Studio**: Appears in the "Create a new project" dialog (search for "solution").

You can also use it to create a fresh solution, configure your database, and then copy the resulting projects into another existing solution if needed.

## Features

- **BoostX.Model**: Contains the `DbContext`, entity models, and Unit of Work logic.
- **BoostX.Migrate**: A console utility for database schema management, multi-provider migrations (SQL Server, PostgreSQL, MySQL), and seed data management.
- **BoostX.Api**: A demonstration ASP.NET Core Web API project showcasing the Unit of Work pattern, OData-style querying, and background processing using **EfCore.Boost**.
- **BoostX.Test**: A comprehensive test project using **Testcontainers** to run integration tests against real database instances (PostgreSQL, SQL Server, MySQL).
- Pre-configured patterns for schema names, normalized `DbContext` naming, and SQL deployment script generation.

## Getting Started

To install the template from NuGet:
```powershell
dotnet new install EfCore.Boost.Template.Simple.Solution
```

To create a new solution via CLI:
```powershell
dotnet new boostsimplesolution -n YourSolutionName
```

Create with explicit DbContext and schema configuration:
```powershell
dotnet new boostsimplesolution -n YourSolutionName --Schema YourSchemaName --Context YourDbContextName
```

Example:
```powershell
dotnet new boostsimplesolution -n SalesApp --Schema sales --Context DbSalesContext
```

For more details on usage, see the `readme.md` file in the root folder of the generated solution.

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
