using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace EfCore.Boost.Design
{
    /// <summary>
    /// Base factory used by EF Core design-time tools to create a <see cref="DbContext"/>
    /// instance for migrations and other design-time operations.
    ///
    /// The database connection is selected using the command line option:
    /// <c>--connName</c>.
    ///
    /// If no command line option is provided, the connection name is resolved from
    /// <c>DefaultAppConnName</c> in <c>appsettings.json</c>.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <see cref="DbContext"/> type that this factory creates.
    /// </typeparam>
    public abstract class DesignDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
        where TContext : DbContext
    {
        /// <summary>
        /// Creates the design-time <see cref="DbContext"/> instance used by EF Core tools
        /// such as <c>dotnet ef migrations</c>.
        /// </summary>
        /// <param name="args">
        /// Command line arguments passed by the EF Core tooling.
        /// These may include the <c>--connName</c> parameter.
        /// </param>
        /// <returns>
        /// A configured instance of <typeparamref name="TContext"/>.
        /// </returns>
        public TContext CreateDbContext(string[] args)
        {
            var cfg = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string connName = GetArg(args, "--connName") ?? GetDefaultConnectionName(cfg);

            return CreateContext(cfg, connName);
        }

        /// <summary>
        /// Resolves the default connection name when no <c>--connName</c> argument is provided.
        /// </summary>
        /// <param name="configuration">
        /// The configuration used to read application settings.
        /// </param>
        /// <returns>
        /// The default connection name, or an empty string if no default is configured.
        /// </returns>
        protected virtual string GetDefaultConnectionName(IConfiguration configuration)
        {
            return configuration["DefaultAppConnName"] ?? "";
        }

        /// <summary>
        /// Creates the concrete <see cref="DbContext"/> instance for design-time operations.
        /// </summary>
        /// <param name="configuration">
        /// The application configuration loaded for the current environment.
        /// </param>
        /// <param name="connName">
        /// The name of the connection string to use.
        /// </param>
        /// <returns>
        /// A configured instance of <typeparamref name="TContext"/>.
        /// </returns>
        protected abstract TContext CreateContext(IConfigurationRoot configuration, string connName);


        /// <summary>
        /// Retrieves the value of a command line argument by key.
        /// </summary>
        /// <param name="args">
        /// The command line arguments to inspect.
        /// </param>
        /// <param name="key">
        /// The argument name to search for, such as <c>--connName</c>.
        /// </param>
        /// <returns>
        /// The argument value if found; otherwise, <see langword="null"/>.
        /// </returns>
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
