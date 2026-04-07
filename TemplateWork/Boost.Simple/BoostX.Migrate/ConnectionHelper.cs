using EfCore.Boost.CFG;
using EfCore.Boost;
using Microsoft.Extensions.Configuration;

namespace BoostX.Migrate;

/// <summary>
/// Provides utility methods for connection string manipulation and analysis.
/// Handles tasks like identifying 'master/postgres' databases and extracting connection string parts.
/// </summary>
public static class ConnectionHelper
{
    /// <summary>
    /// Attempts to find or generate a connection name suitable for database creation.
    /// It looks for existing connections pointing to the same server that are system/creation databases.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="connName">The original application database connection name.</param>
    /// <returns>The connection name for the creation database (e.g., 'MyConnCreate').</returns>
    public static string GetCreateConnectionName(IConfiguration configuration, string connName)
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

    /// <summary>
    /// Determines if a given connection string points to a database creation/system database (like 'master' or 'postgres').
    /// </summary>
    /// <param name="connectionString">The connection string to analyze.</param>
    /// <param name="provider">The database provider (SqlServer, PostgreSql, MySql).</param>
    /// <returns>True if it is a creation database connection string; otherwise, false.</returns>
    public static bool IsCreateConnectionString(string connectionString, string provider)
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

    /// <summary>
    /// Compares two connection strings to determine if they point to the same database server.
    /// </summary>
    /// <param name="connStr1">The first connection string.</param>
    /// <param name="connStr2">The second connection string.</param>
    /// <param name="provider">The database provider.</param>
    /// <returns>True if they appear to point to the same server; otherwise, false.</returns>
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

    /// <summary>
    /// Extracts a specific part (key-value pair) from a connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to parse.</param>
    /// <param name="key">The key to look for (e.g., 'Data Source', 'Host').</param>
    /// <returns>The value of the specified key, or an empty string if not found.</returns>
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
}
