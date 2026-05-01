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
        [StrMed] public string Name { get; set; } = "";
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
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<FluentTestEntity>(b =>
            {
                b.Property(x => x.Id).HasDbAutoUid();
                b.Property(x => x.Name).HasStrMed();
                b.Property(x => x.Balance).HasPurposeMoney();
                b.Property(x => x.Version).HasAutoIncrementConcurrency();
                b.Property(x => x.Recipient).HasPurposeAddressRecipientName();
                b.Property(x => x.IsDeleted).HasPurposeSoftDelete();
            });

            modelBuilder.Entity<TestEntity>(b =>
            {
                // Just for chainability check
                b.Property(x => x.Name).HasPurposeName().HasStrMed();
            });

            modelBuilder.ApplyEfBoostConventions(this);
        }
    }

    [TestClass]
    public class DiscoveryTests
    {
        public class DiscoveryEntity
        {
            public int Id { get; set; }
            public string Email { get; set; } = "";
            public decimal Price { get; set; }
            public bool IsActive { get; set; }

            [StrCode] public string Code { get; set; } = "";
            [StrShort] public string Short { get; set; } = "";
            [StrMed] public string Med { get; set; } = "";
            [StrLong] public string Long { get; set; } = "";
            [Text] public string Text { get; set; } = "";

            [Percentage] public decimal Pct { get; set; }
        }

        public class DiscoveryDbContext : DbContext
        {
            public DbSet<DiscoveryEntity> DiscoveryEntities { get; set; } = null!;
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=DiscoveryDb;Trusted_Connection=True;");
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                modelBuilder.Entity<DiscoveryEntity>(b =>
                {
                    b.Property(x => x.Email).HasPurposeEmail();
                    b.Property(x => x.Price).HasPurposePrice();
                    b.Property(x => x.IsActive).HasPurposeStatus();
                });
                modelBuilder.ApplyEfBoostConventions(this);
            }
        }

        [TestMethod]
        public void Fluent_Configuration_Should_Be_Discoverable_Via_Annotations()
        {
            using var ctx = new DiscoveryDbContext();
            var emailProp = ctx.Model.FindEntityType(typeof(DiscoveryEntity))!.FindProperty(nameof(DiscoveryEntity.Email))!;
            var priceProp = ctx.Model.FindEntityType(typeof(DiscoveryEntity))!.FindProperty(nameof(DiscoveryEntity.Price))!;
            var statusProp = ctx.Model.FindEntityType(typeof(DiscoveryEntity))!.FindProperty(nameof(DiscoveryEntity.IsActive))!;

            // We use string literals for annotation names since they are internal
            Assert.IsNotNull(emailProp.FindAnnotation("EfBoost:Email"), "Email purpose annotation missing");
            Assert.IsNotNull(emailProp.FindAnnotation("EfBoost:StrLong"), "Email bucket annotation missing");
            Assert.AreEqual(512, emailProp.GetMaxLength());

            Assert.IsNotNull(priceProp.FindAnnotation("EfBoost:Price"), "Price purpose annotation missing");
            Assert.AreEqual(19, priceProp.GetPrecision());
            Assert.AreEqual(4, priceProp.GetScale());

            Assert.IsNotNull(statusProp.FindAnnotation("EfBoost:Status"), "Status purpose annotation missing");
        }

        [TestMethod]
        public void Attribute_Configuration_Should_Be_Discoverable_Via_Annotations()
        {
            using var ctx = new DiscoveryDbContext();
            var entity = ctx.Model.FindEntityType(typeof(DiscoveryEntity))!;

            var codeProp = entity.FindProperty(nameof(DiscoveryEntity.Code))!;
            var shortProp = entity.FindProperty(nameof(DiscoveryEntity.Short))!;
            var medProp = entity.FindProperty(nameof(DiscoveryEntity.Med))!;
            var longProp = entity.FindProperty(nameof(DiscoveryEntity.Long))!;
            var textProp = entity.FindProperty(nameof(DiscoveryEntity.Text))!;
            var pctProp = entity.FindProperty(nameof(DiscoveryEntity.Pct))!;

            Assert.IsNotNull(codeProp.FindAnnotation("EfBoost:StrCode"), "StrCode annotation missing");
            Assert.AreEqual(30, codeProp.GetMaxLength());

            Assert.IsNotNull(shortProp.FindAnnotation("EfBoost:StrShort"), "StrShort annotation missing");
            Assert.AreEqual(50, shortProp.GetMaxLength());

            Assert.IsNotNull(medProp.FindAnnotation("EfBoost:StrMed"), "StrMed annotation missing");
            Assert.AreEqual(256, medProp.GetMaxLength());

            Assert.IsNotNull(longProp.FindAnnotation("EfBoost:StrLong"), "StrLong annotation missing");
            Assert.AreEqual(512, longProp.GetMaxLength());

            Assert.IsNotNull(textProp.FindAnnotation("EfBoost:Text"), "Text annotation missing");
            Assert.IsNull(textProp.GetMaxLength());

            Assert.IsNotNull(pctProp.FindAnnotation("EfBoost:Percentage"), "Percentage annotation missing");
            Assert.AreEqual(18, pctProp.GetPrecision());
            Assert.AreEqual(8, pctProp.GetScale());
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
