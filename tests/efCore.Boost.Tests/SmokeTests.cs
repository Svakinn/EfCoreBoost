using FluentAssertions;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace efCore.Boost.Tests;

public class SmokeTests
{
    public class Item { public int Id {get;set;} public string Name {get;set;} = ""; }

    public class AppDb: DbContext
    {
        public AppDb(DbContextOptions<AppDb> o): base(o) {}
        public DbSet<Item> Items => Set<Item>();
        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Item>(e=>{
                e.HasKey(x=>x.Id);
                e.Property(x=>x.Name).HasMaxLength(128).IsRequired();
                e.HasIndex(x=>x.Name).IsUnique();
            });
        }
    }

    [Fact]
    public async Task Sqlite_inmemory_enforces_unique_index()
    {
        using var conn = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<AppDb>().UseSqlite(conn).Options;
        using var db = new AppDb(opts);
        db.Database.EnsureCreated();
        db.Add(new Item{ Name="A"}); await db.SaveChangesAsync();
        db.Add(new Item{ Name="A"});
        await db.Invoking(d=>d.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task InMemory_provider_is_fast_for_logic()
    {
        var opts = new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase("test").Options;
        using var db = new AppDb(opts);
        db.AddRange(new Item{ Name="A"}, new Item{ Name="B"}); await db.SaveChangesAsync();
        var count = await db.Items.CountAsync();
        count.Should().Be(2);
    }
}
