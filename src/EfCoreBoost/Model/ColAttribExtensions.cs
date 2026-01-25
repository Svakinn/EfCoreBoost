using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Pomelo.EntityFrameworkCore.MySql;
using EfCore.Boost.Model.Attributes;

namespace EfCore.Boost.Model
{

    public static class ColAttribExtensions
    {
        /// <summary>
        /// Handle all our custom attributes for columns:
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="ctx"></param>
        public static void ApplyBoostColumnConventions(this ModelBuilder modelBuilder, DbContext ctx)
        {
            var provider = ctx.Database.ProviderName ?? string.Empty;
            var isSqlServer = provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
            var isNpgsql = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
            var isMySql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entity.ClrType;
                // Skip views or non-CLR-backed entity types
                if (clr == null || Attribute.IsDefined(clr, typeof(ViewKeyAttribute), inherit: false))
                    continue;

                foreach (var pi in clr.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    var prop = entity.FindProperty(pi);
                    if (prop == null) continue;

                    // 1) DbGuid (your existing behavior, slightly cleaned up)
                    var dbGuidAttr = pi.GetCustomAttribute<DbGuidAttribute>();
                    if (dbGuidAttr != null)
                        ConfigureDbGuid(prop, isSqlServer, isNpgsql, isMySql);

                    // 2) DbUid (shorthand: PK + database generated ID)
                    var dbUidAttr = pi.GetCustomAttribute<DbAutoUidAttribute>();
                    if (dbUidAttr != null)
                        ConfigureDbUid(entity, prop, pi, dbUidAttr, isSqlServer, isNpgsql, isMySql);

                    // 3) String length attributes: StrShort / StrLong / StrText
                    if (pi.PropertyType == typeof(string))
                        ConfigureStringSize(prop, pi, isSqlServer, isNpgsql, isMySql);

                    // 4) Decimal precision
                    if (pi.PropertyType == typeof(decimal) || Nullable.GetUnderlyingType(pi.PropertyType) == typeof(decimal))
                        ConfigureDecimalPrecision(prop, pi);
                }
            }
        }

        private static void ConfigureDbGuid(IMutableProperty prop,bool isSqlServer, bool isNpgsql,bool isMySql)
        {
            // Type guard: only Guid / Guid?
            var clrType = Nullable.GetUnderlyingType(prop.ClrType) ?? prop.ClrType;
            if (clrType != typeof(Guid))
                throw new InvalidOperationException(
                    $"[DbGuid] can only be used on Guid/Guid?. Property: {prop.ClrType.Name}.{prop.Name}");
            if (isSqlServer)
            {
                prop.SetColumnType("uniqueidentifier");
                prop.SetDefaultValueSql("NEWSEQUENTIALID()");
            }
            else if (isNpgsql)
            {
                prop.SetColumnType("uuid");
                prop.SetDefaultValueSql("gen_random_uuid()");
            }
            else if (isMySql)
            {
                prop.SetColumnType("char(36)");
                // Adjust later if you implement proper uuid generation in MySQL
                prop.SetDefaultValueSql("''");
            }
        }

        private static void ConfigureDbUid(IMutableEntityType entity,IMutableProperty prop,PropertyInfo pi,DbAutoUidAttribute dbUidAttr,bool isSqlServer,bool isNpgsql,bool isMySql)
        {
            // Type guard: allow int, long, Guid (plus nullable variants)
            var clrType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
            var isInt = clrType == typeof(int);
            var isLong = clrType == typeof(long);
            var isGuid = clrType == typeof(Guid);

            if (!isInt && !isLong && !isGuid)
                throw new InvalidOperationException(
                    $"[DbUid] can only be applied to int/long/Guid properties. " +
                    $"Property: {entity.ClrType.Name}.{pi.Name}, type: {clrType.Name}");

            // If entity already has a PK and it's not this property -> you may want to throw
            var existingPk = entity.FindPrimaryKey();
            if (existingPk != null && !existingPk.Properties.Contains(prop))
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.ClrType.Name}' already has a primary key " +
                    $"({string.Join(",", existingPk.Properties.Select(p => p.Name))}). " +
                    $"Cannot also mark '{pi.Name}' with [DbUid].");
            }

            // Make this property the primary key
            entity.SetPrimaryKey(prop);
            prop.IsNullable = false;
            prop.ValueGenerated = ValueGenerated.OnAdd;
            if (isSqlServer)
            {
                // Using provider strategy; you can customize to use sequences/HiLo later
                prop.SetValueGenerationStrategy(
                    Microsoft.EntityFrameworkCore.Metadata.SqlServerValueGenerationStrategy.IdentityColumn);
            }
            else if (isNpgsql)
            {
                prop.SetValueGenerationStrategy(
                    Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
            }
            //else if (isMySql && (isInt || isLong))
            //Ef conventions for MySql auto-increment are handled by Pomelo automatically
        }

        private static void ConfigureStringSize(IMutableProperty prop, PropertyInfo pi, bool isSqlServer, bool isNpgsql, bool isMySql)
        {
            // DO NOT override explicit MaxLength if already set
            if (prop.GetMaxLength().HasValue)
                return;

            var shortAttr = pi.GetCustomAttribute<StrShortAttribute>();
            var medAttr = pi.GetCustomAttribute<StrMedAttribute>();
            var longAttr = pi.GetCustomAttribute<StrLongAttribute>();
            var codeAttr = pi.GetCustomAttribute<StrCodeAttribute>();
            var textAttr = pi.GetCustomAttribute<TextAttribute>();

            // If none -> let EF default (nvarchar(max)/text/longtext)
            if (shortAttr == null && medAttr == null && longAttr == null && textAttr == null && codeAttr == null)
                return;

            // Prevent conflicting attributes
            var count = (shortAttr != null ? 1 : 0)
                + (medAttr != null ? 1 : 0)
                + (longAttr != null ? 1 : 0)
                + (codeAttr != null ? 1 : 0)
                + (textAttr != null ? 1 : 0);
            if (count > 1)
                throw new InvalidOperationException(
                    $"Property '{prop.ClrType.Name}.{prop.Name}' " +
                    $"has multiple Str* attributes. Only one of [StrCode], [StrShort], [StrMed], [StrLong], [Text] is allowed.");
            // Short / Long
            var maxLen = shortAttr != null ? 50 : (codeAttr != null ? 30 : (medAttr != null ? 256 : 512));
            if (!isNpgsql && textAttr != null)
                prop.SetMaxLength(maxLen);
            //Console.WriteLine($"Configuring string property {prop.ClrType.Name}.{prop.Name} with max length {maxLen}");
            if (isSqlServer) prop.SetColumnType($"nvarchar({maxLen})");
            else if (isNpgsql) prop.SetColumnType("citext");
            else if (isMySql) prop.SetColumnType($"varchar({maxLen})");
        }

        private static void ConfigureDecimalPrecision(IMutableProperty prop, PropertyInfo pi)
        {
            // Type guard: only decimal / decimal?
            var clrType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
            if (clrType != typeof(decimal))
                return;

            // DO NOT override explicit precision/scale if already configured
            if (prop.GetPrecision().HasValue || prop.GetScale().HasValue)
                return;

            // Attributes (fixed mapping)
            var pct = pi.GetCustomAttribute<PercentageAttribute>();
            var price = pi.GetCustomAttribute<PriceAttribute>();
            var qty = pi.GetCustomAttribute<QtyAttribute>();
            var rate = pi.GetCustomAttribute<RateAttribute>();
            var money = pi.GetCustomAttribute<MoneyAttribute>();
            var sort = pi.GetCustomAttribute<SortRankAttribute>();
            var sci = pi.GetCustomAttribute<ScientificAttribute>();

            var count = (pct != null ? 1 : 0)
                + (price != null ? 1 : 0)
                + (qty != null ? 1 : 0)
                + (rate != null ? 1 : 0)
                + (money != null ? 1 : 0)
                + (sort != null ? 1 : 0)
                + (sci != null ? 1 : 0);

            if (count > 1)
                throw new InvalidOperationException(
                    $"Property '{prop.DeclaringType.ClrType.Name}.{prop.Name}' " + "has multiple decimal attributes. Only one is allowed.");

            int precision, scale;

            if (pct != null || qty != null || rate != null) { precision = 18; scale = 8; }
            else if (price != null || money != null) { precision = 19; scale = 4; }
            else if (sort != null || sci != null) { precision = 38; scale = 19; }
            else { precision = 19; scale = 4; } // default for all other decimals

            prop.SetPrecision(precision);
            prop.SetScale(scale);
        }

    }
}
