using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost.Model.Attributes
{
    public static class EfForeignKeyAttributes
    {
        [AttributeUsage(AttributeTargets.Property)]
        public sealed class NoCascadeDeleteAttribute : Attribute { }  //Never have cascade delets on this relationship

        [AttributeUsage(AttributeTargets.Property)]
        public sealed class CascadeDeleteAttribute : Attribute { }  //Override defaults to have cascade deletes active
    }
}
