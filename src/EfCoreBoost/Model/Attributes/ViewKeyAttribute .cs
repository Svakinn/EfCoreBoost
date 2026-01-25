using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.Model.Attributes
{
    /// <summary>
    /// Declares one or more properties that uniquely identify rows in a database view.
    /// Used by EF Boost conventions to configure .HasKey() for read-only entities.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ViewKeyAttribute : Attribute
    {
        public string[] Properties { get; }

        public ViewKeyAttribute(params string[] properties)
            => Properties = properties ?? Array.Empty<string>();
    }
}
