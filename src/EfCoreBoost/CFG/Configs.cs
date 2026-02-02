using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;

namespace EfCore.Boost.CFG
{
    public static class ConnectionStringCfg
    {
        public static string? Get(IConfiguration configuration, string cfgName)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(cfgName)) return null;
            var v = configuration.GetSection("ConnectionStrings")[cfgName];
            return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        }
    }

    public static class DbConnectionCFG
    {
        public static DbConnectionInfo? Get(IConfiguration configuration, string cfgName)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            if (string.IsNullOrWhiteSpace(cfgName)) return null;
            var s = configuration.GetSection("DBConnections").GetSection(cfgName);
            if (!s.Exists()) return null;
            var ret = new DbConnectionInfo();
            ret.ConnectionString = CfgRead.Str(s, "ConnectionString") ?? string.Empty;
            ret.Provider = CfgRead.Str(s, "Provider") ?? string.Empty;
            ret.UseAzure = CfgRead.Bool(s, "UseAzure") ?? false;
            ret.UseManagedIdentity = CfgRead.Bool(s, "UseManagedIdentity") ?? false;
            ret.AzureTenantId = CfgRead.Str(s, "AzureTenantId") ?? string.Empty;
            ret.AzureClientId = CfgRead.Str(s, "AzureClientId") ?? string.Empty;
            ret.AzureClientSecret = CfgRead.Str(s, "AzureClientSecret") ?? string.Empty;
            ret.CommandTimeoutSeconds = CfgRead.Int(s, "CommandTimeoutSeconds");
            ret.RetryCount = CfgRead.Int(s, "RetryCount") ?? ret.RetryCount;
            ret.MaxRetryDelaySeconds = CfgRead.Int(s, "RetryDelaySeconds") ?? CfgRead.Int(s, "MaxRetryDelaySeconds") ?? ret.MaxRetryDelaySeconds;
            ret.UseUtcSessionTimeZone = CfgRead.Bool(s, "UseUtcSessionTimeZone") ?? ret.UseUtcSessionTimeZone;
            return ret;
        }
    }

    internal static class CfgRead
    {
        public static string? Str(IConfigurationSection s, string key)
        {
            var v = s[key];
            if (string.IsNullOrWhiteSpace(v)) return null;
            return v.Trim();
        }

        public static bool? Bool(IConfigurationSection s, string key)
        {
            var v = Str(s, key);
            if (v == null) return null;
            if (bool.TryParse(v, out var b)) return b;
            if (v == "1") return true;
            if (v == "0") return false;
            return null;
        }

        public static int? Int(IConfigurationSection s, string key)
        {
            var v = Str(s, key);
            if (v == null) return null;
            if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return i;
            return null;
        }
    }

    public sealed class DbConnectionInfo
    {
        public string ConnectionString { get; set; } = string.Empty;
        public bool UseAzure { get; set; } = false;
        public bool UseManagedIdentity { get; set; } = false;
        public string AzureTenantId { get; set; } = string.Empty;
        public string AzureClientId { get; set; } = string.Empty;
        public string AzureClientSecret { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty; // SqlServer, Postgres, MySql
        public int? CommandTimeoutSeconds { get; set; } = null;
        public int? RetryCount { get; set; } = 5;
        public int? MaxRetryDelaySeconds { get; set; } = 30;
        public bool UseUtcSessionTimeZone { get; set; } = true;
    }
}
