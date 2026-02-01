using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using EfCore.Boost.Model;
using EfCore.Boost.Model.Attributes;

namespace BoostTest.TestDb
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
            OnModelData(modelBuilder); //Data Seader: TestDbData.cs
        }

        [Index(nameof(RowID), IsUnique = true)]
        [Index(nameof(LastChanged), IsUnique = false)]
        public class MyTable
        {
            [DbAutoUid]
            public long Id { get; set; }

            public Guid RowID { get; set; }

            public DateTimeOffset LastChanged { get; set; }

            [StrShort]
            public string LastChangedBy { get; set; } = string.Empty;

            public ICollection<MyTableRef> MyTableRefs { get; set; } = [];
        }

        [Index(nameof(ParentId), IsUnique = false)]
        [Index(nameof(MyInfo), IsUnique = false)]
        public class MyTableRef
        {
            [DbAutoUid]
            public long Id { get; set; }

            [Required]
            public long ParentId { get; set; }
            [StrMed]
            public string MyInfo { get; set; } = string.Empty;

            public DateTimeOffset LastChanged { get; set; }

            [StrShort]
            public string LastChangedBy { get; set; } = string.Empty;

            [ForeignKey(nameof(ParentId))]
            public MyTable? MyTable { get; set; }
        }

        [ViewKey(nameof(RefId), nameof(MyId))]
        public class MyTableRefView
        {
            public long RefId { get; set; }

            public long MyId { get; set; }

            public Guid RowID { get; set; }

            public DateTimeOffset LastChanged { get; set; }

            [StrShort]
            public string LastChangedBy { get; set; } = string.Empty;

            [StrMed]
            public string MyInfo { get; set; } = string.Empty;

            public DateTimeOffset RefLastChanged { get; set; }

            [StrShort]
            public string RefLastChangedBy { get; set; } = string.Empty;
        }
    }
}
