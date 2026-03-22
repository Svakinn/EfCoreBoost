
namespace EfCore.Boost.Model.Attributes
{
    /// <summary>
    /// Declares one or more properties that uniquely identify rows in a database view.
    /// Used by EF Boost conventions to configure .HasKey() for read-only entities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ViewKeyAttribute(params string[] properties) : Attribute
    {
        public string[] Properties { get; } = properties;
    }
}
