# The BoostX.Migrate Project

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

## Your workflow
### Step 1: Update Your Model

- Modify or add entities in the Model project (the other project :) )

### Step 2: Generate Migrations

Navigate to the `/Ps` folder and run the appropriate script.

### Initial or Rebuild Migrations

These scripts are used when creating the initial set of migrations or when rebuilding them from scratch.  
**Warning:** running these scripts will remove any existing migrations for the selected provider.

You can run the process for all providers or target a single provider as needed.  
The scripts ensure that only one provider’s migrations are active at a time, preventing conflicts between, for example, MySQL and MS SQL migration sets.

The result is a single deployment script per provider, which can be used to apply the full database schema.  
Alternatively, the schema can be applied directly using the command-line utility (see below).

**Scripts:**
- PostgreSQL: `./PgMigrate.ps1`
- SQL Server: `./MsMigrate.ps1`
- MySQL: `./MyMigrate.ps1`

---

### Incremental Migrations

When migrations already exist, and you need to introduce changes after updating the model, use the `AddMigration` scripts.  
These scripts preserve the existing migration history and allow you to include manual SQL additions.

This approach is typically used when releasing new versions of an application.  
In such cases, you generally want to provide two deployment options:

- A script for upgrading from the previous version to the current version
- A script for installing the application from scratch

This can be challenging with standard EF Core migrations, which assume a purely incremental flow.  
Our recommendation is to rely on source control (e.g., Git) to retain versioned migrations using branches or tags.

During development, use the `Migrate.ps1`-style scripts to generate a clean, full deployment script.  
When producing versioned upgrades, use the incremental scripts listed below.

A typical workflow could be:
- Check out the branch corresponding to the previous release
- Update the data model to the current version
- Run the incremental migration scripts
- Store the generated versioned scripts back in your main branch

**Scripts:**
- PostgreSQL: `./PgAddMigration.ps1`
- SQL Server: `./MsAddMigration.ps1`
- MySQL: `./MyAddMigration.ps1`

Migration names are automatically generated using a sequence number, for example:
`AddBoostCTX-1`, `AddBoostCTX-2`

#### Manual SQL in Incremental Migrations

The incremental scripts look for manual SQL files in the `SQL/` folder that match the migration sequence number. For example:
- `SQL/MsSql-1.sql` will be appended to the generated SQL for migration #1
- `SQL/Postgres-2.pgsql` will be appended to the generated SQL for migration #2
- `SQL/MySQL-1.mysq` will be appended to the generated SQL for migration #1

This is the preferred approach for adding custom database objects such as indexes, views, or routines that are directly tied to a specific schema change.

---

#### Summary

When running incremental migration scripts, the process will:

- Generate EF Core migrations (`migrations add`)
- Keep multiple providers in sync
- Generate provider-specific SQL scripts under the `Migrations` folder  
  (e.g., `Migrations/DbDeploy_MsSQL-1.sql`)
- Merge matching manual SQL from the `SQL/` folder into the final deployment script


## Using the Command-Line Utility

This section describes how to use the `BoostX.Migrate` command-line utility to:
- create the database
- apply the schema (initial deployment)
- import seed data

It complements the migration scripts by providing a controlled way to **apply** what those scripts produce.

---

### Invocation

You can call the utility using **positional arguments**:

```bash
BoostX.Migrate [connection] [command]
```

- `connection` (optional): The name of the database connection to use.  
  Defaults to `DefaultAppConnName` in `appsettings.json`.
- `command` (optional): The action to perform.  
  Defaults to `check`.

Or using **named options**:

```bash
BoostX.Migrate --connection [connection] --command [command]
```

- `--connection`, `--conn`: The connection name
- `--command`, `--cmd`: The command to execute

---

## Commands

- **check**  
  Validates the database connection and reports any pending migrations.

- **createdb / create**  
  Creates the database if it does not already exist.

- **migrate / update**  
  Applies the primary schema deployment script to the database.
  > Note: This typically applies only the **initial schema**. Incremental scripts must be applied separately.

- **import**  
  Seeds the database with initial or required data.

---

## Two-Part Migration Process (Create & Update)

The migration workflow is intentionally split into two distinct steps:

1. **Create (`createdb`)**  
   Ensures that the physical database exists on the server.

2. **Update (`migrate`)**  
   Applies the schema using the generated deployment script.

### Why the Split?

The separation between `create` and `update` (migrate) is intentional and reflects how database environments are typically managed in real-world scenarios.

There are two primary reasons for this design:

1. **Different Connection Contexts**

   Database creation is often performed using a **different (administrative) connection** than the one used by the application.

    - Creating a database typically requires elevated permissions at the server level
    - Applying migrations operates within the context of a specific database

   Because of this, it is both practical and cleaner to separate these operations rather than coupling them into a single step.

2. **Restricted Access in Controlled Environments**

   In many environments, especially production:

    - Database administrators (DBAs) create the database manually
    - Application developers are granted permissions **only within that database**
    - Server-level privileges (such as CREATE DATABASE) are intentionally restricted

   This means:
    - You may not be allowed to create the database at all
    - But you are expected to apply schema changes within an existing database

---

By separating these steps:

- The process becomes compatible with both **fully automated setups** and **restricted enterprise environments**
- You avoid mixing responsibilities between **infrastructure provisioning** and **schema management**
- The workflow remains flexible:
    - Use `create` when you have permissions and need automation
    - Skip `create` when the database already exists and go straight to `migrate`

In short, the split ensures that the tool works equally well for local development, CI/CD pipelines, and tightly controlled production environments.

---


## Typical First-Time Installation Flow

A typical installation sequence looks like this:

```bash
BoostX.Migrate MyDb create
BoostX.Migrate MyDb migrate
BoostX.Migrate MyDb import
```

This will:
1. Create the database
2. Apply the initial schema
3. Import seed data

---

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
