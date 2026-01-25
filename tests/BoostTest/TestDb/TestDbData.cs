using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BoostTest.TestDb
{
    public partial class DbTest : DbContext
    {
        protected static void OnModelData(ModelBuilder modelBuilder)
        {
            DateTimeOffset eD = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            modelBuilder.Entity<MyTable>().HasData(
                new MyTable() { Id = -1, RowID = Guid.NewGuid(), LastChanged = eD, LastChangedBy = "Baldr" },
                new MyTable() { Id = -2, RowID = Guid.NewGuid(), LastChanged = eD, LastChangedBy = "Stefan" }
                );
            modelBuilder.Entity<MyTableRef>().HasData(
                new MyTableRef() { Id = -1, ParentId = -1, MyInfo = "BigData", LastChanged = eD, LastChangedBy = "Baldr" },
                new MyTableRef() { Id = -2, ParentId = -1, MyInfo = "BiggerData", LastChanged = eD, LastChangedBy = "Baldr" },
                new MyTableRef() { Id = -3, ParentId = -2, MyInfo = "OtherData", LastChanged = eD, LastChangedBy = "Stefan" }
            );
        }
    } 
}
