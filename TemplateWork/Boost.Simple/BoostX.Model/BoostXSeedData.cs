using Microsoft.EntityFrameworkCore;

namespace BoostX.Model;

public partial class BoostCTX
{
    /// <summary>
    /// Configures seed data for the database model.
    /// HasData is fine for small/static data but is baked into migrations and can bloat the model.
    /// For real datasets, prefer external seeding. A minimal example is shown in the model project.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    private static void OnModelData(ModelBuilder modelBuilder)
    {
        // For auto-generated id's we use negative values to avoid conflicts with none-seeded data
        DateTimeOffset eD = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<IpInfo>().HasData(
            new IpInfo() { Id = -1, HostName = "Localhost", IpNo = "127.0.0.1", LastChangedUtc = eD, Processed = true }
            );
    }
}

