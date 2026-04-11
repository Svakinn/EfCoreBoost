using BoostX.Model;
using EfCore.Boost.DbRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BoostX.Migrate
{
    /// <summary>
    /// Entry point for the BoostX Database Migration Utility.
    /// This utility handles database creation, schema migration, status checks, and data import.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Main entry point for the console application.
        /// Parses command-line arguments and dispatches to the appropriate command handler.
        /// </summary>
        /// <param name="args">
        /// Command-line arguments: [connName] [command]
        /// command: check, createdb, migrate, import.
        /// </param>
        private static async Task Main(string[] args)
        {
            Console.WriteLine("--- BoostX Database Migration Utility ---");
            if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h" || args[0] == "?" || args[0] == "/?"))
            {
                ShowUsage();
                return;
            }
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            // Named arguments logic
            string? connName = null;
            string? command = null;
            var positionalArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                if (arg == "--connection" || arg == "--conn")
                {
                    if (i + 1 < args.Length)
                        connName = args[++i];
                }
                else if (arg == "--command" || arg == "--cmd")
                {
                    if (i + 1 < args.Length)
                        command = args[++i].ToLowerInvariant();
                }
                else if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    // Ignore unknown flags for robustness or show error?
                    // Let's ignore it for now but allow help to trigger ShowUsage.
                }
                else
                    positionalArgs.Add(args[i]);
            }
            // Positional arguments fallbacks (if not already set by named arguments)
            if (string.IsNullOrEmpty(connName) && positionalArgs.Count > 0)
                connName = positionalArgs[0];
            if (string.IsNullOrEmpty(command) && positionalArgs.Count > 1)
                command = positionalArgs[1].ToLowerInvariant();
            // Configuration fallback for connection name
            if (string.IsNullOrEmpty(connName))
                connName = configuration["DefaultAppConnName"] ?? "";
            // Default command
            if (string.IsNullOrEmpty(command))
                command = "check";
            if (string.IsNullOrWhiteSpace(connName))
            {
                Console.WriteLine("Error: Connection name not specified.");
                ShowUsage();
                return;
            }
            Console.WriteLine($"Using connection: {connName}");
            Console.WriteLine($"Command: {command}");
            try
            {
                if (command == "check")
                    await HandleCheck(configuration, connName);
                else if (command == "createdb" || command == "create")
                    await HandleCreateDb(configuration, connName);
                else if (command == "migrate" || command == "update")
                    await HandleMigrate(configuration, connName);
                else if (command == "import")
                    await HandleImport(configuration, connName);
                else
                {
                    Console.WriteLine($"Unknown command: {command}");
                    ShowUsage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command '{command}': {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            }
        }

        private static void ShowUsage()
        {
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  BoostX.Migrate [connection] [command]");
            Console.WriteLine("  BoostX.Migrate --connection [connection] --command [command]");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  --connection, --conn  Name of the connection to use.");
            Console.WriteLine("  --command, --cmd     Command to execute (default: check).");
            Console.WriteLine("");
            Console.WriteLine("Commands:");
            Console.WriteLine("  check                Checks database status and pending migrations.");
            Console.WriteLine("  createdb / create    Creates the database if it doesn't exist.");
            Console.WriteLine("  migrate / update     Runs pending migrations.");
            Console.WriteLine("  import               Imports seed data.");
            Console.WriteLine("");
            Console.WriteLine("Examples:");
            Console.WriteLine("  BoostX.Migrate MyDb check");
            Console.WriteLine("  BoostX.Migrate --conn MyDb --cmd migrate");
            Console.WriteLine("  BoostX.Migrate MyDb");
        }

        /// <summary>
        /// Checks the database status, including connectivity and pending migrations.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="connName">Name of the connection to check.</param>
        private static async Task HandleCheck(IConfiguration configuration, string connName)
        {
            using var uow = new BoostXUow(configuration, connName);
            const string testSql = "SELECT 'Hello' AS Value";
            bool normalConnWorks = false;
            try
            {
                Console.WriteLine($"Connecting to database using '{connName}'...");
                await uow.ExecuteNonQueryAsync(testSql);
                normalConnWorks = true;
                Console.WriteLine("Connection to target database successful.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Target database connection failed: {ex.Message}");
                Console.WriteLine("Attempting to connect to admin/master database...");
                try
                {
                    // Try to run a simple script on the admin database (e.g. postgres or master)
                    await uow.ExecuteAdminDbSqlScriptAsync(testSql);
                    Console.WriteLine("Connection to admin database successful. The target database may not exist.");
                }
                catch (Exception adminEx)
                {
                    Console.WriteLine($"Admin database connection also failed: {adminEx.Message}");
                    var msg = adminEx.Message.ToLowerInvariant();
                    if (msg.Contains("login failed") || msg.Contains("authentication failed") || msg.Contains("password authentication failed") ||
                        msg.Contains("access denied for user"))
                        Console.WriteLine("Error: Authentication failed. Please check your admin credentials.");
                    else if (msg.Contains("network-related") || msg.Contains("server was not found") || msg.Contains("could not open a connection") ||
                             msg.Contains("failed to connect") || msg.Contains("unknown host"))
                        Console.WriteLine("Error: Database server not reachable. Check your network connection and server address.");
                    else
                        Console.WriteLine($"Error: A connection problem occurred: {adminEx.Message}");
                    return;
                }
            }
            if (!normalConnWorks)
            {
                Console.WriteLine("Database check complete. Target database needs to be created.");
                return;
            }
            Console.WriteLine("Checking for pending migrations...");
            var pendingMigrations = await uow.GetDbContext().Database.GetPendingMigrationsAsync();
            int count = 0;
            foreach (var migration in pendingMigrations)
            {
                Console.WriteLine($" - Pending: {migration}");
                count++;
            }
            if (count == 0)
                Console.WriteLine("Database is up to date.");
            else
            {
                Console.WriteLine($"Database schema is not up to date. Found {count} pending migration(s).");
                Console.WriteLine("Please run the schema initialization process.");
            }
        }

        /// <summary>
        /// Constructs the absolute file path for a SQL script.
        /// </summary>
        /// <param name="dbType">The database provider type (e.g., SqlServer, PostgreSql).</param>
        /// <param name="isCreate">True if searching for a database creation script; false if searching for a migration/update script.</param>
        /// <returns>The full path to the SQL file.</returns>
        private static string GetSqlScriptPath(DatabaseType dbType, bool isCreate)
        {
            string folder = isCreate ? "SQL" : "Migrations";
            string fileName = dbType switch
            {
                DatabaseType.SqlServer => isCreate ? "MsSqlCreateDb.sql" : "DbDeploy_MsSql.sql",
                DatabaseType.PostgreSql => isCreate ? "PgSqlCreateDb.pgsql" : "DbDeploy_PgSql.pgsql",
                DatabaseType.MySql => isCreate ? "MySqlCreateDb.mysql" : "DbDeploy_MySql.mysql",
                _ => throw new Exception($"Unsupported database type for scripts: {dbType}")
            };
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder, fileName);
        }

        /// <summary>
        /// Creates the target database using the appropriate creation SQL script.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="connName">Name of the target connection (used to find the corresponding 'Create' connection).</param>
        private static async Task HandleCreateDb(IConfiguration configuration, string connName)
        {
            using var uow = new BoostXUow(configuration, connName);
            var sqlFile = GetSqlScriptPath(uow.DbType, true);
            Console.WriteLine($"Executing creation script: {sqlFile}");
            var sql = await File.ReadAllTextAsync(sqlFile);
            await uow.ExecuteAdminDbSqlScriptAsync(sql);  //New admin connection, i.e., to Master/Postgres
            Console.WriteLine("Database created successfully.");
        }

        /// <summary>
        /// Executes the database migration script to update the schema to the latest version.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="connName">Name of the connection to migrate.</param>
        private static async Task HandleMigrate(IConfiguration configuration, string connName)
        {
            using var uow = new BoostXUow(configuration, connName);
            var sqlFile = GetSqlScriptPath(uow.DbType, false);
            Console.WriteLine($"Executing migration script: {sqlFile}");
            var sql = await File.ReadAllTextAsync(sqlFile);
            // Script contains own transactions, therefore, cannot run in transaction here
            await uow.ExecSqlScriptAsync(sql);
            Console.WriteLine("Migration completed successfully.");
        }

        /// <summary>
        /// Orchestrates the data import process from CSV files into the database.
        /// </summary>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="connName">Name of the connection to import into.</param>
        private static async Task HandleImport(IConfiguration configuration, string connName)
        {
            using var uow = new BoostXUow(configuration, connName);
            await ImportService.ExecuteAsync(uow);
        }
    }
}
