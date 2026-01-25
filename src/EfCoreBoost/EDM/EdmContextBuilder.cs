// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.ModelBuilder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace EfCore.Boost.EDM
{
    public static class EdmContextBuilder
    {
        public static IEdmModel BuildEdmModelFromContext(DbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var builder = new ODataConventionModelBuilder();
            var ctxType = context.GetType();
            var dbSetGeneric = typeof(DbSet<>);

            //Keep for 2nd pass to apply EF keys (and avoid “keyless EDM” surprises)
            var added = new List<(Type ClrType, EntityTypeConfiguration EdmType)>();

            foreach (var prop in ctxType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.PropertyType.IsGenericType) continue;
                if (prop.PropertyType.GetGenericTypeDefinition() != dbSetGeneric) continue;

                var clrType = prop.PropertyType.GenericTypeArguments[0];

                //Skip EF owned/keyless types (OData entity sets require keys)
                var efType = context.Model.FindEntityType(clrType);
                if (efType?.IsOwned() == true) continue;
                if (efType?.FindPrimaryKey() == null) continue;

                var edmType = builder.AddEntityType(clrType);
                builder.AddEntitySet(prop.Name, edmType);

                added.Add((clrType, edmType));
            }

            //Apply EF primary keys into EDM, so OData validation ($filter/$expand) behaves correctly
            foreach (var x in added)
                EdmApplyEfPrimaryKey(x.EdmType, context, x.ClrType);

            return builder.GetEdmModel();
        }

        //Working PK bridge (PropertyInfo-based)
        static void EdmApplyEfPrimaryKey(EntityTypeConfiguration edmType, DbContext ctx, Type clrType)
        {
            var et = ctx.Model.FindEntityType(clrType);
            var pk = et?.FindPrimaryKey();
            if (pk == null || pk.Properties.Count == 0) return;

            foreach (var k in pk.Properties)
            {
                var pi = clrType.GetProperty(k.Name, BindingFlags.Public | BindingFlags.Instance);
                if (pi == null)
                    throw new InvalidOperationException(
                        $"Primary key property '{k.Name}' not found on CLR type '{clrType.Name}'.");

                //OData expects PropertyInfo for keys
                edmType.HasKey(pi);
            }
        }

        public static string SerializeEdmModelXML(IEdmModel model)
        {
            using var sw = new StringWriter();
            using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true });

            if (CsdlWriter.TryWriteCsdl(model, xw, CsdlTarget.OData, out var errors))
            {
                xw.Flush();
                return sw.ToString(); // EDMX XML
            }
            return string.Join("\n", errors.Select(e => e.ErrorMessage));
        }

        public static string BuildXMLModelFromContext(DbContext context)
        {
            var model = BuildEdmModelFromContext(context);
            return SerializeEdmModelXML(model);
        }
    }
}
