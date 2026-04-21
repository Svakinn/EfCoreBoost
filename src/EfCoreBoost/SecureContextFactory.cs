// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Azure.Core;
using Azure.Identity;
using EfCore.Boost.CFG;
using EfCore.Boost.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
// Optional but sometimes helps with tooling
//using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace EfCore.Boost
{
    /// <summary>
    /// Static factory for creating <see cref="DbContext"/> instances with support for multiple database providers,
    /// Azure authentication (Managed Identity/Service Principal), and custom <see cref="IInterceptor"/> implementations.
    /// It handles connection string retrieval from configuration and provider-specific configurations.
    /// </summary>
    public static class SecureContextFactory
    {
        /// <summary>
        /// Creates a <see cref="DbContext"/> instance using configuration settings.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="DbContext"/> to create.</typeparam>
        /// <param name="configuration">The <see cref="IConfiguration"/> instance to retrieve connection settings from.</param>
        /// <param name="configName">The name of the connection configuration. If empty, uses the 'DefaultAppConnName' from configuration.</param>
        /// <param name="interceptors">An optional collection of <see cref="IInterceptor"/> to be applied to the <see cref="DbContext"/>.</param>
        /// <param name="migrationAssembly">The name of the assembly containing the migrations.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="Exception">Thrown if 'DefaultAppConnName' or the connection configuration is missing.</exception>
        public static T CreateDbContext<T>(IConfiguration configuration, string configName = "", IEnumerable<IInterceptor>? interceptors = null, string? migrationAssembly = null) where T : DbContext
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                var dbConnName = configuration["DefaultAppConnName"];
                if (string.IsNullOrWhiteSpace(dbConnName))
                    throw new Exception("DefaultAppConnName missing or no value in config file !");
                configName = dbConnName;
            }
            var dbCfg = DbConnectionCfg.Get(configuration, configName);
            if (dbCfg == null || string.IsNullOrWhiteSpace(dbCfg.ConnectionString))
                throw new Exception("DbConfig for " + configName + " is missing !");
            return CreateDbContext<T>(
                dbCfg.ConnectionString,
                dbCfg.UseAzure,
                dbCfg.UseManagedIdentity,
                dbCfg.AzureTenantId,
                dbCfg.AzureClientId,
                dbCfg.AzureClientSecret,
                dbCfg.Provider,
                ComposeInterceptors(interceptors, dbCfg.UseUtcSessionTimeZone),
                migrationAssembly,
                dbCfg.RetryCount ?? 5,
                dbCfg.MaxRetryDelaySeconds ?? 30,
                dbCfg.CommandTimeoutSeconds ?? 60
             );
        }

        /// <summary>
        /// Composes a list of interceptors, including default ones like <see cref="UtcSessionInterceptor"/> and <see cref="AutoIncrementConcurrencyInterceptor"/>.
        /// </summary>
        /// <param name="custom">A collection of custom interceptors provided by the caller.</param>
        /// <param name="useUtcSession">If true, adds the <see cref="UtcSessionInterceptor"/> to ensure UTC session time zone on supported databases (MySQL, PostgreSQL).</param>
        /// <returns>An <see cref="IEnumerable{IInterceptor}"/> containing both custom and default interceptors.</returns>
        private static IEnumerable<IInterceptor> ComposeInterceptors(IEnumerable<IInterceptor>? custom, bool useUtcSession)
        {
            var list = custom?.ToList() ?? new List<IInterceptor>();
            if (useUtcSession)
                list.Add(new UtcSessionInterceptor());
            list.Add(new AutoIncrementConcurrencyInterceptor());
            return list;
        }

        /// <summary>
        /// Creates a <see cref="DbContext"/> instance with detailed connection parameters.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="DbContext"/> to create.</typeparam>
        /// <param name="connectionString">The database connection string.</param>
        /// <param name="useAzure">If true, enables Azure authentication for SQL Server.</param>
        /// <param name="useManagedIdentity">If true, uses Azure Managed Identity for authentication. Requires <paramref name="useAzure"/> to be true.</param>
        /// <param name="tenantId">The Azure Tenant ID. Required for Service Principal authentication.</param>
        /// <param name="clientId">The Azure Client ID (Application ID). Used for Managed Identity (if specified) or Service Principal authentication.</param>
        /// <param name="clientSecret">The Azure Client Secret. Required for Service Principal authentication.</param>
        /// <param name="provider">The database provider type (e.g., 'sqlserver', 'postgresql', 'mysql').</param>
        /// <param name="interceptors">An optional collection of <see cref="IInterceptor"/> to be applied to the <see cref="DbContext"/>.</param>
        /// <param name="migrationAssembly">The name of the assembly containing the migrations.</param>
        /// <param name="retryCount">The maximum number of retry attempts for transient failures (SQL Server only).</param>
        /// <param name="maxRetryDelaySeconds">The maximum delay between retry attempts (SQL Server only).</param>
        /// <param name="commandTimeoutSeconds">The command timeout in seconds. Set to null to skip explicit setting.</param>
        /// <returns>A new instance of <typeparamref name="T"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if Azure authentication parameters are incomplete.</exception>
        /// <exception cref="Exception">Thrown if the provider is unknown or the <see cref="DbContext"/> instance cannot be created.</exception>
        public static T CreateDbContext<T>(
           string connectionString,
           bool useAzure = false,
           bool useManagedIdentity = false,
           string? tenantId = null,
           string? clientId = null,
           string? clientSecret = null,
           string? provider = null,
           IEnumerable<IInterceptor>? interceptors = null, //optional interceptors passed to DbContextOptionsBuilder
           string? migrationAssembly = null,
           int retryCount = 5,             // intended for azure only
           int maxRetryDelaySeconds = 30,  // intended for azure only
           int? commandTimeoutSeconds = 60 //Set null to skip.
        )
        where T : DbContext
        {
            var prov = NormalizeProvider(provider);
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            switch (prov)
            {
                case "sqlserver":
                    var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
                    var sqlConnection = new SqlConnection(sqlBuilder.ConnectionString);
                    if (useAzure)
                    {
                        var azureUrl = "https://database.windows.net/.default";
                        var scope = new TokenRequestContext([azureUrl]);
                        TokenCredential credential;
                        if (useManagedIdentity)
                        {
                            // Managed Identity mode (on cloud server):
                            if (string.IsNullOrEmpty(clientId))
                                credential = new DefaultAzureCredential();
                            else
                                credential = new ManagedIdentityCredential(clientId);
                        }
                        else
                        {
                            // Service principal (client secret) mode:
                            // Works from anywhere (local, on-prem, Azure) as long as these three are set.
                            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                                throw new ArgumentException("Azure authentication requires tenantId, clientId, and clientSecret when UseManagedIdentity is false.");
                            credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                        }
                        var accessToken = credential.GetToken(scope, CancellationToken.None);
                        sqlConnection.AccessToken = accessToken.Token;
                    }
                    optionsBuilder.UseSqlServer(sqlConnection, sql =>
                    {
                        if (commandTimeoutSeconds is { } to) sql.CommandTimeout(to);
                        if (!string.IsNullOrWhiteSpace(migrationAssembly))
                            sql.MigrationsAssembly(migrationAssembly);
                        // Auto-enable retries for Azure SQL unless explicitly disabled.
                        var shouldEnableRetries = useAzure && retryCount > 0;
                        if (shouldEnableRetries)
                        {
                            sql.EnableRetryOnFailure(
                                maxRetryCount: retryCount,
                                maxRetryDelay: TimeSpan.FromSeconds(maxRetryDelaySeconds),
                                errorNumbersToAdd: null // use built-in transient list
                            );
                        }
                    });
                    break;
                case "postgresql":
                    optionsBuilder.UseNpgsql(connectionString, npgsql =>
                    {
                        if (!string.IsNullOrWhiteSpace(migrationAssembly))
                            npgsql.MigrationsAssembly(migrationAssembly);
                    });
                    break;
                case "mysql": //version 8.0 is bare minimum for Pomelo, lesser versions require different migration output
                    throw new Exception($"MySQL is not supported by Pomelo for .NET10. Use EFCore.Boost version 9.x until then");
                    /*
                    optionsBuilder.UseMySql(
                        connectionString,
                        ServerVersion.Create(8, 0, 13, ServerType.MySql),  //8.0.13+ supports UUID()
                        mySql =>
                        {
                            if (!string.IsNullOrWhiteSpace(migrationAssembly))
                                mySql.MigrationsAssembly(migrationAssembly);
                        });
                    break;
                    */
                default:
                    throw new Exception($"Unknown or unsupported provider: {prov}");
            }
            if (interceptors != null) optionsBuilder.AddInterceptors(interceptors);
            T? t = Activator.CreateInstance(typeof(T), optionsBuilder.Options) as T;
            return t ?? throw new Exception("Unable to create SQL Instance");
        }

        /// <summary>
        /// Normalizes a provider name string to a standard internal representation.
        /// </summary>
        /// <param name="raw">The raw provider name string from configuration.</param>
        /// <returns>A normalized provider name ('sqlserver', 'postgresql', or 'mysql').</returns>
        /// <exception cref="Exception">Thrown if the provider name is unknown or unsupported.</exception>
        public static string NormalizeProvider(string? raw)
        {
            raw = raw ?? string.Empty;
            return raw.Trim().ToLowerInvariant() switch
            {
                "" or "sqlserver" or "mssql" or "mssqldb" => "sqlserver",
                "postgres" or "postgresql" or "pgsql" or "pg" => "postgresql",
                "mysql" or "mariadb" => "mysql",
                _ => throw new Exception($"Unknown provider: {raw}")
            };
        }

        /// <summary>
        /// Creates a <see cref="DbContext"/> instance specifically configured for migrations.
        /// This locates the migration assembly based on the specified factory type.
        /// </summary>
        /// <remarks>
        /// Migrations are often run from a dedicated migration project (e.g., 'MyDbCtx.Migrate') rather than the core data-model project.
        /// To ensure that database settings and <c>appsettings.json</c> are correctly loaded from the migration project's environment,
        /// the <typeparamref name="TFactory"/> type (typically a class within the migration project) is used to resolve the correct
        /// assembly and its associated resources.
        /// </remarks>
        /// <typeparam name="TContext">The type of <see cref="DbContext"/> to be migrated.</typeparam>
        /// <typeparam name="TFactory">The type of the factory or startup class in the migration project, used to locate the migration assembly.</typeparam>
        /// <param name="configuration">The <see cref="IConfiguration"/> instance.</param>
        /// <param name="connName">The connection name in the configuration.</param>
        /// <returns>A new instance of <typeparamref name="TContext"/>.</returns>
        public static TContext CreateDbContextForMigrations<TContext, TFactory>(IConfiguration configuration, string connName) where TContext : DbContext
        {
            return CreateDbContext<TContext>(configuration, connName, migrationAssembly: typeof(TFactory).Assembly.GetName().Name);
        }

        /// <summary>
        /// Builds an administrative connection string (e.g., connecting to 'master' on SQL Server or 'postgres' on PostgreSQL)
        /// based on a standard application connection string. This is useful for database creation or maintenance tasks.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> instance.</param>
        /// <param name="configName">The name of the connection configuration. If empty, uses 'DefaultAppConnName'.</param>
        /// <returns>A connection string configured for administrative access.</returns>
        /// <exception cref="Exception">Thrown if configuration or the connection string is missing.</exception>
        public static string BuildAdminConnectionString(IConfiguration configuration, string configName = "")
        {
            if (string.IsNullOrWhiteSpace(configName))
            {
                var dbConnName = configuration["DefaultAppConnName"];
                if (string.IsNullOrWhiteSpace(dbConnName))
                    throw new Exception("DefaultAppConnName missing or no value in config file !");
                configName = dbConnName;
            }
            var dbCfg = DbConnectionCfg.Get(configuration, configName);
            if (dbCfg == null || string.IsNullOrWhiteSpace(dbCfg.ConnectionString))
                throw new Exception("DbConfig for " + configName + " is missing !");
            return BuildProviderAdminConnectionString(dbCfg.ConnectionString, dbCfg.Provider);
        }

        /// <summary>
        /// Provider-specific logic to modify a connection string for administrative access.
        /// Swaps the database name to 'master' (SQL Server), 'postgres' (PostgreSQL), or empty (MySQL).
        /// </summary>
        /// <param name="connectionString">The original connection string.</param>
        /// <param name="provider">The provider name.</param>
        /// <returns>The modified connection string.</returns>
        /// <exception cref="Exception">Thrown if the provider is unknown or unsupported.</exception>
        private static string BuildProviderAdminConnectionString(string connectionString, string? provider)
        {
            var prov = NormalizeProvider(provider);
            switch (prov)
            {
                case "sqlserver":
                    var sql = new SqlConnectionStringBuilder(connectionString)
                    {
                        InitialCatalog = "master"
                    };
                    return sql.ConnectionString;
                case "postgresql":
                    var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
                    {
                        Database = "postgres"
                    };
                    return csb.ConnectionString;
                case "mysql":
                    throw new Exception($"MySQL is not supported by Pomelo for .NET10. Use EFCore.Boost version 9.x until then");
                    // MySQL is more slippery here.
                    // Often use the same server / user credentials, optionally clear DB name
                    // or switch to a configured admin database if you later support that.
                    /*
                    var my = new MySqlConnector.MySqlConnectionStringBuilder(connectionString);
                    if (!string.IsNullOrWhiteSpace(my.Database))
                        my.Database = string.Empty;
                    return my.ConnectionString;
                    */
                default:
                    throw new Exception($"Unknown or unsupported provider: {prov}");
            }
        }
    }

}
