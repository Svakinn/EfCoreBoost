using EfCore.Boost.Model;
using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;

namespace Boost.Model;



public partial class BoostXDbContext(DbContextOptions<BoostXDbContext> options) : DbContext(options)
{
    public const string DefaultSchemaName = "core";

    #region dbsets
    // For OData discoverability we need to expose the DbSet
    public DbSet<IpInfo> IpInfos { get; set; }
    #endregion

    #region onMOdelCreating

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply default EFCore.Boost conventions to the model.
        // This configures all supported attributes and sets the default schema to "core" (in this case).
        // Note: EFCore.Boost defaults to no cascade deletes, so you typically need none or far fewer
        // fluent foreign key configurations here.
        modelBuilder.ApplyEfBoostConventions(this, DefaultSchemaName);

        // Optional: add small seed data for a few basic tables.
        // Be aware this increases the size of the DbContext assembly.
        // Recommended seeding approaches:
        // - HasData: (like we demonstrate here) for small, static lookup data (migration-based)
        // - Runtime seeding: for flexible or environment-dependent data
        // - Migration SQL: for controlled, versioned data inserts
        // - EFCore.Boost based bulk import project: for larger datasets (e.g., CSV)
        OnModelData(modelBuilder); //Run our has-data migration
    }

    #endregion

    #region data model

    [Comment("Received IP numbers with reverse lookup data")]
    [Index(nameof(IpNo), IsUnique = true, Name = "IpNoIdx")]
    [Index(nameof(LastChangedUtc), AllDescending = true)]
    [Index(nameof(Processed))]
    public class IpInfo
    {
        [DbAutoUid]
        public long Id { get; set; }
        [StrShort]
        public string IpNo { get; set; } = string.Empty;
        [StrLong]
        public string? HostName { get; set; }
        public bool Processed { get; set; }
        [LastChangedUtc]
        public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    // Example view
    [ViewKey(nameof(Id))]
    public class IpInfoView
    {
        public long Id { get; set; }
        public string IpNo { get; set; } = string.Empty;
        public string? HostName { get; set; }
    }

    #endregion
}

