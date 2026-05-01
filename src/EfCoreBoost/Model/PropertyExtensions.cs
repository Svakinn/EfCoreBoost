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
    public static PropertyBuilder<T> HasDbAutoUid<T>(this PropertyBuilder<T> builder)
    {
        if (builder.Metadata.DeclaringType is IMutableEntityType entityType)
        {
            EfBoostPropertyConfiguration.ApplyDbAutoUid(entityType, builder.Metadata, false, false, false);
        }
        return builder;
    }

    public static PropertyBuilder<Guid> HasDbGuid(this PropertyBuilder<Guid> builder)
    {
        EfBoostPropertyConfiguration.ApplyDbGuid(builder.Metadata, false, false, false);
        return builder;
    }

    public static PropertyBuilder<Guid?> HasDbGuid(this PropertyBuilder<Guid?> builder)
    {
        EfBoostPropertyConfiguration.ApplyDbGuid(builder.Metadata, false, false, false);
        return builder;
    }

    public static PropertyBuilder<string> HasStrCode(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 30, EfBoostAnnotationNames.StrCode, false, false, false);
        return builder;
    }

    public static PropertyBuilder<string> HasStrShort(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 50, EfBoostAnnotationNames.StrShort, false, false, false);
        return builder;
    }

    public static PropertyBuilder<string> HasStrMed(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 256, EfBoostAnnotationNames.StrMed, false, false, false);
        return builder;
    }

    public static PropertyBuilder<string> HasStrLong(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, 512, EfBoostAnnotationNames.StrLong, false, false, false);
        return builder;
    }

    public static PropertyBuilder<string> HasText(this PropertyBuilder<string> builder)
    {
        EfBoostPropertyConfiguration.ApplyStringSize(builder.Metadata, null, EfBoostAnnotationNames.Text, false, false, false);
        return builder;
    }

    public static PropertyBuilder<decimal> HasMoney(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Money); return builder; }
    public static PropertyBuilder<decimal?> HasMoney(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 19, 4, EfBoostAnnotationNames.Money); return builder; }

    public static PropertyBuilder<decimal> HasRate(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Rate); return builder; }
    public static PropertyBuilder<decimal?> HasRate(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Rate); return builder; }

    public static PropertyBuilder<decimal> HasQty(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Qty); return builder; }
    public static PropertyBuilder<decimal?> HasQty(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 18, 8, EfBoostAnnotationNames.Qty); return builder; }

    public static PropertyBuilder<decimal> HasSortRank(this PropertyBuilder<decimal> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.SortRank); return builder; }
    public static PropertyBuilder<decimal?> HasSortRank(this PropertyBuilder<decimal?> builder) { EfBoostPropertyConfiguration.ApplyPrecision(builder.Metadata, 38, 19, EfBoostAnnotationNames.SortRank); return builder; }

    public static PropertyBuilder<T> HasAutoIncrementConcurrency<T>(this PropertyBuilder<T> builder)
    {
        // We need ModelBuilder here, but PropertyBuilder doesn't have it easily.
        // Actually, we can get it from a builder.
        // Wait, PropertyBuilder doesn't expose ModelBuilder.
        // But we can use builder.Metadata.DeclaringType.Model and access its builder via internal services if needed,
        // or just apply what we can and let convention do the rest if we can't.
        // However, EnsureDefaultNumIfMissing uses modelBuilder.Entity(clr).Property(name).HasDefaultValue.

        // Let's see if we can use builder directly.
        builder.IsConcurrencyToken();
        builder.Metadata.SetAnnotation(EfBoostAnnotationNames.AutoIncrementConcurrency, true);
        var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (t == typeof(long)) builder.HasDefaultValue(1L);
        else if (t == typeof(int)) builder.HasDefaultValue(1);

        return builder;
    }

    public static PropertyBuilder<T> HasAutoIncrement<T>(this PropertyBuilder<T> builder)
    {
        builder.Metadata.SetAnnotation(EfBoostAnnotationNames.AutoIncrement, true);
        var t = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (t == typeof(long)) builder.HasDefaultValue(1L);
        else if (t == typeof(int)) builder.HasDefaultValue(1);
        return builder;
    }

    // Semantic helpers
    public static PropertyBuilder<string> HasPurposeName(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeUserName(this PropertyBuilder<string> builder) => builder.HasStrShort();
    public static PropertyBuilder<string> HasPurposeExternalRef(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeTitle(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeFileName(this PropertyBuilder<string> builder) => builder.HasStrLong();
    public static PropertyBuilder<string> HasPurposeMimeType(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeEmail(this PropertyBuilder<string> builder) => builder.HasStrLong();
    public static PropertyBuilder<string> HasPurposePhoneNumber(this PropertyBuilder<string> builder) => builder.HasStrShort();
    public static PropertyBuilder<string> HasPurposeSiteUrl(this PropertyBuilder<string> builder) => builder.HasStrLong();
    public static PropertyBuilder<string> HasPurposeCountryCode(this PropertyBuilder<string> builder) => builder.HasStrCode();
    public static PropertyBuilder<string> HasPurposeCurrencyCode(this PropertyBuilder<string> builder) => builder.HasStrCode();
    public static PropertyBuilder<string> HasPurposeLanguageCode(this PropertyBuilder<string> builder) => builder.HasStrCode();
    public static PropertyBuilder<string> HasPurposeCultureCode(this PropertyBuilder<string> builder) => builder.HasStrCode();
    public static PropertyBuilder<string> HasPurposeAddressPostalCode(this PropertyBuilder<string> builder) => builder.HasStrShort();
    public static PropertyBuilder<string> HasPurposeAddressAdminArea(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeAddressStreetName(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeAddressStreetNumber(this PropertyBuilder<string> builder) => builder.HasStrShort();
    public static PropertyBuilder<string> HasPurposeAddressCity(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeAddressBuildingUnit(this PropertyBuilder<string> builder) => builder.HasStrShort();
    public static PropertyBuilder<string> HasPurposeAddressRecipientName(this PropertyBuilder<string> builder) => builder.HasStrMed();
    public static PropertyBuilder<string> HasPurposeRegNo(this PropertyBuilder<string> builder) => builder.HasStrCode();

    public static PropertyBuilder<string> HasPurposeHtml(this PropertyBuilder<string> builder) => builder.HasText();
    public static PropertyBuilder<string> HasPurposeEditor(this PropertyBuilder<string> builder) => builder.HasText();
    public static PropertyBuilder<string> HasPurposeJson(this PropertyBuilder<string> builder) => builder.HasText();
    public static PropertyBuilder<string> HasPurposeMarkdown(this PropertyBuilder<string> builder) => builder.HasText();
    public static PropertyBuilder<string> HasPurposeRichText(this PropertyBuilder<string> builder) => builder.HasText();

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
    public static ReferenceCollectionBuilder HasNoCascadeDelete(this ReferenceCollectionBuilder builder)
    {
        EfBoostPropertyConfiguration.ApplyNoCascadeDelete(builder.Metadata);
        return builder;
    }

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
        if (table != null) builder.ToTable(table, schema);
        else builder.ToTable(builder.Metadata.ClrType.Name, schema);
        return builder;
    }

    public static EntityTypeBuilder<T> HasViewKey<T>(this EntityTypeBuilder<T> builder, params string[] propertyNames) where T : class
    {
        builder.HasKey(propertyNames);
        return builder;
    }
}
