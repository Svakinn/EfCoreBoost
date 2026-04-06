using Microsoft.Extensions.Configuration;

namespace BoostX.Migrate
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("--- BoostX Database Migration Utility ---");
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("AppSettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // Usage example:
            // dotnet BoostX.Migrate MyAppConnName [import|check|CreateDb|Migrate]
            // If connection name is omitted, it will be read from AppSettings.json["DefaultAppConnName"]

            // Determine connection name from args or config
            string connName = args.Length > 0 ? args[0] : configuration["DefaultAppConnName"] ?? "";

            if (string.IsNullOrWhiteSpace(connName))
            {
                Console.WriteLine("Error: Connection name not specified. Provide it as an argument or set 'DefaultAppConnName' in AppSettings.json.");
                return;
            }

            // Command logic: second argument after connection name (optional)
            string command = args.Length > 1 ? args[1].ToLowerInvariant() : "check";

            Console.WriteLine($"Using connection: {connName}");
            Console.WriteLine($"Command: {command}");


        }
    }
}
