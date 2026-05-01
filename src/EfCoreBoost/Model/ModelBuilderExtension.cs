using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using static EfCore.Boost.Model.Attributes.EfForeignKeyAttributes;

namespace EfCore.Boost.Model
{
    public static class ModelBuilderExtension
    {
        /// <summary>
        /// Call this in every model builder, this maps columns and sets appropriate db-types as needed for each database flavor.
        /// For MySql schema will be built into the table name (since not supported by Mysql)
        /// For Postgres we switch from using nvarchar(x) to citext
        /// For Postgres we also need to unify usage of DateTime -> DateTime will be "timestamp without time zone"
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="ctx"></param>
        /// <param name="defaultSchema">Schema attribute will override this one if empty defaults to each db flavor normally uses i.e. 'dbo' for mssql.</param>
        public static void ApplyEfBoostConventions( this ModelBuilder modelBuilder,DbContext ctx,string? defaultSchema = null  )
        {
            var provider = ctx.Database.ProviderName ?? string.Empty;
            var isPg = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
            if (isPg)
            {
                modelBuilder.HasPostgresExtension("citext");
                modelBuilder.HasPostgresExtension("pgcrypto");
                ApplyPostgresCitext(modelBuilder);
                ApplyPostgresDateTimeFix(modelBuilder);
            }
            // For our custom attributes for schema and database generated guids:
            modelBuilder.ApplyViewKeyConvention();
            // Schema + table mapping
            modelBuilder.ApplySchemaAndViewMapping(ctx, defaultSchema);
            // Column attributes, string-lengths, DbGuid, DbAutoUid etc
            modelBuilder.ApplyBoostColumnConventions(ctx);
            //Week foreign key with no cascading deletes
            //modelBuilder.ApplyNoCascadeDelete();
            //Reverse the EF CasadedDelets convention -> no cascading deletes applied by db, unless specifically requested by attribute ([CascadeDelete])
            modelBuilder.DisableAllCascadeDeletes();
        }

        private static void ApplyPostgresCitext(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                foreach (var prop in entity.GetProperties())
                {
                    if (prop.ClrType != typeof(string)) continue;
                    if (!string.IsNullOrEmpty(prop.GetColumnType())) continue;
                    prop.SetColumnType("citext");
                }
        }

        internal static void ApplyPostgresDateTimeFix(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entityType.ClrType;
                if (Attribute.IsDefined(clr, typeof(ViewKeyAttribute), false))
                    continue; // skip views
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                        property.SetColumnType("timestamp without time zone");
                }
            }
        }

        /// <summary>
        /// By default, EF forces cascade delete on all none-nullable foreign keys (If parent is deleted, kill all the children)
        /// This is quite dangerous behavior, and we would rather have error from db, preventing parent to be deleted.
        /// Applying this method reverses this default, unless a special-custom attribute [CascadeDelete] is placed on the navigation object along with the foreign key.
        /// </summary>
        /// <param name="modelBuilder"></param>
        internal static void DisableAllCascadeDeletes(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var fk in entityType.GetForeignKeys())
                {
                    if (fk.IsOwnership)
                        continue;
                    var nav = fk.DependentToPrincipal?.PropertyInfo;
                    var hasCascadeAttr = nav != null &&
                        Attribute.IsDefined(nav, typeof(CascadeDeleteAttribute), inherit: true);
                    if (!hasCascadeAttr)
                        fk.DeleteBehavior = DeleteBehavior.Restrict; // default: no cascade
                    else
                        EfBoostPropertyConfiguration.ApplyCascadeDelete(fk);
                }
        }
    }
}
