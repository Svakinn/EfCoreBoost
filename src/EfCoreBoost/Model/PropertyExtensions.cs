using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Boost.Model;
/// <summary>
/// Make our custom attributes work fluently as well
/// </summary>
public static class EfBoostPropertyExtensions
{
    /// <summary>
    /// Configures a property as a primary key with automatic value generation.
    /// </summary>
    public static PropertyBuilder<T> HasDbAutoUid<T>(this PropertyBuilder<T> builder)
    {
        if (builder.Metadata.DeclaringType is IMutableEntityType entityType)
        {
            EfBoostPropertyConfiguration.ApplyDbAutoUid(entityType, builder.Metadata, false, false, false);
        }
        return builder;
    }

    /// <summary>
    /// Configures a GUID property as a database-generated unique identifier.
    /// </summary>
    public static PropertyBuilder<Guid> HasDbGuid(this PropertyBuilder<Guid> builder)
    {
        EfBoostPropertyConfiguration.ApplyDbGuid(builder.Metadata, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a nullable GUID property as a database-generated unique identifier.
    /// </summary>
    public static PropertyBuilder<Guid?> HasDbGuid(this PropertyBuilder<Guid?> builder)
    {
        EfBoostPropertyConfiguration.ApplyDbGuid(builder.Metadata, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a string property with a standard "Code" length (30).
    /// </summary>
    public static PropertyBuilder<string> HasStrCode(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.StrCode, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a string property with a standard "Short" length (50).
    /// </summary>
    public static PropertyBuilder<string> HasStrShort(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.StrShort, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a string property with a standard "Medium" length (256).
    /// </summary>
    public static PropertyBuilder<string> HasStrMed(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.StrMed, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a string property with a standard "Long" length (512).
    /// </summary>
    public static PropertyBuilder<string> HasStrLong(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 512, EfBoostAnnotationNames.StrLong, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a string property as an unlimited length text.
    /// </summary>
    public static PropertyBuilder<string> HasText(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Text, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a property with a high-precision decimal type for monetary values (19,4).
    /// </summary>
    public static PropertyBuilder<decimal> HasPurposeMoney(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Money); return builder; }
    /// <summary>
    /// Configures a nullable property with a high-precision decimal type for monetary values (19,4).
    /// </summary>
    public static PropertyBuilder<decimal?> HasPurposeMoney(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Money); return builder; }

    public static PropertyBuilder<decimal> HasPurposeRate(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Rate); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeRate(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Rate); return builder; }
    public static PropertyBuilder<decimal> HasPurposeQty(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Qty); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeQty(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Qty); return builder; }
    public static PropertyBuilder<decimal> HasPurposePrice(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Price); return builder; }
    public static PropertyBuilder<decimal?> HasPurposePrice(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Price); return builder; }
    public static PropertyBuilder<decimal> HasPurposePercentage(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Percentage); return builder; }
    public static PropertyBuilder<decimal?> HasPurposePercentage(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Percentage); return builder; }
    public static PropertyBuilder<decimal> HasPurposeSortRank(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.SortRank); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeSortRank(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.SortRank); return builder; }
    public static PropertyBuilder<decimal> HasPurposeScientific(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.Scientific); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeScientific(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.Scientific); return builder; }
    public static PropertyBuilder<decimal> HasPurposeLongitude(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 9, 6, EfBoostAnnotationNames.Longitude); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeLongitude(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 9, 6, EfBoostAnnotationNames.Longitude); return builder; }
    public static PropertyBuilder<decimal> HasPurposeLatitude(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 9, 6, EfBoostAnnotationNames.Latitude); return builder; }
    public static PropertyBuilder<decimal?> HasPurposeLatitude(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 9, 6, EfBoostAnnotationNames.Latitude); return builder; }

    /// <summary>
    /// Configures a property to have a database-generated current UTC date/time default value.
    /// </summary>
    public static PropertyBuilder<T> HasDbDefaultCurrentUtc<T>(this PropertyBuilder<T> builder)
    {
        EfBoostPropertyConfiguration.ApplyDbDefaultCurrentUtc(builder.Metadata, false, false, false);
        return builder;
    }

    /// <summary>
    /// Configures a property as an auto-incrementing concurrency token.
    /// </summary>
    public static PropertyBuilder<T> HasAutoIncrementConcurrency<T>(this PropertyBuilder<T> builder)
    {
        // Let's see if we can use builder directly.
        builder.IsConcurrencyToken();
        builder.Metadata.SetAnnotation(EfBoostAnnotationNames.AutoIncrementConcurrency, true);
        var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (t == typeof(long)) builder.HasDefaultValue(1L);
        else if (t == typeof(int)) builder.HasDefaultValue(1);

        return builder;
    }

    /// <summary>
    /// Configures a property for auto-incrementing values.
    /// </summary>
    public static PropertyBuilder<T> HasAutoIncrement<T>(this PropertyBuilder<T> builder)
    {
        builder.Metadata.SetAnnotation(EfBoostAnnotationNames.AutoIncrement, true);
        var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (t == typeof(long)) builder.HasDefaultValue(1L);
        else if (t == typeof(int)) builder.HasDefaultValue(1);
        return builder;
    }

    // Semantic helpers
    public static PropertyBuilder<string> HasPurposeName(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.Name, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeUserName(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.UserName, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeExternalRef(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.ExternalRef, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeTitle(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.Title, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeFileName(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 512, EfBoostAnnotationNames.FileName, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeMimeType(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.MimeType, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeEmail(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 512, EfBoostAnnotationNames.Email, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposePhoneNumber(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.PhoneNumber, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeSiteUrl(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 512, EfBoostAnnotationNames.SiteUrl, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeCountryCode(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.CountryCode, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeCurrencyCode(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.CurrencyCode, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeLanguageCode(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.LanguageCode, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeCultureCode(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.CultureCode, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressPostalCode(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.AddressPostalCode, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressAdminArea(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.AddressAdminArea, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressStreetName(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.AddressStreetName, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressStreetNumber(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.AddressStreetNumber, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressCity(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.AddressCity, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressBuildingUnit(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.AddressBuildingUnit, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeAddressRecipientName(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.AddressRecipientName, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeRegNo(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.RegNo, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeHtml(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Html, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeEditor(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Editor, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeJson(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Json, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeMarkdown(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Markdown, false, false, false); return builder; }
    public static PropertyBuilder<string> HasPurposeRichText(this PropertyBuilder<string> builder) { EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.RichText, false, false, false); return builder; }
    public static PropertyBuilder<byte[]> HasPurposeMedia(this PropertyBuilder<byte[]> builder) { EfBoostPropertyConfiguration.ApplyMedia(builder.Metadata); return builder; }
    public static PropertyBuilder<byte[]> HasPurposeHash(this PropertyBuilder<byte[]> builder) { EfBoostPropertyConfiguration.ApplyHash(builder.Metadata); return builder; }
    public static PropertyBuilder<byte[]> HasPurposeSalt(this PropertyBuilder<byte[]> builder) { EfBoostPropertyConfiguration.ApplySalt(builder.Metadata); return builder; }
    public static PropertyBuilder<byte[]> HasPurposeEncrypted(this PropertyBuilder<byte[]> builder) { EfBoostPropertyConfiguration.ApplyEncrypted(builder.Metadata); return builder; }
    public static PropertyBuilder<byte[]> HasPurposeSigningKey(this PropertyBuilder<byte[]> builder) { EfBoostPropertyConfiguration.ApplySigningKey(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeSoftDelete<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplySoftDelete(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeLastChangedUtc<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyLastChangedUtc(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeCreatedUtc<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyCreatedUtc(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeValidFromUtc<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyValidFromUtc(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeValidToUtc<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyValidToUtc(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeExpiresUtc<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyExpiresUtc(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeTenant<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyTenant(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeStatus<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyStatus(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeSoftRef<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplySoftRef(builder.Metadata); return builder; }
    public static PropertyBuilder<T> HasPurposeBirthDate<T>(this PropertyBuilder<T> builder) { EfBoostPropertyConfiguration.ApplyBirthDate(builder.Metadata); return builder; }

    // Relationship helpers
    /// <summary>
    /// Configures a relationship to use Restricted delete behavior.
    /// </summary>
    public static ReferenceCollectionBuilder HasNoCascadeDelete(this ReferenceCollectionBuilder builder)
    {
        EfBoostPropertyConfiguration.ApplyNoCascadeDelete(builder.Metadata);
        return builder;
    }

    /// <summary>
    /// Configures a relationship to use Cascade delete behavior.
    /// </summary>
    public static ReferenceCollectionBuilder HasCascadeDelete(this ReferenceCollectionBuilder builder)
    {
        EfBoostPropertyConfiguration.ApplyCascadeDelete(builder.Metadata);
        return builder;
    }

    public static ReferenceReferenceBuilder HasNoCascadeDelete(this ReferenceReferenceBuilder builder)
    {
        EfBoostPropertyConfiguration.ApplyNoCascadeDelete(builder.Metadata);
        return builder;
    }

    public static ReferenceReferenceBuilder HasCascadeDelete(this ReferenceReferenceBuilder builder)
    {
        EfBoostPropertyConfiguration.ApplyCascadeDelete(builder.Metadata);
        return builder;
    }

    // Class-level helpers
    public static EntityTypeBuilder<T> HasDbSchema<T>(this EntityTypeBuilder<T> builder, string schema, string? table = null) where T : class
    {
        builder.ToTable(table ?? builder.Metadata.ClrType.Name, schema);
        return builder;
    }

    public static EntityTypeBuilder<T> HasViewKey<T>(this EntityTypeBuilder<T> builder, params string[] propertyNames) where T : class
    {
        builder.HasKey(propertyNames);
        return builder;
    }
}
