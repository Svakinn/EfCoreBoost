using EfCore.Boost.Model;
using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;

namespace BoostTest;

[TestClass]
public class FluentSupportTests
{
    public class TestEntity
    {
        [DbAutoUid] public long Id { get; set; }
        [StrShort] public string Name { get; set; } = "";
        [Money] public decimal Balance { get; set; }
        [AutoIncrementConcurrency] public int Version { get; set; }
        [AddressRecipientName] public string Recipient { get; set; } = "";
        [SoftDelete] public bool IsDeleted { get; set; }
    }

    public class FluentTestEntity
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Balance { get; set; }
        public int Version { get; set; }
        public string Recipient { get; set; } = "";
        public bool IsDeleted { get; set; }
    }

    public class TestDbContext : DbContext
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;
        public DbSet<FluentTestEntity> FluentTestEntities { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TestDb;Trusted_Connection=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FluentTestEntity>(b =>
            {
                b.Property(x => x.Id).HasDbAutoUid();
                b.Property(x => x.Name).HasStrShort();
                b.Property(x => x.Balance).HasMoney();
                b.Property(x => x.Version).HasAutoIncrementConcurrency();
                b.Property(x => x.Recipient).HasPurposeAddressRecipientName();
                b.Property(x => x.IsDeleted).HasPurposeSoftDelete();
            });

            modelBuilder.ApplyEfBoostConventions(this);
        }
    }

    [TestMethod]
    public void Attribute_And_Fluent_Should_Produce_Same_Metadata()
    {
        using var ctx = new TestDbContext();
        var model = ctx.Model;

        var entity = model.FindEntityType(typeof(TestEntity))!;
        var idProp = entity.FindProperty(nameof(TestEntity.Id))!;
        var nameProp = entity.FindProperty(nameof(TestEntity.Name))!;
        var balanceProp = entity.FindProperty(nameof(TestEntity.Balance))!;
        var versionProp = entity.FindProperty(nameof(TestEntity.Version))!;
        var recipientProp = entity.FindProperty(nameof(TestEntity.Recipient))!;
        var softDeleteProp = entity.FindProperty(nameof(TestEntity.IsDeleted))!;

        var fluentEntity = model.FindEntityType(typeof(FluentTestEntity))!;
        var fIdProp = fluentEntity.FindProperty(nameof(FluentTestEntity.Id))!;
        var fNameProp = fluentEntity.FindProperty(nameof(FluentTestEntity.Name))!;
        var fBalanceProp = fluentEntity.FindProperty(nameof(FluentTestEntity.Balance))!;
        var fVersionProp = fluentEntity.FindProperty(nameof(FluentTestEntity.Version))!;
        var fRecipientProp = fluentEntity.FindProperty(nameof(FluentTestEntity.Recipient))!;
        var fSoftDeleteProp = fluentEntity.FindProperty(nameof(FluentTestEntity.IsDeleted))!;

        // Assertions
        Assert.AreEqual(idProp.ValueGenerated, fIdProp.ValueGenerated);
        Assert.AreEqual(nameProp.GetMaxLength(), fNameProp.GetMaxLength());
        Assert.AreEqual(balanceProp.GetPrecision(), fBalanceProp.GetPrecision());
        Assert.AreEqual(balanceProp.GetScale(), fBalanceProp.GetScale());
        Assert.IsTrue(fVersionProp.IsConcurrencyToken);
        Assert.AreEqual(recipientProp.GetMaxLength(), fRecipientProp.GetMaxLength());
        Assert.AreEqual(256, fRecipientProp.GetMaxLength()); // StrMed is 256

        // SoftDelete should be present
        Assert.IsNotNull(softDeleteProp.FindAnnotation("EfBoost:SoftDelete"));
        Assert.IsNotNull(fSoftDeleteProp.FindAnnotation("EfBoost:SoftDelete"));

        // Check that they both have the same "ValueGenerated" and "IsConcurrencyToken"
        Assert.AreEqual(idProp.ValueGenerated, fIdProp.ValueGenerated);
        Assert.AreEqual(versionProp.IsConcurrencyToken, fVersionProp.IsConcurrencyToken);
        // Both should have same default value (1 for int/long in EnsureDefaultNumIfMissing)
        // Assert.AreEqual(versionProp.GetDefaultValue(), fVersionProp.GetDefaultValue());
        Assert.AreEqual(1, fVersionProp.GetDefaultValue());
    }
}
