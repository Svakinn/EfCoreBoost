using BoostX.Model;
using EfCore.Boost;
using EfCore.Boost.CFG;
using EfCore.Boost.DbRepo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BoostX.Migrate
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("--- BoostX Database Migration Utility ---");
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Usage example:
            // dotnet BoostX.Migrate [connName] [import|check|createdb|migrate]
            // If the connection name is omitted, it will be read from AppSettings.json["DefaultAppConnName"]

            // Determine connection name from args or config
            string connName = args.Length > 0 ? args[0] : configuration["DefaultAppConnName"] ?? "";

            if (string.IsNullOrWhiteSpace(connName))
            {
                Console.WriteLine("Error: Connection name not specified. Provide it as an argument or set 'DefaultAppConnName' in AppSettings.json.");
                return;
            }

            // Command logic: second argument after connection name (optional)
            string command = args.Length > 1 ? args[1].ToLowerInvariant() : "check";

            Console.WriteLine($"Using connection: {connName}");
            Console.WriteLine($"Command: {command}");

            try
            {
                if (command == "check")
                {
                    await HandleCheck(configuration, connName);
                }
                else if (command == "createdb")
                {
                    await HandleCreateDb(configuration, connName);
                }
                else if (command == "migrate")
                {
                    await HandleMigrate(configuration, connName);
                }
                else if (command == "import")
                {
                    await HandleImport(configuration, connName, args);
                }
                else
                {
                    Console.WriteLine($"Unknown command: {command}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command '{command}': {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }
            }
        }

        private static async Task HandleCheck(IConfiguration configuration, string connName)
        {
            await using var db = SecureContextFactory.CreateDbContext<BoostXDbContext>(configuration, connName);
            try
            {
                Console.WriteLine("Connecting to database...");
                await db.Database.OpenConnectionAsync();
                await db.Database.CloseConnectionAsync();
            }
            catch (Exception ex)
            {
                var msg = ex.Message.ToLowerInvariant();
                if (msg.Contains("login failed") || msg.Contains("authentication failed") || msg.Contains("password authentication failed") || msg.Contains("access denied for user"))
                {
                    Console.WriteLine("Error: Authentication failed. Please check your username and password.");
                }
                else if (msg.Contains("network-related") || msg.Contains("server was not found") || msg.Contains("could not open a connection") || msg.Contains("failed to connect") || msg.Contains("unknown host"))
                {
                    Console.WriteLine("Error: Database server not reachable. Check your network connection and server address.");
                }
                else if (msg.Contains("does not exist") || msg.Contains("database") && msg.Contains("unknown") || msg.Contains("database") && msg.Contains("not found"))
                {
                    Console.WriteLine("Error: The specified database does not exist on the server.");
                }
                else if (msg.Contains("timeout") || msg.Contains("timed out"))
                {
                    Console.WriteLine("Error: Connection timed out. The server might be busy or unreachable.");
                }
                else
                {
                    Console.WriteLine($"Error: A connection problem occurred: {ex.Message}");
                }
                return;
            }

            Console.WriteLine("Checking for pending migrations...");
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();

            int count = 0;
            foreach (var migration in pendingMigrations)
            {
                Console.WriteLine($" - Pending: {migration}");
                count++;
            }

            if (count == 0)
            {
                Console.WriteLine("Database is up to date.");
            }
            else
            {
                Console.WriteLine($"Database schema is not up to date. Found {count} pending migration(s).");
                Console.WriteLine("Please run the schema initialization process.");
            }
        }

        private static async Task HandleCreateDb(IConfiguration configuration, string connName)
        {
            string createConnName = GetCreateConnectionName(configuration, connName);
            Console.WriteLine($"Using create connection: {createConnName}");

            using var uowCreate = new BoostXUow(configuration, createConnName);
            var sqlFile = GetSqlScriptPath(uowCreate.DbType, true);

            Console.WriteLine($"Executing creation script: {sqlFile}");
            var sql = await File.ReadAllTextAsync(sqlFile);
            await uowCreate.ExecSqlScriptAsync(sql);
            Console.WriteLine("Database created successfully.");
        }

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

        private static async Task HandleImport(IConfiguration configuration, string connName, string[] args)
        {
            string createConnName = GetCreateConnectionName(configuration, connName);
            using var uow = new BoostXUow(configuration, connName);
            using var createUow = new BoostXUow(configuration, createConnName);

            var importer = new Import();
            // Passing original args, skipping connName and command if they were provided
            await importer.ExecuteAsync(uow, createUow, args.Skip(2).ToArray());
        }

        private static string GetCreateConnectionName(IConfiguration configuration, string connName)
        {
            var dbCfg = DbConnectionCfg.Get(configuration, connName);
            if (dbCfg == null) return connName;

            if (IsCreateConnectionString(dbCfg.ConnectionString, dbCfg.Provider))
                return connName;

            // Search for a matching create connection in the same config
            var dbConnections = configuration.GetSection("DBConnections").GetChildren();
            foreach (var section in dbConnections)
            {
                var otherCfg = DbConnectionCfg.Get(configuration, section.Key);
                if (otherCfg == null) continue;

                if (otherCfg.Provider == dbCfg.Provider && IsCreateConnectionString(otherCfg.ConnectionString, otherCfg.Provider))
                {
                    // Optionally check if they point to the same server/host
                    if (IsSameServer(dbCfg.ConnectionString, otherCfg.ConnectionString, dbCfg.Provider))
                    {
                        return section.Key;
                    }
                }
            }

            // Fallback to naming convention if no match found
            return connName.EndsWith("Create", StringComparison.OrdinalIgnoreCase) ? connName : connName + "Create";
        }

        private static bool IsCreateConnectionString(string connectionString, string provider)
        {
            var normalizedProvider = SecureContextFactory.NormalizeProvider(provider);
            return normalizedProvider switch
            {
                "sqlserver" => connectionString.Contains("initial catalog=master", StringComparison.OrdinalIgnoreCase) ||
                               connectionString.Contains("database=master", StringComparison.OrdinalIgnoreCase),
                "postgresql" => connectionString.Contains("database=postgres", StringComparison.OrdinalIgnoreCase),
                "mysql" => !connectionString.Contains("database=", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private static bool IsSameServer(string connStr1, string connStr2, string provider)
        {
            var normalizedProvider = SecureContextFactory.NormalizeProvider(provider);
            return normalizedProvider switch
            {
                "sqlserver" => GetPart(connStr1, "data source") == GetPart(connStr2, "data source") ||
                               GetPart(connStr1, "server") == GetPart(connStr2, "server"),
                "postgresql" => GetPart(connStr1, "host") == GetPart(connStr2, "host") &&
                                GetPart(connStr1, "port") == GetPart(connStr2, "port"),
                "mysql" => GetPart(connStr1, "server") == GetPart(connStr2, "server") &&
                           GetPart(connStr1, "port") == GetPart(connStr2, "port"),
                _ => false
            };
        }

        private static string GetPart(string connectionString, string key)
        {
            var parts = connectionString.Split(';');
            foreach (var part in parts)
            {
                var kvp = part.Split('=');
                if (kvp.Length == 2 && kvp[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp[1].Trim();
                }
            }
            return string.Empty;
        }

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
    }
}
