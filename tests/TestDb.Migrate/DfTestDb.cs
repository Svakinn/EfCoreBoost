using EfCore.Boost;
using EfCore.Boost.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace TestDb.Migrate
{
    /// <summary>
    /// We need data-factory for EF-code generation using dotnet ef migrations commands
    /// The SecureContextfactory handles picking upp connection strings and settings, sutable for each db-flavour or azure
    /// Or you pass in the connecton name: "--connName".
    /// </summary>
    public sealed class DbTestContextFactory : DesignDbContextFactoryBase<DbTest>
    {
        protected override DbTest CreateContext(IConfigurationRoot configuration, string connName)
            => SecureContextFactory.CreateDbContextForMigrations<DbTest, DbTestContextFactory>(configuration, connName);
    }
}
