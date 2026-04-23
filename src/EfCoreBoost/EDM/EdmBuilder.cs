// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.ModelBuilder;
using System.Reflection;
using System.Xml;

namespace EfCore.Boost.EDM
{
    public static class EdmBuilder
    {
        /// <summary>
        /// Builds an OData EDM model from a collection of entity types with optional entity set names.
        /// If a DbContext is provided, it will be used to skip owned/keyless types and apply primary keys.
        /// </summary>
        /// <param name="entityTypes">A dictionary where the key is the entity set name and the value is the entity type.</param>
        /// <param name="context">Optional DbContext for EF metadata resolution.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder (e.g., adding functions or actions).</param>
        /// <returns>An IEdmModel containing the registered entity sets.</returns>
        private static Microsoft.OData.Edm.IEdmModel BuildEdmModelInternal(
            IEnumerable<KeyValuePair<string, Type>> entityTypes,
            DbContext? context = null,
            Action<ODataConventionModelBuilder>? configure = null)
        {
            var builder = new ODataConventionModelBuilder();
            var added = new List<(Type ClrType, EntityTypeConfiguration EdmType)>();

            foreach (var kvp in entityTypes)
            {
                if (context != null)
                {
                    var efType = context.Model.FindEntityType(kvp.Value);
                    if (efType?.IsOwned() == true) continue;
                    if (efType?.FindPrimaryKey() == null) continue;
                }

                var edmType = builder.AddEntityType(kvp.Value);
                builder.AddEntitySet(kvp.Key, edmType);
                added.Add((kvp.Value, edmType));
            }

            if (context != null)
            {
                foreach (var x in added)
                    EdmApplyEfPrimaryKey(x.EdmType, context, x.ClrType);
            }

            // Allow custom configuration (functions, actions, etc.)
            configure?.Invoke(builder);

            return builder.GetEdmModel();
        }

        /// <summary>
        /// Applies EF primary keys to the EDM entity type configuration.
        /// </summary>
        internal static void EdmApplyEfPrimaryKey(EntityTypeConfiguration edmType, DbContext ctx, Type clrType)
        {
            var et = ctx.Model.FindEntityType(clrType);
            var pk = et?.FindPrimaryKey();
            if (pk == null || pk.Properties.Count == 0) return;

            foreach (var k in pk.Properties)
            {
                var pi = clrType.GetProperty(k.Name, BindingFlags.Public | BindingFlags.Instance);
                if (pi == null)
                    throw new InvalidOperationException($"Primary key property '{k.Name}' not found on CLR type '{clrType.Name}'.");

                edmType.HasKey(pi);
            }
        }

        /// <summary>
        /// Builds an OData EDM model for a single entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type to include in the model.</typeparam>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>An IEdmModel containing the entity set.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromEntity<TEntity>(Action<ODataConventionModelBuilder>? configure = null)
        {
            return BuildEdmModelFromTypes([typeof(TEntity)], configure);
        }

        /// <summary>
        /// Recommended: Builds an OData EDM model from all repository properties exposed by a Unit of Work.
        /// The model surface is defined by the UOW, while the underlying DbContext is used for metadata resolution.
        /// </summary>
        /// <typeparam name="TUow">The Unit of Work type to scan.</typeparam>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder (e.g., adding functions or actions).</param>
        /// <returns>An IEdmModel containing entity sets for all discovered repositories.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromUow<TUow>(Action<ODataConventionModelBuilder>? configure = null)
            where TUow : class, EfCore.Boost.UOW.IDbReadUow, new()
        {
            using var uow = new TUow();
            return BuildEdmModelFromUow(uow, configure);
        }

        /// <summary>
        /// Builds an OData EDM model from all EfRepo and EfReadRepo properties defined in a Unit of Work type.
        /// NOTE: This overload does not use a DbContext instance, so EF-specific metadata (like primary keys) may be missing.
        /// </summary>
        /// <param name="uowType">The Unit of Work type to scan.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>An IEdmModel containing entity sets for all discovered repositories.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromUow(Type uowType, Action<ODataConventionModelBuilder>? configure = null)
        {
            if (uowType == null) throw new ArgumentNullException(nameof(uowType));
            var repoProps = GetUowRepoProperties(uowType);
            var types = repoProps.Select(p => new KeyValuePair<string, Type>(p.Name, p.PropertyType.GetGenericArguments()[0]));
            return BuildEdmModelFromTypes(types, configure);
        }

        /// <summary>
        /// Recommended: Builds an OData EDM model from a Unit of Work instance.
        /// Uses the repositories exposed by the UOW to define the model surface, and the UOW's internal DbContext for metadata.
        /// </summary>
        /// <param name="uow">The Unit of Work instance.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder (e.g., adding functions or actions).</param>
        /// <returns>An IEdmModel.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromUow(EfCore.Boost.UOW.IDbReadUow uow, Action<ODataConventionModelBuilder>? configure = null)
        {
            if (uow == null) throw new ArgumentNullException(nameof(uow));
            var context = uow.GetDbContext();
            var uowType = uow.GetType();
            var repoProps = GetUowRepoProperties(uowType);
            var types = repoProps.Select(p => new KeyValuePair<string, Type>(p.Name, p.PropertyType.GetGenericArguments()[0]));
            return BuildEdmModelInternal(types, context, configure);
        }

        private static IEnumerable<PropertyInfo> GetUowRepoProperties(Type uowType)
        {
            return uowType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType &&
                            (p.PropertyType.GetGenericTypeDefinition() == typeof(EfCore.Boost.DbRepo.EfRepo<>) ||
                             p.PropertyType.GetGenericTypeDefinition() == typeof(EfCore.Boost.DbRepo.EfReadRepo<>) ||
                             p.PropertyType.GetGenericTypeDefinition() == typeof(EfCore.Boost.DbRepo.EfLongIdRepo<>) ||
                             p.PropertyType.GetGenericTypeDefinition() == typeof(EfCore.Boost.DbRepo.EfLongIdReadRepo<>)));
        }

        /// <summary>
        /// Builds an OData EDM model from a DbContext instance.
        /// Use this when you want to expose all DbSet properties from a context.
        /// </summary>
        /// <param name="context">The DbContext instance.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>An IEdmModel.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromContext(DbContext context, Action<ODataConventionModelBuilder>? configure = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var ctxType = context.GetType();
            var dbSetGeneric = typeof(DbSet<>);
            var dbSetProps = ctxType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == dbSetGeneric);

            var types = dbSetProps.Select(p => new KeyValuePair<string, Type>(p.Name, p.PropertyType.GetGenericArguments()[0]));
            return BuildEdmModelInternal(types, context, configure);
        }

        /// <summary>
        /// Builds an OData EDM model from a collection of entity types with optional entity set names.
        /// </summary>
        /// <param name="entityTypes">A dictionary where the key is the entity set name and the value is the entity type.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>An IEdmModel containing the registered entity sets.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromTypes(IEnumerable<KeyValuePair<string, Type>> entityTypes, Action<ODataConventionModelBuilder>? configure = null)
        {
            return BuildEdmModelInternal(entityTypes, null, configure);
        }

        /// <summary>
        /// Builds an OData EDM model from a manually selected collection of entity types.
        /// Entity set names will default to the type names.
        /// </summary>
        /// <param name="entityTypes">The entity types to include in the model.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>An IEdmModel containing entity sets for all provided types.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromTypes(IEnumerable<Type> entityTypes, Action<ODataConventionModelBuilder>? configure = null)
        {
            return BuildEdmModelFromTypes(entityTypes.Select(t => new KeyValuePair<string, Type>(t.Name, t)), configure);
        }

        /// <summary>
        /// Builds an OData EDM model from multiple manually selected entity types.
        /// Entity set names will default to the type names.
        /// </summary>
        /// <param name="entityTypes">The entity types to include in the model.</param>
        /// <returns>An IEdmModel containing entity sets for all provided types.</returns>
        public static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromTypes(params Type[] entityTypes)
        {
            return BuildEdmModelFromTypes((IEnumerable<Type>)entityTypes, null);
        }

        /// <summary>
        /// Builds an OData EDM model from all DbSet properties in a DbContext.
        /// </summary>
        /// <typeparam name="T">The DbContext type.</typeparam>
        /// <returns>An IEdmModel containing entity sets for all DbSets.</returns>
        private static Microsoft.OData.Edm.IEdmModel BuildEdmModelFromContext<T>() where T : DbContext, new()
        {
            using var context = new T();
            return BuildEdmModelFromContext(context);
        }

        /// <summary>
        /// Serializes an IEdmModel to CSDL XML.
        /// </summary>
        /// <param name="model">The EDM model to serialize.</param>
        /// <returns>A CSDL XML string, or error messages if serialization fails.</returns>
        internal static string SerializeEdmModelXml(Microsoft.OData.Edm.IEdmModel model)
        {
            using var sw = new StringWriter();
            using var xw = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true });
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

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model from all DbSet properties in a DbContext.
        /// </summary>
        /// <typeparam name="T">The DbContext type.</typeparam>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromContext<T>() where T : DbContext, new()
        {
            using var context = new T();
            return BuildXmlModelFromContext(context);
        }

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model from a DbContext instance.
        /// </summary>
        /// <param name="context">The DbContext instance.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromContext(DbContext context, Action<ODataConventionModelBuilder>? configure = null)
        {
            return SerializeEdmModelXml(BuildEdmModelFromContext(context, configure));
        }

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model for a single entity type.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromEntity<TEntity>(Action<ODataConventionModelBuilder>? configure = null)
        {
            return SerializeEdmModelXml(BuildEdmModelFromEntity<TEntity>(configure));
        }

        /// <summary>
        /// Recommended: Builds a CSDL XML representation of the EDM model from all repository properties exposed by a Unit of Work.
        /// </summary>
        /// <typeparam name="TUow">The Unit of Work type.</typeparam>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromUow<TUow>(Action<ODataConventionModelBuilder>? configure = null)
             where TUow : class, EfCore.Boost.UOW.IDbReadUow, new()
        {
            return SerializeEdmModelXml(BuildEdmModelFromUow<TUow>(configure));
        }

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model from all repository properties in a Unit of Work type.
        /// </summary>
        /// <param name="uowType">The Unit of Work type.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromUow(Type uowType, Action<ODataConventionModelBuilder>? configure = null)
        {
            return SerializeEdmModelXml(BuildEdmModelFromUow(uowType, configure));
        }

        /// <summary>
        /// Recommended: Builds a CSDL XML representation of the EDM model from a Unit of Work instance.
        /// </summary>
        /// <param name="uow">The Unit of Work instance.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromUow(EfCore.Boost.UOW.IDbReadUow uow, Action<ODataConventionModelBuilder>? configure = null)
        {
            return SerializeEdmModelXml(BuildEdmModelFromUow(uow, configure));
        }

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model from multiple manually selected entity types.
        /// </summary>
        /// <param name="entityTypes">The entity types to include.</param>
        /// <param name="configure">Optional callback to customize the ODataConventionModelBuilder.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromTypes(IEnumerable<Type> entityTypes, Action<ODataConventionModelBuilder>? configure = null)
        {
            return SerializeEdmModelXml(BuildEdmModelFromTypes(entityTypes, configure));
        }

        /// <summary>
        /// Builds a CSDL XML representation of the EDM model from multiple manually selected entity types.
        /// </summary>
        /// <param name="entityTypes">The entity types to include.</param>
        /// <returns>CSDL XML string.</returns>
        public static string BuildXmlModelFromTypes(params Type[] entityTypes)
        {
            return SerializeEdmModelXml(BuildEdmModelFromTypes(entityTypes));
        }
    }
}

