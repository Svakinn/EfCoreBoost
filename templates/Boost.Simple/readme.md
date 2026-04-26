# BoostX Generated Project

This solution was generated using the `EfCore.Boost Simple Solution Template`. It provides a clean, four-project architecture for building and maintaining an Entity Framework Core data model, its migrations, an API, and tests.

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
- Run the script for your database provider.

### Initial / Rebuild (Wipes previous migrations)
- PostgreSQL: `./PgMigrate.ps1`
- SQL Server: `./MsMigrate.ps1`
- MySQL: `./MyMigrate.ps1`

## 3. Configure Database Connections

Update `appsettings.json` to define your database connections.

Unlike the default .NET approach (single unnamed or loosely named connection strings),  
EfCore.Boost uses a **named connection model with provider metadata**:

```json
{
  "DefaultAppConnName": "BoostXPg",
  "DBConnections": {
    "BoostXMs": {
      "ConnectionString": "...",
      "Provider": "SqlServer"
    },
    "BoostXPg": {
      "ConnectionString": "...",
      "Provider": "Postgres"
    },
    "BoostXMy": {
      "ConnectionString": "...",
      "Provider": "MySql"
    }
  }
}
```

---

### 🧠 Concept

Each connection is:

- **Named** (`BoostXMs`, `BoostXPg`, etc.)
- **Self-contained**, including:
    - Connection string
    - Database provider

This allows the system to:
- Select database providers dynamically
- Avoid hardcoding provider logic in code
- Support multiple database engines in parallel

---

### 🎯 Default Connection

```json
"DefaultAppConnName": "BoostXPg"
```

This defines:

- The connection used when none is explicitly specified
- The default for:
    - Application runtime
    - Migration tools
    - Command-line utility

Changing this value allows you to switch the **entire application context** to another database provider or environment.

---

### ⚙️ Why this approach?

This structure is designed for **control and flexibility**, especially in multi-environment and multi-provider setups.

#### a) Programmatic control

Your application can:

- Select connections by name
- Switch providers at runtime
- Route different workloads to different databases

#### b) Tooling consistency

All tooling (migration scripts, CLI utility, etc.) uses the same structure:

- No duplication of connection configuration
- No mismatch between runtime and migration setup

#### c) Environment switching

You can easily:

- Move from local → test → production
- Switch servers or credentials
- Change provider (e.g., Postgres → SQL Server)

…by updating a **single configuration section**

---

## 4. Apply Migrations
You can apply migrations manually using the generated SQL scripts or by using the command-line utility.

### Using the Utility
Run the migration utility from the `BoostX.Migrate` project:

```bash
# Optional: Create the database if it doesn't exist
BoostX.Migrate [connection] create

# Apply the primary deployment script
BoostX.Migrate [connection] update
```

### Alternative: Using SQL Deployment Scripts
Ready-to-use SQL deployment scripts are located in the `/SQL` folder.

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

## Using the Template in IDEs

This template generates a multi-project solution (Model + Migrate).

### Rider / Visual Studio

- Use **New Solution / New Project** to create a project from the template
- The template will not appear under **Add → New Project** inside an existing solution

### Alternative (CLI)

You can always create the project using:

```bash
dotnet new boostsimple -n YourProjectName
