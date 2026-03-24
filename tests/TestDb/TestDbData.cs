using Microsoft.EntityFrameworkCore;

namespace TestDb
{
    public partial class DbTest
    {
        private static void OnModelData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyTable>().HasData(
                new MyTable() { Id = -1, RowID = Guid.NewGuid(), LastChangedBy = "Baldr", Status = 1, Code = "BD", Balance = 350, Heading = "Baldo", Discount = 5 },
                new MyTable() { Id = -2, RowID = Guid.NewGuid(), LastChangedBy = "Stefan", Status = 2, Code = "Mn", Balance = 200, Heading = "Mando", Discount = 0  }
                );
            modelBuilder.Entity<MyTableRef>().HasData(
                new MyTableRef() { Id = -1, ParentId = -1, MyInfo = "BigData", LastChangedBy = "Baldr", Amount = 300 },
                new MyTableRef() { Id = -2, ParentId = -1, MyInfo = "BiggerData",LastChangedBy = "Baldr", Amount = 50 },
                new MyTableRef() { Id = -3, ParentId = -2, MyInfo = "OtherData", LastChangedBy = "Stefan", Amount = 200 }
            );
        }
    }
}
