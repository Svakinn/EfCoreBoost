using BoostX.Model;
using EfCore.Boost;
using EfCore.Boost.Design;
using Microsoft.Extensions.Configuration;

namespace BoostX.Migrate;

    /// <summary>
    /// We need data-factory for EF-code generation using dotnet ef migrations commands
    /// The SecureContextfactory handles picking upp connection strings and settings, sutable for each db-flavour or azure
    /// Or you pass in the connecton name: "--connName".
    /// </summary>
    public sealed class DbBoostXContextFactory : DesignDbContextFactoryBase<BoostXDbContext>
    {
        protected override BoostXDbContext CreateContext(IConfigurationRoot configuration, string connName)
            => SecureContextFactory.CreateDbContextForMigrations<BoostXDbContext, DbBoostXContextFactory>(configuration, connName);
    }

