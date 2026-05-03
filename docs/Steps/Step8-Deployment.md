# Step 8: Local Deployment

Now that we've verified our model and views with TestContainers, it's time to get serious and deploy to a proper database server. This could be SQL Server (on-prem or Azure), PostgreSQL, or MySQL.

While you or your database administrator can always create the database and deploy migrations manually using the generated SQL scripts, we will demonstrate how to use the built-in command-line tool to handle the entire lifecycle.

> **Important:** Creating a new database usually requires **administrative privileges** (e.g., `dbcreator` role in SQL Server or `superuser` in PostgreSQL). Ensure your connection string uses an account with sufficient permissions, especially for the `create` step.

Specifically, we will show how the tool can:
1.  **Create** the physical database.
2.  **Apply** the EF Core migrations and custom views.
3.  **Import** the seed data.

In this walkthrough, we use **SQL Server** running on your local computer as our target.

## 8.1 Naming Your Database

If you need to change the database name from the default `MyPets` (from your project name), now is the time to do it. You must ensure your connection string is properly set up to point to your target server and database.

You can find and modify this name in two key places:

1.  **AppSettings.json**: Locate the connection strings in the `MyPets.Migrate` project.
    ```json
    "MsMyPets": {
      "ConnectionString": "data source=localhost;initial catalog=MyPets;...",
      "Provider": "SqlServer"
    }
    ```
2.  **Database Creation Scripts**: The scripts responsible for creating the physical database also contain the name. These are located in the `SQL` folder of the `MyPets.Migrate` project:
    - `MsSqlCreateDb.sql`
    - `PgSqlCreateDb.pgsql`
    - `MySqlCreateDb.mysql`

For example, in `MsSqlCreateDb.sql`:
```sql
DECLARE @DbName sysname = N'MyPets';
```

## 8.2 Build the Solution

Ensure your project is built before running the migration tool. This generates the necessary executables and copies the SQL/CSV files to the output directory.

## 8.3 Testing Connectivity

Before running the full deployment, you can test if your connection settings are correct and check the status of your database. The migration tool includes a `check` command for this purpose.

Open a terminal in the folder where the `MyPets.Migrate` executable is located (usually `bin/Debug/net8.0/`, `net9.0/` or `net10.0/`).

Run the check command:
```bash
./MyPets.Migrate check
```
> **Note:** If you run the command without a connection name, it will use the default connection defined in `AppSettings.json` (see section 8.4 for details).

Or specify a specific connection:
```bash
./MyPets.Migrate MsMyPets check
```

This command will:
- Verify connectivity to the target database.
- If the database doesn't exist, it will attempt to connect to the "admin" database (e.g., `master` for SQL Server or `postgres` for PostgreSQL) to verify server access.
- Report any pending migrations that haven't been applied yet.

## 8.4 Using the Migration Tool

The `MyPets.Migrate` project is a command-line tool that handles the deployment lifecycle. It is designed to be provider-agnostic, using the connection name to determine which database flavor (SQL Server, PostgreSQL, or MySQL) to target.

### Connection Selection
The tool follows this priority for choosing a connection:
1.  **Command-line argument**: `./MyPets.Migrate [ConnectionName] [Command]`
2.  **Named parameter**: `./MyPets.Migrate --conn [ConnectionName] --cmd [Command]`
3.  **Default Configuration**: If no connection is specified, it uses the `DefaultAppConnName` defined in `AppSettings.json`.

For more advanced scenarios, refer to the [EF Migrations Command Guide](../EfMigrationsCMD.md).

### Deployment Steps
Run the following three commands in sequence:

#### 1. Create the Database (`create`)
This runs the provider-specific creation script (e.g., `MsSqlCreateDb.sql`) using an administrative connection. It ensures the physical database exists on your server.
```bash
./MyPets.Migrate MsMyPets create
```

#### 2. Apply Migrations (`migrate`)
This executes the `DbDeploy_*.sql` script (e.g., `DbDeploy_MsSql.sql`) located in the `Migrations` folder. This script is a "package" containing all EF Core table definitions plus your custom manual SQL for views (`PetDetails`).
```bash
./MyPets.Migrate MsMyPets migrate
```

#### 3. Import Seed Data (`import`)
This executes the `ImportService.cs` logic. It reads the CSV files from the `Data/CSV` folder and populates your tables.
```bash
./MyPets.Migrate MsMyPets import
```

## 8.5 Verify with your Database Tool

Open your favorite database tool (e.g., Rider's Database tab, DBeaver, or SSMS) and connect to your local server. Verify that:
- The `MyPets` database exists.
- The `Pets` and `AnimalTypes` tables contain the data from your CSV files.
- The `PetDetails` view correctly returns the joined data.

---

[Next Step: Business Logic and Unit of Work](Step9-Logic.md)  
[Back to Overview](../GettingStarted.md)
