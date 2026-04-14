using EfCore.Boost.Model;
using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;

namespace BoostX.Model;



/// <summary>
/// The primary database context for the BoostX application.
/// Defines the data model and configures EF Core conventions.
/// </summary>
public partial class BoostCTX(DbContextOptions<BoostCTX> options) : DbContext(options)
{
    /// <summary>
    /// The default database schema name used by this context.
    /// </summary>
    public const string DefaultSchemaName = "BoostSchemaX";

    #region dbsets
    /// <summary>
    /// Gets or sets the DbSet for IpInfo entities.
    /// </summary>
    public DbSet<IpInfo> IpInfos { get; set; }
    public DbSet<IpInfoView> IpInfoViews { get; set; }
    #endregion

    #region onMOdelCreating

    /// <summary>
    /// Configures the database model during creation.
    /// Applies EFCore.Boost conventions and seed data.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model.</param>
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

    /// <summary>
    /// Represents an entry of IP information with reverse lookup data.
    /// </summary>
    [Comment("Received IP numbers with reverse lookup data")]
    [Index(nameof(IpNo), IsUnique = true, Name = "IpNoIdx")]
    [Index(nameof(LastChangedUtc), AllDescending = true)]
    [Index(nameof(Processed))]
    public class IpInfo
    {
        /// <summary>
        /// Gets or sets the unique identifier for the IP information record.
        /// </summary>
        [DbAutoUid]
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the IP number string.
        /// </summary>
        [StrShort]
        public string IpNo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resolved host name for the IP, if available.
        /// </summary>
        [StrLong]
        public string? HostName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the IP information has been processed.
        /// </summary>
        public bool Processed { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the record was last changed.
        /// </summary>
        [LastChangedUtc]
        public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Represents a view-optimized model for IP information.
    /// </summary>
    [ViewKey(nameof(Id))]
    public class IpInfoView
    {
        /// <summary>
        /// Gets or sets the ID of the record.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the IP number.
        /// </summary>
        public string IpNo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the host name.
        /// </summary>
        public string? HostName { get; set; }
    }

    #endregion
}

