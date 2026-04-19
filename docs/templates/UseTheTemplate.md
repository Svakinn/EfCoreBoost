# Using EfCore.Boost Templates

This guide explains how to install and use the templates for **EfCore.Boost**. These templates provide a production-oriented starting point for database-backed applications using Entity Framework Core, following the recommended architecture of EfCore.Boost.

## Installation

Both templates are distributed via NuGet. You can install them using the .NET CLI:

```powershell
# Install the Project Template (Model + Migration)
dotnet new install EfCore.Boost.Template.Simple

# Install the Solution Template (Full Solution with Model + Migration + Web API + Tests)
dotnet new install EfCore.Boost.Template.Simple.Solution
```

## Available Templates

There are two primary templates available, catering to different needs:

### 1. Simple Project Template (`boostsimple`)
This template adds a pair of model and migration projects. It is ideal for adding EfCore.Boost to an existing workspace or starting a focused data layer.

- **Short Name**: `boostsimple`
- **Projects included**:
  - **BoostX.Model**: `DbContext`, entity models, and Unit of Work logic.
  - **BoostX.Migrate**: Console utility for multi-provider migrations (SQL Server, PostgreSQL, MySQL).

### 2. Simple Solution Template (`boostsimplesolution`)
This is a full **solution template**. It generates a complete `.sln` file and sets up the entire architecture, including a Web API and integration tests.

- **Short Name**: `boostsimplesolution`
- **Projects included**:
  - **BoostX.Model**: `DbContext`, entity models, and Unit of Work logic.
  - **BoostX.Migrate**: Console utility for multi-provider migrations.
  - **BoostX.Api**: ASP.NET Core Web API project showcasing the Unit of Work pattern and OData-style querying.
  - **BoostX.Test**: Comprehensive test project using **Testcontainers** for integration testing against real databases.

## IDE Usage

Once installed, both templates are fully integrated into modern IDEs:

- **JetBrains Rider**: 
  - Both templates appear in the **New Solution** dialog.
  - Check for Database under "Other" in the Project Type column.
- **Visual Studio**: 
  - Both templates appear in the **Create a new project** dialog.
  - Search for "EfCore.Boost" or "boost".

## CLI Usage

You can also instantiate the templates via the command line.

### Create a new project set:
```powershell
dotnet new boostsimple -n MyProjectName --Schema my_schema --Context MyDbContext
```

### Create a new full solution:
```powershell
dotnet new boostsimplesolution -n MySolutionName --Schema my_schema --Context MyDbContext
```

### Common Parameters:
- `-n | --name`: The base name for the projects (replaces `BoostX`).
- `--Schema`: The database schema name (default: `core`).
- `--Context`: The name of the `DbContext` class (default: matches project name).

## About EfCore.Boost

EfCore.Boost is an extension library for Entity Framework Core focused on structured data access and cross-database support.

- 📦 NuGet: [https://www.nuget.org/packages/EfCore.Boost](https://www.nuget.org/packages/EfCore.Boost)
- 📚 Documentation & source: [https://github.com/Svakinn/EfCoreBoost](https://github.com/Svakinn/EfCoreBoost)

EfCore.Boost provides:
- Unit of Work and Repository patterns out of the box.
- Multi-provider support (SQL Server, PostgreSQL, MySQL).
- Convention-based model configuration.
- Integrated support for views, routines, and bulk operations.
