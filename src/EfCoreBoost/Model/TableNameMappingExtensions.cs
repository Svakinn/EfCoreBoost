using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.Model
{
    public static class TableNameMappingExtensions
    {
        /// <summary>
        /// Mainly for MySql that has no schema built in.
        /// We map the DB-tablenames to our classes with the schema context, (actual scema or as part of the table name)
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="entityType"></param>
        /// <param name="table"></param>
        /// <param name="schema"></param>
        /// <param name="hasNoSchema"></param>
        public static void MapTable(this ModelBuilder modelBuilder, Type entityType, string table, string schema, bool hasNoSchema)
        {
            if (hasNoSchema)
                modelBuilder.Entity(entityType).ToTable($"{schema}_{table}");
            else
                modelBuilder.Entity(entityType).ToTable(table, schema);
        }

        public static void MapTable<TEntity>(this ModelBuilder modelBuilder, string table, string schema, bool hasNoSchema)
            where TEntity : class
        {
            if (hasNoSchema)
                modelBuilder.Entity<TEntity>().ToTable($"{schema}_{table}");
            else
                modelBuilder.Entity<TEntity>().ToTable(table, schema);
        }
    }
}
