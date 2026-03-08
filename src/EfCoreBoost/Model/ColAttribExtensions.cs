using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pomelo.EntityFrameworkCore.MySql;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

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
                var conCurrCount = 0;
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
                    // 5) AutoIncrementConcurrency
                    var auIncr = pi.GetCustomAttribute<AutoIncrementConcurrencyAttribute>();
                    if (auIncr != null && IsIntOrLong(pi.PropertyType))
                    {
                        if (conCurrCount > 0) throw new InvalidOperationException($"Multiple [AutoIncrementConcurrency] properties on '{clr.Name}', '{pi.Name}'.");
                        conCurrCount++;
                        EnsureDefaultNumIfMissing(modelBuilder, clr, pi);
                        modelBuilder.Entity(clr).Property(pi.Name).IsConcurrencyToken();
                    }
                    var oIncr = pi.GetCustomAttribute<AutoIncrementAttribute>();
                    if (oIncr != null && IsIntOrLong(pi.PropertyType))
                    {
                        //Set default value to 0 if not set
                        EnsureDefaultNumIfMissing(modelBuilder, clr, pi);
                    }
                }
            }
        }

        private static bool IsIntOrLong(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long);
        }

        private static bool HasAnyDefault(Microsoft.EntityFrameworkCore.Metadata.IMutableProperty p)  => p.GetDefaultValue() != null || !string.IsNullOrWhiteSpace(p.GetDefaultValueSql());

        private static void EnsureDefaultNumIfMissing(ModelBuilder modelBuilder, Type clr, PropertyInfo pi)
        {
            var p = modelBuilder.Entity(clr).Property(pi.Name).Metadata;
            if (HasAnyDefault(p)) return; // someone already set it (Fluent API, convention, etc.)

            var t = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
            if (t == typeof(long)) modelBuilder.Entity(clr).Property(pi.Name).HasDefaultValue(1L);
            else if (t == typeof(int)) modelBuilder.Entity(clr).Property(pi.Name).HasDefaultValue(1);
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

        private enum StrBucket { None, Code, Short, Med, Long, Text }
        private readonly record struct StrRule(Type AttrType, StrBucket Bucket);
        private static void ConfigureStringSize(IMutableProperty prop, PropertyInfo pi, bool isSqlServer, bool isNpgsql, bool isMySql)
        {
            // DO NOT override explicit MaxLength if already set
            if (prop.GetMaxLength().HasValue) return;

            static bool Has(PropertyInfo pi, Type attrType) => Attribute.IsDefined(pi, attrType, inherit: false);
            // Table-driven rules: add new attributes here
            // URL is mapped to Med (256).
            StrRule[] rules =
            {
                // Explicit size buckets
                new(typeof(StrCodeAttribute), StrBucket.Code),
                new(typeof(StrShortAttribute), StrBucket.Short),
                new(typeof(StrMedAttribute), StrBucket.Med),
                new(typeof(StrLongAttribute), StrBucket.Long),
                new(typeof(TextAttribute), StrBucket.Text),
                // Semantic: codes
                new(typeof(CountryCodeAttribute), StrBucket.Code),
                new(typeof(CurrencyCodeAttribute), StrBucket.Code),
                new(typeof(LanguageCodeAttribute), StrBucket.Code),
                new(typeof(CultureCodeAttribute), StrBucket.Code),
                new(typeof(MimeTypeAttribute), StrBucket.Short),
                // Semantic: address bits (short-ish)
                new(typeof(AddressPostalCodeAttribute), StrBucket.Short),
                new(typeof(AddressStreetNumberAttribute), StrBucket.Short),
                new(typeof(AddressBuildingUnitAttribute), StrBucket.Short),
                new(typeof(PhoneAttribute), StrBucket.Short),
                new(typeof(UserNameAttribute), StrBucket.Short),

                // Semantic: address bits (medium)
                new(typeof(NameAttribute), StrBucket.Med),
                new(typeof(TitleAttribute), StrBucket.Med),
                new(typeof(ExternalRefAttribute), StrBucket.Med),
                new(typeof(AddressStreetNameAttribute), StrBucket.Med),
                new(typeof(AddressCityAttribute), StrBucket.Med),
                new(typeof(AddressAdminAreaAttribute), StrBucket.Med),
                new(typeof(AddressRecepientNameAttribute), StrBucket.Med), // consider renaming typo
                // Semantic: special strings
                new(typeof(EmailAttribute), StrBucket.Long), // RFC max 320 -> keep Long(512)
                new(typeof(UrlAttribute), StrBucket.Long),    // URL <= 356
                new(typeof(FileNameAttribute), StrBucket.Long),
                // Formatted/structured text (unbounded)
                new(typeof(HtmlAttribute), StrBucket.Text),
                new(typeof(MarkdownAttribute), StrBucket.Text),
                new(typeof(RichTextAttribute), StrBucket.Text),
                new(typeof(JsonAttribute), StrBucket.Text),
                new(typeof(EditorAttribute), StrBucket.Text),
            };
            StrBucket bucket = StrBucket.None;
            Type? firstAttr = null;
            foreach (var r in rules)
            {
                if (!Has(pi, r.AttrType)) continue;
                if (bucket == StrBucket.None)
                {
                    bucket = r.Bucket;
                    firstAttr = r.AttrType;
                    continue;
                }
                if (bucket != r.Bucket)
                    throw new InvalidOperationException(
                        $"Property '{prop.DeclaringType?.Name}.{prop.Name}' has multiple string intent attributes " +
                        $"('{firstAttr?.Name}', '{r.AttrType.Name}'). Only one string intent/size attribute is allowed.");
                // If same bucket (aliases), allow it.
            }
            // If none -> let EF default
            if (bucket == StrBucket.None) return;
            int? maxLen = bucket switch
            {
                StrBucket.Code => 30,
                StrBucket.Short => 50,
                StrBucket.Med => 256,
                StrBucket.Long => 512,
                _ => null // Text => unbounded
            };
            if (maxLen.HasValue) prop.SetMaxLength(maxLen.Value);
            // Column types: keep consistent across providers
            if (isSqlServer)
                prop.SetColumnType(maxLen.HasValue ? $"nvarchar({maxLen.Value})" : "nvarchar(max)");
            else if (isNpgsql)
                prop.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "text");
            else if (isMySql)
                prop.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "longtext");
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
            var lgt = pi.GetCustomAttribute<LongitudeAttribute>();
            var lat = pi.GetCustomAttribute<LatitudeAttribute>();

            var count = (pct != null ? 1 : 0)
                + (price != null ? 1 : 0)
                + (qty != null ? 1 : 0)
                + (rate != null ? 1 : 0)
                + (money != null ? 1 : 0)
                + (sort != null ? 1 : 0)
                + (lgt != null ? 1 : 0)
                + (lat != null ? 1 : 0)
                + (sci != null ? 1 : 0);

            if (count > 1)
                throw new InvalidOperationException(
                    $"Property '{prop.DeclaringType.ClrType.Name}.{prop.Name}' " + "has multiple decimal attributes. Only one is allowed.");

            int precision, scale;

            if (pct != null || qty != null || rate != null) { precision = 18; scale = 8; }
            else if (price != null || money != null) { precision = 19; scale = 4; }
            else if (sort != null || sci != null) { precision = 38; scale = 19; }
            else if (lgt != null || lat != null) { precision = 9; scale = 6; }
            else { precision = 19; scale = 4; } // default for all other decimals

            prop.SetPrecision(precision);
            prop.SetScale(scale);
        }

    }
}
