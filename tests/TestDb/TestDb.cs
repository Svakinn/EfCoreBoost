using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using EfCore.Boost.Model;
using EfCore.Boost.Model.Attributes;

namespace TestDb
{
    /// <summary>
    /// Just a simple DbContext for testing the UOW.
    /// Here we define our code first datamodel using the Boost attributes:
    /// [DbAutoUid] for auto generated Id-keys
    /// [StrShort],[StrMrd],[StrLong],[StrCode],[Text] for broad string lengths (utilizing storing and indexing for MySQL (varchar) & MsSQL (nvarchar)), postgres defaults to citext
    /// [ViewKey] for mapping to view and defining primary key for the view (uniqueness of the entities)
    /// </summary>
    /// <param name="options"></param>
    public partial class DbTest(DbContextOptions<DbTest> options) : DbContext(options)
    {
            public DbSet<MyTable> MyTables { get; set; }
            public DbSet<MyTableRef> MyTableRefs { get; set; }
            public DbSet<MyTableRefView> MyTableRefViews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyEfBoostConventions(this, "my");  //Here is where the magic happens with each db-flavor and our Boost attributes, default schema set to "my"
            OnModelData(modelBuilder); //Data Seeder: TestDbData.cs
        }

        [Index(nameof(RowID), IsUnique = true)]
        [Index(nameof(LastChanged), IsUnique = false)]
        public class MyTable
        {
            [DbAutoUid]
            public long Id { get; set; }
            [AutoIncrementConcurrency]
            public long RowVersion { get; set; }   //This is for testing if Automatic concurrency cheks work, not that this is feelgood approach to real entities that can be updated by anyone
            [DbGuid]
            public Guid RowID { get; set; }   //For testing automatic seeding of GuIds
            [StrCode]
            public string Code { get; set; } = string.Empty;
            [Title]
            public string? Heading {  get; set; }
            [Money]
            public decimal Balance { get; set; }
            [Status]
            public int Status { get; set; }
            [Percentage]
            public decimal Discount { get; set; }
            [LastChangedUtc]
            public DateTimeOffset LastChanged { get; set; } = DateTimeOffset.UtcNow;
            [CreatedUtc]
            public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
            [ExternalRef]
            public string LastChangedBy { get; set; } = string.Empty;

            public ICollection<MyTableRef> MyTableRefs { get; set; } = [];
        }

        [Index(nameof(ParentId), IsUnique = false)]
        [Index(nameof(MyInfo), IsUnique = false)]
        public class MyTableRef
        {
            [DbAutoUid]
            public long Id { get; set; }
            [AutoIncrement]
            public long RowVersion { get; set; }  //Note this will auto-increment from previous-server value when saved
            [Required]
            public long ParentId { get; set; }
            [Name]
            public string MyInfo { get; set; } = string.Empty;
            [Money]
            public decimal Amount { get; set; }
            [CreatedUtc]
            public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
            [LastChangedUtc]
            public DateTimeOffset LastChanged { get; set; } = DateTimeOffset.UtcNow;
            [ExternalRef]
            public string LastChangedBy { get; set; } = string.Empty;

            [ForeignKey(nameof(ParentId))]
            public MyTable? MyTable { get; set; }
        }

        [ViewKey(nameof(RefId), nameof(MyId))]
        public class MyTableRefView
        {
            [ExternalRef]
            public long RefId { get; set; }
            public long MyId { get; set; }
            public long RowVersion { get; set; }
            public Guid RowID { get; set; }
            [StrCode]
            public string Code { get; set; } = string.Empty;
            [Title]
            public string? Heading { get; set; }
            [LastChangedUtc]
            public DateTimeOffset ParLastChanged { get; set; }
            [ExternalRef]
            public string ParLastChangedBy { get; set; } = string.Empty;
            [Name]
            public string MyInfo { get; set; } = string.Empty;
            [Money]
            public decimal Amount { get; set; }
            [LastChangedUtc]
            public DateTimeOffset LastChanged { get; set; }
            [CreatedUtc]
            public DateTimeOffset Created { get; set; }
            [ExternalRef]
            public string LastChangedBy { get; set; } = string.Empty;
        }
    }
}
