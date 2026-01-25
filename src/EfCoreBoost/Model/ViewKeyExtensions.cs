using System.Reflection;
using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;

namespace EfCore.Boost.Model
{
    public static class ViewKeyExtensions
    {
        public static void ApplyViewKeyConvention(this ModelBuilder modelBuilder)
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                var clr = entity.ClrType;
                if (clr == null) continue;

                // Only for view entities
                if (!Attribute.IsDefined(clr, typeof(ViewKeyAttribute), false))
                    continue;

                var keyAttr = clr.GetCustomAttribute<ViewKeyAttribute>();
                if (keyAttr == null || keyAttr.Properties.Length == 0) continue;

                modelBuilder.Entity(clr).HasKey(keyAttr.Properties);
            }
        }
    }
}
