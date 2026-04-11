# BoostX Generated Project

This project was generated using the `EfCore.Boost Simple Template`. It provides a clean, two-project structure for building and maintaining an Entity Framework Core data model and its migrations.

## Generated Projects

- **BoostX.Model**: Contains the `BoostCTX` class, entity models, and Unit of Work (UoW) logic.
- **BoostX.Migrate**: A console utility for database deployment, schema management, and data import.

# Getting Started

After generating the project from the template, follow these basic steps to get up and running.

## 1. Extend the Model
- Add or modify entities in the Model project
- Configure mappings and relationships as needed

## 2. Generate Migrations
- Open a terminal in the `/Ps` folder
- Run the script for your database provider:
    - PostgreSQL: `./PgMigrate.ps1`
    - SQL Server: `./MsMigrate.ps1`
    - MySQL: `./MyMigrate.ps1`

## 3. Configure Database Connection
- Update `appsettings.json`
- Update your connection string(s)


## 4. Apply Migrations
Run the migration utility:

```bash
BoostX.Migrate check
BoostX.Migrate create
BoostX.Migrate update
```

### Alternative: Using SQL Deployment Scripts

In addition to running migrations through the application, the project generates ready-to-use SQL deployment scripts.

These scripts are located in the `/SQL` folder, for example:

```text
/SQL/DbDeploy_MsSQL.sql
```

## 5. (Optional) Import Seed Data

```bash
BoostX.Migrate import
```
The `BoostX.Migrate` utility is the primary tool for managing your database.

## More Information

For detailed documentation on migration workflows, supporting multiple database providers (MS SQL, PostgreSQL, MySQL), and manual SQL scripting, please refer to the [README in the BoostX.Migrate project](./BoostX.Migrate/README.md).

## Using the Model in Your Application

Consumer applications should reference the **Model** project only.

- Add a project reference to `BoostX.Model`

Make sure to configure the required **EfCore.Boost connection string** in your application’s `appsettings.json`, using the same connection name expected by the model configuration.
