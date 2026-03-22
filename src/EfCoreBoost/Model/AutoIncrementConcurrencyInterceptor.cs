using EfCore.Boost.Model.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Concurrent;

namespace EfCore.Boost.Model
{
    public sealed class AutoIncrementConcurrencyInterceptor : SaveChangesInterceptor
    {
        private readonly ConcurrentDictionary<IEntityType, IProperty?> _cache = new();
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            var ctx = eventData.Context;
            if (ctx != null)
                Apply(ctx.ChangeTracker);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var ctx = eventData.Context;
            if (ctx != null)
                Apply(ctx.ChangeTracker);
            return ValueTask.FromResult(result);
        }

        private void Apply(ChangeTracker changeTracker)
        {
            foreach (var entry in changeTracker.Entries())
            {
                if (entry.State != EntityState.Modified)
                    continue;
                var versionProperty = _cache.GetOrAdd(entry.Metadata, FindVersionTokenProperty);
                if (versionProperty == null)
                    continue;
                var propertyEntry = entry.Property(versionProperty.Name);
                if (propertyEntry.OriginalValue == null)
                    continue; // Detached updates without original value -> skip
                if (versionProperty.ClrType == typeof(long))
                {
                    var original = (long)propertyEntry.OriginalValue;
                    propertyEntry.CurrentValue = original + 1L;
                    propertyEntry.IsModified = true;
                }
                else if (versionProperty.ClrType == typeof(int))
                {
                    var original = (int)propertyEntry.OriginalValue;
                    propertyEntry.CurrentValue = original + 1;
                    propertyEntry.IsModified = true;
                }
            }
        }

        private static IProperty? FindVersionTokenProperty(IEntityType entityType)
        {
            IProperty? match = null;
            foreach (var p in entityType.GetProperties())
            {
                //Only applies on integers or long
                if (p.ClrType != typeof(long) && p.ClrType != typeof(int))
                    continue;
                var pi = p.PropertyInfo;
                if (pi == null)
                    continue; // Ignore shadow properties
                if (Attribute.IsDefined(pi, typeof(AutoIncrementAttribute), inherit: true) || Attribute.IsDefined(pi, typeof(AutoIncrementConcurrencyAttribute), inherit: true))
                {
                    match = p;
                    break;
                }
            }
            return match;
        }
    }
}
