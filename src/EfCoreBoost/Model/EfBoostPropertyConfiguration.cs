using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Boost.Model;

internal static class EfBoostPropertyConfiguration
{
    /// <summary>
    /// Configures a property as a database-generated GUID with provider-specific default values (e.g., NEWSEQUENTIALID() for SQL Server).
    /// </summary>
    internal static void ApplyDbGuid(IMutableProperty property, bool isSqlServer, bool isNpgsql, bool isMySql)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clrType != typeof(Guid)) throw new InvalidOperationException($"[DbGuid] can only be used on Guid/Guid?. Property: {property.DeclaringType.Name}.{property.Name}");
        property.SetAnnotation(EfBoostAnnotationNames.DbGuid, true);
        if (isSqlServer)
        {
            property.SetColumnType("uniqueidentifier");
            property.SetDefaultValueSql("NEWSEQUENTIALID()");
        }
        else if (isNpgsql)
        {
            property.SetColumnType("uuid");
            property.SetDefaultValueSql("gen_random_uuid()");
        }
        else if (isMySql)
        {
            property.SetColumnType("char(36)");
            property.SetDefaultValueSql("(UUID())");
        }
    }

    /// <summary>
    /// Configures a property to have a database-generated current UTC date/time default value.
    /// For MySQL, we use UTC_TIMESTAMP(6) to match the default datetime(6) column precision.
    /// </summary>
    internal static void ApplyDbDefaultCurrentUtc(IMutableProperty property, bool isSqlServer, bool isNpgsql, bool isMySql)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clrType != typeof(DateTime) && clrType != typeof(DateTimeOffset))
        {
            throw new InvalidOperationException(
                $"[DbDefaultCurrentUtc] can only be used on DateTime or DateTimeOffset properties. Property: {property.DeclaringType.Name}.{property.Name}");
        }

        property.SetAnnotation(EfBoostAnnotationNames.DbDefaultCurrentUtc, true);
        property.ValueGenerated = ValueGenerated.OnAdd;

        if (isSqlServer)
        {
            property.SetDefaultValueSql(
                clrType == typeof(DateTimeOffset)
                    ? "TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')"
                    : "SYSUTCDATETIME()");
        }
        else if (isNpgsql)
        {
            property.SetDefaultValueSql(
                clrType == typeof(DateTimeOffset)
                    ? "CURRENT_TIMESTAMP"
                    : "CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");
        }
        else if (isMySql)
        {
            property.SetDefaultValueSql("UTC_TIMESTAMP(6)");
        }
    }

    /// <summary>
    /// Configures a property as a primary key with automatic value generation (Identity or GUID).
    /// </summary>
    internal static void ApplyDbAutoUid(IMutableEntityType entity, IMutableProperty property, bool isSqlServer, bool isNpgsql, bool isMySql)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        var isInt = clrType == typeof(int);
        var isLong = clrType == typeof(long);
        var isGuid = clrType == typeof(Guid);
        if (!isInt && !isLong && !isGuid) throw new InvalidOperationException($"[DbUid] can only be applied to int/long/Guid properties. Property: {entity?.Name ?? "Unknown"}.{property.Name}, type: {clrType.Name}");
        property.SetAnnotation(EfBoostAnnotationNames.DbAutoUid, true);
        var existingPk = entity.FindPrimaryKey();
        if (existingPk != null && !existingPk.Properties.Contains(property))
        {
            throw new InvalidOperationException(
                $"Entity '{entity.ClrType.Name}' already has a primary key ({string.Join(",", existingPk.Properties.Select(p => p.Name))}). " +
                $"Cannot also mark '{property.Name}' with [DbUid].");
        }
        entity.SetPrimaryKey(property);
        property.IsNullable = false;
        property.ValueGenerated = ValueGenerated.OnAdd;
        if (isSqlServer)
            property.SetValueGenerationStrategy(SqlServerValueGenerationStrategy.IdentityColumn);
        else if (isNpgsql)
            property.SetValueGenerationStrategy(Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
    }

    /// <summary>
    /// Applies string length configuration, semantic purpose annotations, and database-specific column types.
    /// </summary>
    internal static void ApplyStringSize(IMutableProperty property, int? maxLen, string? annotationName, bool isSqlServer, bool isNpgsql, bool isMySql)
    {
        if (property.ClrType != typeof(string)) return;
        if (annotationName != null) property.SetAnnotation(annotationName, true);
        // Map bucket annotation for semantic purposes
        var bucketAnnotation = annotationName switch
        {
            EfBoostAnnotationNames.StrCode => EfBoostAnnotationNames.StrCode,
            EfBoostAnnotationNames.StrShort => EfBoostAnnotationNames.StrShort,
            EfBoostAnnotationNames.StrMed => EfBoostAnnotationNames.StrMed,
            EfBoostAnnotationNames.StrLong => EfBoostAnnotationNames.StrLong,
            EfBoostAnnotationNames.Text => EfBoostAnnotationNames.Text,
            EfBoostAnnotationNames.Name or EfBoostAnnotationNames.ExternalRef or EfBoostAnnotationNames.Title or
            EfBoostAnnotationNames.MimeType or EfBoostAnnotationNames.AddressAdminArea or EfBoostAnnotationNames.AddressStreetName or
            EfBoostAnnotationNames.AddressCity or EfBoostAnnotationNames.AddressRecipientName => EfBoostAnnotationNames.StrMed,
            EfBoostAnnotationNames.UserName or EfBoostAnnotationNames.PhoneNumber or EfBoostAnnotationNames.AddressPostalCode or
            EfBoostAnnotationNames.AddressStreetNumber or EfBoostAnnotationNames.AddressBuildingUnit => EfBoostAnnotationNames.StrShort,
            EfBoostAnnotationNames.FileName or EfBoostAnnotationNames.Email or EfBoostAnnotationNames.SiteUrl => EfBoostAnnotationNames.StrLong,
            EfBoostAnnotationNames.CountryCode or EfBoostAnnotationNames.CurrencyCode or EfBoostAnnotationNames.LanguageCode or
            EfBoostAnnotationNames.CultureCode or EfBoostAnnotationNames.RegNo => EfBoostAnnotationNames.StrCode,
            EfBoostAnnotationNames.Html or EfBoostAnnotationNames.Editor or EfBoostAnnotationNames.Json or
            EfBoostAnnotationNames.Markdown or EfBoostAnnotationNames.RichText => EfBoostAnnotationNames.Text,
            _ => null
        };
        if (bucketAnnotation != null && bucketAnnotation != annotationName)
            property.SetAnnotation(bucketAnnotation, true);
        if (property.GetMaxLength().HasValue) return;
        if (maxLen.HasValue) property.SetMaxLength(maxLen.Value);
        if (isSqlServer) property.SetColumnType(maxLen.HasValue ? $"nvarchar({maxLen.Value})" : "nvarchar(max)");
        else if (isNpgsql)
            property.SetColumnType("citext");
            //Note that we force all strings to citext in postgres (superior text handling we want to take advantage of)
            //property.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "text");
        else if (isMySql) property.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "longtext");
    }

    /// <summary>
    /// Applies precision and scale for decimal properties along with a semantic purpose annotation.
    /// </summary>
    internal static void ApplyPrecision(IMutableProperty property, int precision, int scale, string? annotationName)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clrType != typeof(decimal)) return;
        if (annotationName != null) property.SetAnnotation(annotationName, true);
        if (property.GetPrecision().HasValue || property.GetScale().HasValue) return;
        property.SetPrecision(precision);
        property.SetScale(scale);
    }

    /// <summary>
    /// Configures a property as an auto-incrementing concurrency token.
    /// </summary>
    internal static void ApplyAutoIncrementConcurrency(IMutableProperty property, ModelBuilder modelBuilder)
    {
        property.SetAnnotation(EfBoostAnnotationNames.AutoIncrementConcurrency, true);
        property.IsConcurrencyToken = true;
        EnsureDefaultNumIfMissing(modelBuilder, property);
    }

    /// <summary>
    /// Configures a property for auto-incrementing values.
    /// </summary>
    internal static void ApplyAutoIncrement(IMutableProperty property, ModelBuilder modelBuilder)
    {
        property.SetAnnotation(EfBoostAnnotationNames.AutoIncrement, true);
        EnsureDefaultNumIfMissing(modelBuilder, property);
    }

    internal static void ApplySoftDelete(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.SoftDelete, true);
    internal static void ApplyLastChangedUtc(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.LastChangedUtc, true);
    internal static void ApplyCreatedUtc(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.CreatedUtc, true);
    internal static void ApplyValidFromUtc(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.ValidFromUtc, true);
    internal static void ApplyValidToUtc(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.ValidToUtc, true);
    internal static void ApplyExpiresUtc(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.ExpiresUtc, true);
    internal static void ApplyTenant(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Tenant, true);
    internal static void ApplyStatus(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Status, true);
    internal static void ApplySoftRef(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.SoftRef, true);
    internal static void ApplyBirthDate(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.BirthDate, true);
    internal static void ApplyMedia(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Media, true);
    internal static void ApplyHash(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Hash, true);
    internal static void ApplySalt(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Salt, true);
    internal static void ApplyEncrypted(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.Encrypted, true);
    internal static void ApplySigningKey(IMutableProperty property) => property.SetAnnotation(EfBoostAnnotationNames.SigningKey, true);

    /// <summary>
    /// Configures a foreign key to use Restricted delete behavior.
    /// </summary>
    internal static void ApplyNoCascadeDelete(IMutableForeignKey fk)
    {
        fk.DeleteBehavior = DeleteBehavior.Restrict;
        fk.SetAnnotation(EfBoostAnnotationNames.NoCascadeDelete, true);
    }

    /// <summary>
    /// Configures a foreign key to use Cascade delete behavior.
    /// </summary>
    internal static void ApplyCascadeDelete(IMutableForeignKey fk)
    {
        fk.DeleteBehavior = DeleteBehavior.Cascade;
        fk.SetAnnotation(EfBoostAnnotationNames.CascadeDelete, true);
    }

    private static void EnsureDefaultNumIfMissing(ModelBuilder modelBuilder, IMutableProperty property)
    {
        if (property.GetDefaultValue() != null || !string.IsNullOrWhiteSpace(property.GetDefaultValueSql())) return;
        var t = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (t == typeof(long)) property.SetDefaultValue(1L);
        else if (t == typeof(int)) property.SetDefaultValue(1);
    }
}
