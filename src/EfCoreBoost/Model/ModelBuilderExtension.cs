using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using System.Reflection;
using static EfCore.Boost.Model.Attributes.EfForeignKeyAttributes;

namespace EfCore.Boost.Model
{
    public static class ModelBuilderExtension
    {
        /// <summary>
        /// Call this in every modelbuilder, this maps columns and sets apropriate db-types as needed for each database flavor.
        /// For MySql schema will be built into table name (since not supported by Mysql)
        /// For Postgres we switch from using nvarchar(x) to citext
        /// For Postgres we also need to unify usage of DateTime -> DateTime will be "timestamp without time zone"
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="ctx"></param>
        /// <param name="defaultSchema">Schema attribute will override this one if empty defaults to each db flavor normaly uses i.e. 'dbo' for mssql.</param>
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
            // For our custom attributes for shcema and database generated guids:
            modelBuilder.ApplyViewKeyConvention();
            // Schema + table mapping
            modelBuilder.ApplySchemaAndViewMapping(ctx, defaultSchema);
            // Column attributes, stringlengths, DbGuid, DbAutoUid etc
            modelBuilder.ApplyBoostColumnConventions(ctx);
            //Week foreign key with no cascading deletes
            //modelBuilder.ApplyNoCascadeDelete();
            //Reverse the EF CasadedDelets convension -> no cascading deletes applied by db, unless specificly requested by attribute ([CascadeDelete])
            modelBuilder.DisableAllCascadeDeletes();
        }

        static void ApplyPostgresCitext(ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                foreach (var prop in entity.GetProperties())
                {
                    if (prop.ClrType != typeof(string)) continue;
                    if (!string.IsNullOrEmpty(prop.GetColumnType())) continue;
                    prop.SetColumnType("citext");
                }
        }

        public static void ApplyPostgresDateTimeFix(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entityType.ClrType;
                if (clr != null && Attribute.IsDefined(clr, typeof(ViewKeyAttribute), false))
                    continue; // skip views
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                        property.SetColumnType("timestamp without time zone");
                }
            }
        }


        /// <summary>
        /// In case we dont run ApplyNoCascadeDelete, we run this in stead to look for [NoCascadeDelete] to apply to foreign key relation
        /// </summary>
        /// <param name="modelBuilder"></param>
        public static void ApplyNoCascadeDelete(this ModelBuilder modelBuilder)
        {

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var fk in entityType.GetForeignKeys())
                {
                    // dependent → principal navigation
                    var nav = fk.DependentToPrincipal?.PropertyInfo;
                    if (nav == null) continue;

                    // Look for [NoCascadeDelete] on the navigation
                    if (Attribute.IsDefined(nav, typeof(NoCascadeDeleteAttribute), inherit: true))
                        fk.DeleteBehavior = DeleteBehavior.Restrict;  // or NoAction
                }
        }

        /// <summary>
        /// By dfault EF forces cascade delete on all none nullable foreighn keys (If parent is deleted, kill all the children)
        /// This is quite dangerous behaviour and we would rather have error from db, preventing parent to be deleted.
        /// Applying this method reverses this default, unless special-custom attribute [CascadeDelete] is placed on the navigation object along with the foreign key.
        /// </summary>
        /// <param name="modelBuilder"></param>
        public static void DisableAllCascadeDeletes(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
                foreach (var fk in entityType.GetForeignKeys())
                {
                    if (fk.IsOwnership) continue;

                    var nav = fk.DependentToPrincipal?.PropertyInfo;
                    var hasCascadeAttr = nav != null &&
                        Attribute.IsDefined(nav, typeof(CascadeDeleteAttribute), inherit: true);

                    if (!hasCascadeAttr)
                        fk.DeleteBehavior = DeleteBehavior.Restrict; // default: no cascade
                    else
                        fk.DeleteBehavior = DeleteBehavior.Cascade;  // explicitly allowed
                }
        }
    }
}
