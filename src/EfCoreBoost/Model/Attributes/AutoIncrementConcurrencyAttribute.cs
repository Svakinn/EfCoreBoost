using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.Model.Attributes
{
    /// <summary>
    /// Declares Integer Or Long value as ConcurrencyChecked attribute but also provides automatic increment by boost on save
    /// This is Cross-platform safe concurrency checking where increments are handled by boost.
    /// If you want the db to handle auto-updates use the standard ef´s [ConcurrencyCheck] instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class AutoIncrementConcurrencyAttribute : Attribute
    {
        public string[] Properties { get; }

        public AutoIncrementConcurrencyAttribute(params string[] properties)   => Properties = properties ?? Array.Empty<string>();
    }

    /// <summary>
    /// This one is related to AutoIncrementConcurrency but here we do not let EF-enforce concurrency.
    /// Instead we provide cross plattform row-counter that enables "manual" concurrency only where applicable.
    /// For example if you are saving form from youser, then you chekck the RowCounter vs. the one in db and throw exeption if not matched
    /// The benefit is that you can controll when you want concurrency where EF-Core forces it every time with AutoIncrementConcurrency
    /// and you cannot turn it off.
    /// </summary>

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class AutoIncrementAttribute : Attribute
    {
        public string[] Properties { get; }

        public AutoIncrementAttribute(params string[] properties) => Properties = properties ?? Array.Empty<string>();
    }
}
