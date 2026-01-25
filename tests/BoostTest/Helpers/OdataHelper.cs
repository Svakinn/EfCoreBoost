using BoostTest.TestDb;
using EfCore.Boost;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BoostTest.TestDb.DbTest;

namespace BoostTest.Helpers
{
    public static class OdataTestHelper
    {
        private static IServiceProvider BuildAspNetODataServices(IEdmModel model, string routePrefix)
        {
            var sc = new ServiceCollection();
            sc.AddLogging();
            sc.AddRouting();

            // This is the important part: register ASP.NET Core OData with route components and query features
            sc.AddControllers().AddOData(opt =>
            {
                opt.AddRouteComponents(routePrefix, model)
                   .Filter().OrderBy().Select().Expand().Count()
                   .SetMaxTop(null);
            });

            return sc.BuildServiceProvider();
        }

        private static IEdmEntitySet FindEntitySetFor<TEntity>(IEdmModel model)
        {
            var container = model.EntityContainer ?? throw new InvalidOperationException("EDM model has no entity container.");
            var clrName = typeof(TEntity).Name;

            // If your EDM builder used DbSet property names, this might not match CLR type name.
            // This fallback matches by EDM element type name.
            foreach (var es in container.EntitySets())
            {
                if (es.Type is IEdmCollectionType col &&
                    col.ElementType.Definition is IEdmEntityType et &&
                    et.Name == clrName)
                    return es;
            }
            throw new InvalidOperationException($"EDM entity set not found for '{clrName}'. Ensure TEntity is exposed as a DbSet and included in EDM.");
        }


        public static ODataQueryOptions<TEntity> CreateOptions<TEntity>(UOWTestDb uow, string queryString, string routePrefix = "odata")
        {
            var model = uow.GetModel();
            var sp = BuildAspNetODataServices(model, routePrefix);

            var httpContext = new DefaultHttpContext { RequestServices = sp };
            var request = httpContext.Request;
            request.Method = HttpMethods.Get;

            if (string.IsNullOrWhiteSpace(queryString)) queryString = "";
            if (!queryString.StartsWith('?')) queryString = "?" + queryString;
            while (queryString.StartsWith("??", StringComparison.Ordinal)) queryString = queryString[1..];
            request.QueryString = new QueryString(queryString);

            var set = FindEntitySetFor<TEntity>(model);
            var path = new ODataPath(new EntitySetSegment(set));

            // Attach OData feature state
            var feat = httpContext.ODataFeature();
            feat.Model = model;
            feat.Path = path;

            // Make the request look like it belongs to the OData route component
            request.Path = new PathString($"/{routePrefix}/{set.Name}");

            var context = new ODataQueryContext(model, typeof(TEntity), path);
            return new ODataQueryOptions<TEntity>(context, request);
        }

    }
      
}
