
namespace EfCore.Boost.Model.Attributes
{
    /// <summary>
    /// Declares Integer Or Long value as ConcurrencyChecked attribute but also provides automatic increment by boost on save
    /// This is Cross-platform safe concurrency checking where increments are handled by boost.
    /// If you want the db to handle auto-updates use the standard ef´s [ConcurrencyCheck] instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class AutoIncrementConcurrencyAttribute(params string[] properties) : Attribute
    {
        public string[] Properties { get; } = properties;
    }

    /// <summary>
    /// This one is related to AutoIncrementConcurrency but here we do not let EF-enforce concurrency.
    /// Instead, we provide cross plattform row-counter that enables "manual" concurrency only where applicable.
    /// For example if you are saving form from youser, then you chekck the RowCounter vs. the one in db and throw exeption if not matched
    /// The benefit is that you can controll when you want concurrency where EF-Core forces it every time with AutoIncrementConcurrency
    /// and you cannot turn it off.
    /// </summary>

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class AutoIncrementAttribute(params string[] properties) : Attribute
    {
        public string[] Properties { get; } = properties;
    }
}
