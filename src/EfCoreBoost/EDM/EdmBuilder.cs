// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.ModelBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace EfCore.Boost.EDM
{
    public static class EdmBuilder
    {
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromContext<T>() where T : DbContext, new()
        {
            var builder = new ODataConventionModelBuilder();

            var dbContextType = typeof(T);
            var dbSetProps = dbContextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));

            foreach (var prop in dbSetProps)
            {
                var entityType = prop.PropertyType.GetGenericArguments()[0];
                builder.AddEntitySet(prop.Name, builder.AddEntityType(entityType));
            }

            return builder.GetEdmModel();
        }

        public static string SerializeEdmModelXML(Microsoft.OData.Edm.IEdmModel model)
        {
            using (var sw = new StringWriter())
            using (var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true }))
            {
                if (CsdlWriter.TryWriteCsdl(model, xw, CsdlTarget.OData, out var errors))
                {
                    xw.Flush();
                    return sw.ToString(); // EDMX XML
                }
                else
                {
                    return string.Join("\n", errors.Select(e => e.ErrorMessage));
                }
            }
        }

        public static string BuildXMLModelFromContext<T>() where T : DbContext, new()
        {
            return SerializeEdmModelXML(BuildEdmModelFromContext<T>());
        }
    }
}

