using Azure.Core;
using Azure.Identity;
using DbRepo.CFG;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql.EntityFrameworkCore.PostgreSQL; // Optional, but sometimes helps with tooling
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.Extensions.Logging;

namespace DbRepo
{
    public static class SecureContextFactory
    {
        public static T CreateDbContext<T>(IConfiguration configuration, string configName = "") where T : DbContext
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
                dbCfg.Provider
                );
        }

        public static T CreateDbContext<T>(
           string connectionString,
           bool useAzure = false,
           bool useManagedIdentity = false,
           string? tenantId = null,
           string? clientId = null,
           string? clientSecret = null,
           string? provider = null
        ) 
        where T : DbContext
        {
            var prov = NormalizeProvider(provider);
            //Console.WriteLine($"[SecureContextFactory] Creating context for provider: {prov}");
            //Console.WriteLine($"[SecureContextFactory] Connection string: {connectionString}");
            var optionsBuilder = new DbContextOptionsBuilder<T>();
            switch (prov)
            {
                case "sqlserver":
                    var sqlBuilder = new SqlConnectionStringBuilder(connectionString);
                    var sqlConnection = new SqlConnection(sqlBuilder.ConnectionString);
                    if (useAzure)
                    {
                        var azureUrl = "https://database.windows.net/.default";
                        if (useManagedIdentity)
                        {
                            if (string.IsNullOrEmpty(clientId))
                                sqlConnection.AccessToken = new DefaultAzureCredential().GetToken(new TokenRequestContext([azureUrl])).Token;
                            else
                                sqlConnection.AccessToken = new ManagedIdentityCredential(clientId).GetToken(new TokenRequestContext([azureUrl])).Token;
                        }
                        else
                        {
                            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                                throw new ArgumentException("Azure authentication requires tenantId, clientId, and clientSecret.");
                            sqlConnection.AccessToken = new ClientSecretCredential(tenantId, clientId, clientSecret).GetToken(new TokenRequestContext([azureUrl])).Token;
                        }
                    }
                    optionsBuilder.UseSqlServer(sqlConnection); 
                    break;
                case "postgresql":
                    optionsBuilder.UseNpgsql(connectionString).
                        EnableSensitiveDataLogging(). //For debugging just now
                        LogTo(Console.WriteLine, LogLevel.Debug); 
                    break;
                case "mysql":
                    optionsBuilder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); 
                    break;
                default:
                    throw new Exception($"Unknown or unsupported provider: {prov}");
            }

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
