using System.Data;
using System.Runtime.CompilerServices;
using EfCore.Boost.DbRepo;
using Microsoft.EntityFrameworkCore;

namespace TestDb
{
    public partial class DbTest
    {
        /// <summary>
        /// For seed data, normal postgres date interceptors are not run, so we need our own here
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        static DateTime PgUtcSeed(DbContext ctx, DateTime dt)
        {
            var isPg = ctx.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false;
            if (isPg)
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return dt;
        }

        private static void OnModelData(ModelBuilder modelBuilder, DbContext ctx)
        {
            var Dn = PgUtcSeed(ctx, DateTime.UtcNow);
            modelBuilder.Entity<MyTable>().HasData(
                new MyTable() { Id = -1, RowID = Guid.NewGuid(), LastChangedBy = "Baldr", Status = 1, Code = "BD", Balance = 350, Heading = "Baldo", Discount = 5, LastChanged = Dn, Created = Dn },
                new MyTable() { Id = -2, RowID = Guid.NewGuid(), LastChangedBy = "Stefan", Status = 2, Code = "Mn", Balance = 200, Heading = "Mando", Discount = 0, LastChanged = Dn, Created = Dn }
                );
            modelBuilder.Entity<MyTableRef>().HasData(
                new MyTableRef() { Id = -1, ParentId = -1, MyInfo = "BigData", LastChangedBy = "Baldr", Amount = 300, LastChanged = Dn, Created = Dn },
                new MyTableRef() { Id = -2, ParentId = -1, MyInfo = "BiggerData",LastChangedBy = "Baldr", Amount = 50, LastChanged = Dn, Created = Dn },
                new MyTableRef() { Id = -3, ParentId = -2, MyInfo = "OtherData", LastChangedBy = "Stefan", Amount = 200, LastChanged = Dn, Created = Dn }
            );
        }
    }
}
