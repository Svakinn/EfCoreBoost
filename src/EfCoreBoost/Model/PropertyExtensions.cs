using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EfCore.Boost.Model;
/// <summary>
/// Make our custom attributes work fluently as well
/// </summary>
public static class EfBoostPropertyExtensions
{
    public static PropertyBuilder<long> HasDbAutoUid(this PropertyBuilder<long> builder)
    {
        builder.Metadata.SetAnnotation("EfBoost:DbAutoUid", true);
        return builder;
    }
}
