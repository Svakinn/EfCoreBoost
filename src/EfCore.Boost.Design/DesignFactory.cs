using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EfCore.Boost.Design
{
    public abstract class DesignDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
        where TContext : DbContext
    {
        public TContext CreateDbContext(string[] args)
        {
            string connName = GetArg(args, "--connName") ?? GetDefaultConnectionName();

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

            return CreateContext(configuration, connName);
        }

        protected abstract string GetDefaultConnectionName();

        protected abstract TContext CreateContext(IConfigurationRoot configuration, string connName);

        protected static string? GetArg(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : null;

                if (args[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                    return args[i][(key.Length + 1)..];
            }
            return null;
        }
    }
}