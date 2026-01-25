using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.Model.Attributes
{
    /// <summary>
    /// Optional schema/table hint for EF Boost conventions.
    /// If only schema is given, the class name is used as the table name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class DbSchemaAttribute : Attribute
    {
        /// <summary>Database schema name (e.g. "att" or "main").</summary>
        public string Schema { get; }

        /// <summary>Optional explicit table name (defaults to class name).</summary>
        public string? Table { get; }

        public DbSchemaAttribute(string schema) => Schema = schema;

        public DbSchemaAttribute(string schema, string table)
        {
            Schema = schema;
            Table = table;
        }
    }
}
