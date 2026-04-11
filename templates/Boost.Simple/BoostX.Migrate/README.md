# BoostX Database Migration Utility

The `BoostX.Migrate` utility is a specialized tool with a dual purpose in the development and deployment lifecycle of applications using the BoostCTX database context.

## Core Purpose

This project serves two primary functions:

1.  **Migration Preparation & Script Generation**:
    - It is used to generate Entity Framework Core migrations for the project.
    - It acts as a container for **manual SQL scripts** (e.g., for views, routines, custom indexes, or triggers).
    - it produces **production-ready SQL scripts** that can be used to apply the database layer directly to a production database server.

2.  **Database Initialization & Seed Data**:
    - It provides the capability to optionally run/apply migrations directly to a target database.
    - It handles the import of initial/seed data.
    - **Note on Seed Data Architecture**: The seed and initial data logic is intentionally kept in this separate project. This ensures that the primary `DbContext` and Unit of Work (UoW) in the main application remain clean and free from the overhead of seed data management and large static data sets.

## Usage

You can call the utility using positional arguments or named options.

### Positional Arguments
```bash
BoostX.Migrate [connection] [command]
```
- `connection`: (Optional) The name of the database connection to use. If omitted, it defaults to the value of `DefaultAppConnName` in `AppSettings.json`.
- `command`: (Optional) The action to perform. Defaults to `check`.

### Named Options
```bash
BoostX.Migrate --connection [connection] --command [command]
```
- `--connection`, `--conn`: The name of the connection.
- `--command`, `--cmd`: The command to execute.

## Commands

- **check**: Validates the database connection and reports any pending migrations.
- **createdb / create**: Creates the database if it doesn't already exist.
- **migrate / update**: Applies any pending schema migrations to the database.
- **import**: Seeds the database with initial or required data.

## Two-Part Migration Process (Create & Update)

The migration process is intentionally split into two distinct steps: `create` and `update` (migrate).

1.  **Create (`createdb`)**: This step ensures the physical database exists on the server.
2.  **Update (`migrate`)**: This step applies the Entity Framework Core migrations to define the schema.

### Why the Split?
This separation is designed to allow developers to include **manual SQL scripts** for database objects that are not easily managed through standard EF Core migrations, such as:
- Complex Database Views
- Stored Procedures and Functions (Routines)
- Custom Indexes or Constraints
- Triggers

By running `create` first, you ensure the database container is ready. You can then execute custom SQL scripts before or alongside the `update` command to ensure all necessary database components are correctly initialized and available for the application.

## Applying Migrations to the Model

To apply migrations to the model, you use the PowerShell scripts located in the `Ps/` directory. This is typically done during development when you change the entity model and need to generate new migrations or update the database schema for different database providers.

1.  Open a terminal in the `Ps/` folder.
2.  Execute the script corresponding to your database provider:
    - **Microsoft SQL Server**: `./MsMigrate.ps1`
    - **PostgreSQL**: `./PgMigrate.ps1`
    - **MySQL**: `./MyMigrate.ps1`

### Supporting Multiple Providers
The project is designed to hold and maintain migrations for all three supported providers (MS SQL, PostgreSQL, and MySQL) at the same time. When you make a change to the model, you should ideally run the migration script for each provider to ensure the database schema remains synchronized across all target environments.

## Examples

Check the status of the default connection:
```bash
BoostX.Migrate
```

Create and migrate a specific database:
```bash
BoostX.Migrate MyDb create
BoostX.Migrate MyDb update
```

Import seed data using named arguments:
```bash
BoostX.Migrate --conn MyDb --cmd import
```
