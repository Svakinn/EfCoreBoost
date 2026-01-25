using System;
using System.Reflection;
using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
namespace EfCore.Boost.Model
{
    public static class SchemaExtensions
    {
        /// <summary>
        /// Applies schema + table/view naming based on [DbSchema] and [IsView].
        /// MySQL: uses "schema_name" for both tables and views (no real schema).
        /// SQL Server / PostgreSQL: uses schema + table/view names.
        /// </summary>
        public static void ApplySchemaAndViewMapping(this ModelBuilder modelBuilder, DbContext ctx, string? defaultSchema = null)
        {
            var provider = ctx.Database.ProviderName ?? string.Empty;
            var isMySql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);
            var isNpgsql = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
            var isSqlServer = provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);

            // Default schema per provider if caller didn’t specify
            if (string.IsNullOrEmpty(defaultSchema))
            {
                if (isSqlServer) defaultSchema = "dbo";
                else if (isNpgsql) defaultSchema = "public";
                else defaultSchema = null; // MySQL etc.
            }

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entity.ClrType;
                if (clr == null) continue;

                var isViewKeyAttr = clr.GetCustomAttribute<ViewKeyAttribute>();
                var schemaAttr = clr.GetCustomAttribute<DbSchemaAttribute>();
                var isView = isViewKeyAttr != null;
                var schema = schemaAttr?.Schema ?? defaultSchema;
                var baseName = schemaAttr?.Table ?? clr.Name;
                if (isMySql)
                {
                    // MySQL: no schema; use prefix when schema is present
                    var finalName = !string.IsNullOrEmpty(schema) ? $"{schema}_{baseName}" : baseName;
                    if (isView)
                    {
                        entity.SetViewName(finalName);
                        entity.SetViewSchema(null);
                        entity.SetTableName(null);
                        entity.SetSchema(null);
                    }
                    else
                    {
                        entity.SetTableName(finalName);
                        entity.SetSchema(null);
                    }                    
                }
                else
                {
                    // SqlServer / PostgreSQL (and similar)
                    if (isView)
                    {
                        entity.SetViewName(baseName);
                        entity.SetViewSchema(schema);
                        entity.SetTableName(null);
                        entity.SetSchema(null);
                    }
                    else
                    {
                        entity.SetTableName(baseName);
                        entity.SetSchema(schema);
                    }

                }
            }
        }
    }
}
