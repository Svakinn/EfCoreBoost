using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EfCore.Boost.Model;

internal static class EfBoostPropertyConfiguration
{
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

    internal static void ApplyStringSize(IMutableProperty property, int? maxLen, string annotationName, bool isSqlServer, bool isNpgsql, bool isMySql)
    {
        if (property.ClrType != typeof(string)) return;
        if (property.GetMaxLength().HasValue) return;
        property.SetAnnotation(annotationName, true);
        if (maxLen.HasValue) property.SetMaxLength(maxLen.Value);
        if (isSqlServer) property.SetColumnType(maxLen.HasValue ? $"nvarchar({maxLen.Value})" : "nvarchar(max)");
        else if (isNpgsql) property.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "text");
        else if (isMySql) property.SetColumnType(maxLen.HasValue ? $"varchar({maxLen.Value})" : "longtext");
    }

    internal static void ApplyPrecision(IMutableProperty property, int precision, int scale, string annotationName)
    {
        var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (clrType != typeof(decimal)) return;
        if (property.GetPrecision().HasValue || property.GetScale().HasValue) return;
        property.SetAnnotation(annotationName, true);
        property.SetPrecision(precision);
        property.SetScale(scale);
    }

    internal static void ApplyAutoIncrementConcurrency(IMutableProperty property, ModelBuilder modelBuilder)
    {
        property.SetAnnotation(EfBoostAnnotationNames.AutoIncrementConcurrency, true);
        property.IsConcurrencyToken = true;
        EnsureDefaultNumIfMissing(modelBuilder, property);
    }

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

    internal static void ApplyNoCascadeDelete(IMutableForeignKey fk)
    {
        fk.DeleteBehavior = DeleteBehavior.Restrict;
        fk.SetAnnotation(EfBoostAnnotationNames.NoCascadeDelete, true);
    }

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
