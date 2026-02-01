using EfCore.Boost;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BoostTest.TestDb
{
    /// <summary>
    /// We need data-factory for EF-code generation using dotnet ef migrations commands
    /// The SecureContextfactory handles picking upp connection strings and settings, sutable for each db-flavour or azure
    /// </summary>
    public class DbTestContextFactory : IDesignTimeDbContextFactory<DbTest>
    {
        public DbTest CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true, reloadOnChange: true).Build();
            return SecureContextFactory.CreateDbContext<DbTest>(configuration);
        }
    }
}
