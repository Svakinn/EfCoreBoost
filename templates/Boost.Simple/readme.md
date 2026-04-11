# BoostX Generated Project

This project was generated using the `EfCore.Boost Simple Template`. It provides a clean, two-project structure for building and maintaining an Entity Framework Core data model and its migrations.

## Generated Projects

- **BoostX.Model**: Contains the `BoostCTX` class, entity models, and Unit of Work (UoW) logic.
- **BoostX.Migrate**: A console utility for database deployment, schema management, and data import.

## Quick Start

The `BoostX.Migrate` utility is the primary tool for managing your database.

### Common Commands

- **Check**: Validates the connection and reports pending migrations.
  ```powershell
  dotnet run --project BoostX.Migrate --command check
  ```

- **Create**: Creates the database if it doesn't already exist.
  ```powershell
  dotnet run --project BoostX.Migrate --command create
  ```

- **Migrate/Update**: Applies any pending schema migrations.
  ```powershell
  dotnet run --project BoostX.Migrate --command update
  ```

- **Import**: Seeds the database with initial or required data.
  ```powershell
  dotnet run --project BoostX.Migrate --command import
  ```

## More Information

For detailed documentation on migration workflows, supporting multiple database providers (MS SQL, PostgreSQL, MySQL), and manual SQL scripting, please refer to the [README in the BoostX.Migrate project](./BoostX.Migrate/README.md).

