// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Azure.Core;
using Azure.Identity;
using EfCore.Boost.CFG;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Npgsql.EntityFrameworkCore.PostgreSQL; // Optional, but sometimes helps with tooling
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost
{
    /// <summary>
    /// Tool we rely on to handle database connections and connection settings
    /// You just throw in connection name to be used in the appsettings.json (or the default one is picked up)
    /// This handles Azure authentication too, if needed
    /// </summary>
    public static class SecureContextFactory
    {
        public static T CreateDbContext<T>(IConfiguration configuration, string configName = "", IEnumerable<IInterceptor>? interceptors = null) where T : DbContext
        { 
            if (string.IsNullOrWhiteSpace(configName))
            {
                var dbConnName = configuration.GetValue<string>("DefaultAppConnName");
                if (string.IsNullOrWhiteSpace(dbConnName))
                    throw new Exception("DefaultAppConnName missing or no value in config file !");
                configName = dbConnName;
            }
            var dbCfg = DbConnectionCFG.Get(configuration, configName);
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
                dbCfg.RetryCount ?? 5,
                dbCfg.MaxRetryDelaySeconds ?? 30,
                dbCfg.CommandTimeoutSeconds ?? 60
             );
        }

        /// <summary>
        /// This is a part of our convension to store times in UTC in the database.
        /// Some database types (MySql and Postgres) require us to set the session time zone to UTC on connection open, for consistency.
        /// </summary>
        /// <param name="custom"></param>
        /// <param name="useUtcSession"></param>
        /// <returns></returns>
        private static IEnumerable<IInterceptor>? ComposeInterceptors(IEnumerable<IInterceptor>? custom, bool useUtcSession)
        {
            var list = custom?.ToList() ?? new List<IInterceptor>();
            if (useUtcSession)
                list.Add(new UtcSessionInterceptor());
            return list;
        }

        public static T CreateDbContext<T>(
           string connectionString,
           bool useAzure = false,
           bool useManagedIdentity = false,
           string? tenantId = null,
           string? clientId = null,
           string? clientSecret = null,
           string? provider = null,
           IEnumerable<IInterceptor>? interceptors = null, //optional interceptors pased to DbContextOptionsBuilder
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
                        var scope = new TokenRequestContext(new[] { azureUrl });
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
                        if (commandTimeoutSeconds is int to) sql.CommandTimeout(to);
                        // Auto enable retries for Azure SQL unless explicitly disabled.
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
                    optionsBuilder.UseNpgsql(connectionString); //.
                        //EnableSensitiveDataLogging().LogTo(Console.WriteLine, LogLevel.Debug); //For debugging only 
                    break;
                case "mysql": //version 8.0 is bare minimum for Pomelo, lesser versions require different migration output
                    optionsBuilder.UseMySql(connectionString, ServerVersion.Create(8, 0, 0, ServerType.MySql)); 
                    break;
                default:
                    throw new Exception($"Unknown or unsupported provider: {prov}");
            }
            if (interceptors != null) optionsBuilder.AddInterceptors(interceptors);
            T? t = Activator.CreateInstance(typeof(T), optionsBuilder.Options) as T;
            return t ?? throw new Exception("Unable to create SQL Instance");
        }

        public static string NormalizeProvider(string? raw)
        { 
            raw = raw ?? string.Empty;
            return raw?.Trim().ToLowerInvariant() switch
            {
                "" or "sqlserver" or "mssql" or "mssqldb" => "sqlserver",
                "postgres" or "postgresql" or "pgsql" or "pg" => "postgresql",
                "mysql" or "mariadb" => "mysql",
                _ => throw new Exception($"Unknown provider: {raw}")
            };
        }
    }


}
