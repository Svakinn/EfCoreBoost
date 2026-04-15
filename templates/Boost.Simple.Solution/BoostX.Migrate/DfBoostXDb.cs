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
public sealed class BoostCTXFactory : DesignDbContextFactoryBase<BoostCTX>
{
    /// <summary>
    /// Creates a new instance of the DbContext using configuration.
    /// </summary>
    /// <param name="configuration">The configuration root.</param>
    /// <param name="connName">The connection name to use.</param>
    /// <returns>A configured BoostCTX instance.</returns>
    protected override BoostCTX CreateContext(IConfigurationRoot configuration, string connName)
        => SecureContextFactory.CreateDbContextForMigrations<BoostCTX, BoostCTXFactory>(configuration, connName);
}

