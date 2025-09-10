using Microsoft.Extensions.Configuration;
using Microsoft.Spatial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace DbRepo.CFG
{
    /// <summary>
    /// Helpers for common config gets from the appsettings.json file
    /// Few datatypes for configs also supplied
    /// </summary>
    public class CFGBase<T>(IConfiguration configuration, string sectionName, string cfgName) where T : class
    {

#pragma warning disable CS0693 // Type parameter has the same name as the type parameter from outer type
        /// <summary>
        /// Get row of type T from the config
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T? GetVal<T>()
        {
            var s1 = this._configuration.GetSection(this.SectionName);
            var s2 = s1.GetSection(this.GfgName);
            var ret = s2.Get<T>();
            return ret;
        }
#pragma warning restore CS0693 // Type parameter has the same name as the type parameter from outer type

        protected readonly IConfiguration _configuration = configuration;
        protected string SectionName = sectionName;
        protected string GfgName { get; set; } = cfgName;
    }

    /// <summary>
    /// Getter to retreive connecton string, by name, from the config
    /// We expect the connection string to be under the "ConnectionStrings" section
    /// Note: this is for simple connection strings, use the DbConfigurationCFG for connections that may require security context (Azure)
    /// One practical tip: you can and should use environmental variables on prod system, override sensitive data like connection string
    /// This works both on all plattforms (windows/azure/linux)
    /// Example for overriding connection string for the db named "svak" you would set it like this with windows command:
    /// set ConnectionStrings__Svak=Server=prod;Database=Svak2;User Id=sa;Password=secret;
    /// On windows server on prem (running app under iis) use powerhell to log-in as the iis-user and then set the variable:
    ///    1. Start-Process powershell.exe -Credential (Get-Credential)
    ///    2. [Environment]::SetEnvironmentVariable("ConnectionStrings__Svak", "Server=prod;Database=Svak2;User Id=svc;Password=secret;", "User")
    /// </summary>
    public static class ConnectionStringCfg {
        public static string? Get(IConfiguration configuration, string cfgName) {
            var obj = new CFGBase<string>(configuration, "ConnectionStrings", cfgName);
            return obj.GetVal<string>();
        }
    }

    /// <summary>
    /// Get data service info, by name from the config
    /// We expect the data service info to be under the "DataServices" section
    /// One practical tip: you can and should use environmental variables on prod system, override sensitive data like connection string
    /// This works both on all plattforms (windows/azure/linux)
    /// Example for overriding connection string for the db named "svak" you would set it like this with windows command:
    ///  set DBConnections__Svak__ConnectionString=Server=prod;Database=Svak2;User Id=sa;Password=secret;
    /// On windows server on prem (running app under iis) use powerhell to log-in as the iis-user and then set the variable:
    ///    1. Start-Process powershell.exe -Credential (Get-Credential)
    ///    2. [Environment]::SetEnvironmentVariable("DBConnections__Svak__ConnectionString", "Server=prod;Database=Svak2;User Id=svc;Password=secret;", "User")
    /// </summary>
    public static class DbConnectionCFG
    {
        public static DbConnectionInfo? Get(IConfiguration configuration, string cfgName)
        {
            var obj = new CFGBase<DbConnectionInfo>(configuration, "DBConnections", cfgName);
            return obj.GetVal<DbConnectionInfo>();
        }
    }

    public class DbConnectionInfo
    {
        public string ConnectionString { get; set; } = string.Empty;
        public bool UseAzure { get; set; } = false;
        public bool UseManagedIdentity { get; set; } = false;   
        public string AzureTenantId { get; set; } = string.Empty;
        public string AzureClientId { get; set; } = string.Empty;
        public string AzureClientSecret { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;  //SqlServer, Postgres or MySql, defaults to SqlServer if empty

    }

}
