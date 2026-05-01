using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection;

namespace EfCore.Boost.Model
{

    public static class ColAttribExtensions
    {
        /// <summary>
        /// Handle all our custom attributes for columns:
        /// </summary>
        /// <param name="modelBuilder"></param>
        /// <param name="ctx"></param>
        internal static void ApplyBoostColumnConventions(this ModelBuilder modelBuilder, DbContext ctx)
        {
            var provider = ctx.Database.ProviderName ?? string.Empty;
            var isSqlServer = provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase);
            var isNpgsql = provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase);
            var isMySql = provider.Contains("MySql", StringComparison.OrdinalIgnoreCase);

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entity.ClrType;
                var isView = Attribute.IsDefined(clr, typeof(ViewKeyAttribute), inherit: false);
                var conCurrCount = 0;
                foreach (var pi in clr.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    var prop = entity.FindProperty(pi);
                    if (prop == null) continue;

                    // 1) DbGuid
                    var dbGuidAttr = pi.GetCustomAttribute<DbGuidAttribute>();
                    if (dbGuidAttr != null)
                        EfBoostPropertyConfiguration.ApplyDbGuid(prop, isSqlServer, isNpgsql, isMySql);

                    // 2) DbUid
                    var dbUidAttr = pi.GetCustomAttribute<DbAutoUidAttribute>();
                    if (dbUidAttr != null && !isView)
                        EfBoostPropertyConfiguration.ApplyDbAutoUid(entity, prop, isSqlServer, isNpgsql, isMySql);

                    // 3) String length attributes
                    if (pi.PropertyType == typeof(string))
                        ConfigureStringSize(prop, pi, isSqlServer, isNpgsql, isMySql);

                    // 4) Decimal precision
                    if (pi.PropertyType == typeof(decimal) || Nullable.GetUnderlyingType(pi.PropertyType) == typeof(decimal))
                        ConfigureDecimalPrecision(prop, pi);

                    // 5) AutoIncrementConcurrency
                    var auIncr = pi.GetCustomAttribute<AutoIncrementConcurrencyAttribute>();
                    if (auIncr != null && !isView && IsIntOrLong(pi.PropertyType))
                    {
                        if (conCurrCount > 0) throw new InvalidOperationException($"Multiple [AutoIncrementConcurrency] properties on '{clr.Name}', '{pi.Name}'.");
                        conCurrCount++;
                        EfBoostPropertyConfiguration.ApplyAutoIncrementConcurrency(prop, modelBuilder);
                    }
                    var oIncr = pi.GetCustomAttribute<AutoIncrementAttribute>();
                    if (oIncr != null && !isView && IsIntOrLong(pi.PropertyType))
                    {
                        EfBoostPropertyConfiguration.ApplyAutoIncrement(prop, modelBuilder);
                    }

                    // 6) Purpose markings
                    if (pi.GetCustomAttribute<SoftDeleteAttribute>() != null) EfBoostPropertyConfiguration.ApplySoftDelete(prop);
                    if (pi.GetCustomAttribute<LastChangedUtcAttribute>() != null) EfBoostPropertyConfiguration.ApplyLastChangedUtc(prop);
                    if (pi.GetCustomAttribute<CreatedUtcAttribute>() != null) EfBoostPropertyConfiguration.ApplyCreatedUtc(prop);
                    if (pi.GetCustomAttribute<ValidFromUtcAttribute>() != null) EfBoostPropertyConfiguration.ApplyValidFromUtc(prop);
                    if (pi.GetCustomAttribute<ValidToUtcAttribute>() != null) EfBoostPropertyConfiguration.ApplyValidToUtc(prop);
                    if (pi.GetCustomAttribute<ExpiresUtcAttribute>() != null) EfBoostPropertyConfiguration.ApplyExpiresUtc(prop);
                    if (pi.GetCustomAttribute<TenantAttribute>() != null) EfBoostPropertyConfiguration.ApplyTenant(prop);
                    if (pi.GetCustomAttribute<StatusAttribute>() != null) EfBoostPropertyConfiguration.ApplyStatus(prop);
                    if (pi.GetCustomAttribute<SoftRefAttribute>() != null) EfBoostPropertyConfiguration.ApplySoftRef(prop);
                    if (pi.GetCustomAttribute<BirthDateAttribute>() != null) EfBoostPropertyConfiguration.ApplyBirthDate(prop);

                    // 7) Raw data
                    if (pi.GetCustomAttribute<MediaAttribute>() != null) EfBoostPropertyConfiguration.ApplyMedia(prop);
                    if (pi.GetCustomAttribute<HashAttribute>() != null) EfBoostPropertyConfiguration.ApplyHash(prop);
                    if (pi.GetCustomAttribute<SaltAttribute>() != null) EfBoostPropertyConfiguration.ApplySalt(prop);
                    if (pi.GetCustomAttribute<EncryptedAttribute>() != null) EfBoostPropertyConfiguration.ApplyEncrypted(prop);
                    if (pi.GetCustomAttribute<SigningKeyAttribute>() != null) EfBoostPropertyConfiguration.ApplySigningKey(prop);
                }
            }
        }

        private static bool IsIntOrLong(Type t)
        {
            t = Nullable.GetUnderlyingType(t) ?? t;
            return t == typeof(int) || t == typeof(long);
        }

        private static bool HasAnyDefault(IMutableProperty p)  => p.GetDefaultValue() != null || !string.IsNullOrWhiteSpace(p.GetDefaultValueSql());

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
            EfBoostPropertyConfiguration.ApplyDbGuid(prop, isSqlServer, isNpgsql, isMySql);
        }

        private static void ConfigureDbUid(IMutableEntityType entity,IMutableProperty prop,PropertyInfo pi,DbAutoUidAttribute dbUidAttr,bool isSqlServer,bool isNpgsql,bool isMySql)
        {
            EfBoostPropertyConfiguration.ApplyDbAutoUid(entity, prop, isSqlServer, isNpgsql, isMySql);
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
            [
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
                new(typeof(MimeTypeAttribute), StrBucket.Med),
                // Semantic: address bits (shortish)
                new(typeof(AddressPostalCodeAttribute), StrBucket.Short),
                new(typeof(AddressStreetNumberAttribute), StrBucket.Short),
                new(typeof(AddressBuildingUnitAttribute), StrBucket.Short),
                new(typeof(PhoneNumberAttribute), StrBucket.Short),
                new(typeof(UserNameAttribute), StrBucket.Short),
                new(typeof(RegNoAttribute), StrBucket.Code),

                // Semantic: address bits (medium)
                new(typeof(NameAttribute), StrBucket.Med),
                new(typeof(TitleAttribute), StrBucket.Med),
                new(typeof(ExternalRefAttribute), StrBucket.Med),
                new(typeof(AddressStreetNameAttribute), StrBucket.Med),
                new(typeof(AddressCityAttribute), StrBucket.Med),
                new(typeof(AddressAdminAreaAttribute), StrBucket.Med),
                new(typeof(AddressRecipientNameAttribute), StrBucket.Med), // consider renaming typo
                // Semantic: special strings
                new(typeof(EmailAttribute), StrBucket.Long), // RFC max 320 -> keep Long(512)
                new(typeof(SiteUrlAttribute), StrBucket.Long),    // URL <= 356
                new(typeof(FileNameAttribute), StrBucket.Long),
                // Formatted/structured text (unbounded)
                new(typeof(HtmlAttribute), StrBucket.Text),
                new(typeof(MarkdownAttribute), StrBucket.Text),
                new(typeof(RichTextAttribute), StrBucket.Text),
                new(typeof(JsonAttribute), StrBucket.Text),
                new(typeof(EditorAttribute), StrBucket.Text)
            ];
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
                        $"Property '{prop.DeclaringType.Name}.{prop.Name}' has multiple string intent attributes " +
                        $"('{firstAttr?.Name}', '{r.AttrType.Name}'). Only one string intent/size attribute is allowed.");
                // If the same bucket (aliases), allow it.
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

            var annotationName = bucket switch
            {
                StrBucket.Code => EfBoostAnnotationNames.StrCode,
                StrBucket.Short => EfBoostAnnotationNames.StrShort,
                StrBucket.Med => EfBoostAnnotationNames.StrMed,
                StrBucket.Long => EfBoostAnnotationNames.StrLong,
                StrBucket.Text => EfBoostAnnotationNames.Text,
                _ => null
            };
            if (annotationName != null) prop.SetAnnotation(annotationName, true);

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
            string annotationName;
            if (pct != null || qty != null || rate != null) { precision = 18; scale = 8; annotationName = pct != null ? EfBoostAnnotationNames.Percentage : (qty != null ? EfBoostAnnotationNames.Qty : EfBoostAnnotationNames.Rate); }
            else if (price != null || money != null) { precision = 19; scale = 4; annotationName = price != null ? EfBoostAnnotationNames.Price : EfBoostAnnotationNames.Money; }
            else if (sort != null || sci != null) { precision = 38; scale = 19; annotationName = sort != null ? EfBoostAnnotationNames.SortRank : EfBoostAnnotationNames.Scientific; }
            else if (lgt != null || lat != null) { precision = 9; scale = 6; annotationName = lgt != null ? EfBoostAnnotationNames.Longitude : EfBoostAnnotationNames.Latitude; }
            else { precision = 19; scale = 4; annotationName = null!; } // default for all other decimals
            prop.SetPrecision(precision);
            prop.SetScale(scale);
            prop.SetAnnotation(annotationName, true);
        }

    }
}
