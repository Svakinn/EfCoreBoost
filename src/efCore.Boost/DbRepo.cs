// Revised version of DbRepo.cs
// Includes: EDM metadata support, scalar query helpers, async-only design, tracked vs. non-tracked distinction, OData integration, bulk insert/delete helpers

using Microsoft.AspNetCore.OData.Query;
//using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.ModelBuilder;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Xml;

namespace DbRepo;

/// 
/// Enumeration to define the supported database engines.
/// Used to adapt quoting and raw SQL execution based on provider
public enum DatabaseType
{
    Unknown,
    SqlServer,
    PostgreSql,
    MySql,
    Sqlite,
    Oracle,
    InMemory
}

/// 
/// Interface that exposes an IQueryable for OData-compatible queries.
/// Implement this interface to allow your repository to participate in OData pipelines.
public interface IODataQueryable<T> where T : class
{
    IQueryable<T> AsODataQueryable();
}

/// 
/// Read-only repository interface supporting async operations and OData.
/// This provides non-tracking queries for efficiency and safety in readonly contexts.
public interface IAsyncReadRepo<T> where T : class
{
    /// Return an IQueryable for LINQ composition with AsNoTracking by default.
    public IQueryable<T> QueryNoTrack();

    /// <summary>
    /// Find an entity by ID using non-tracking query.
    /// </summary>
    Task<T?> ByKeyAsync(params object[] key);

    /// <summary>
    /// Returns the first entity matching a filter, without tracking.
    /// </summary>
    Task<T?> FirstNoTrackAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Run query and return results using optional filter. No tracking.
    /// </summary>
    Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Streams results using asynchronous iteration (good for large datasets).
    /// </summary>
    IAsyncEnumerable<T> StreamNoTrackAsync(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Check if any rows match filter using non-tracking query.
    /// </summary>
    Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Count the number of matching entities. Uses non-tracking query.
    /// </summary>
    Task<long> CountAsync(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Add OData filters (Filter, OrderBy, Skip, Top) to a LINQ query.
    /// This modifies the query but does not execute it.
    /// Useful when you want to inspect or continue modifying the query afterward.
    /// Note: The base LINQ query is preserved when applying OData filters.
    /// Any filtering logic from the original query is combined with the OData filter using logical AND.
    /// This means OData cannot override or remove the base constraints — it only narrows the result.
    /// </summary>
    /// <param name="options">Incoming ODataQueryOptions from controller</param>
    /// <param name="query">Base query to modify</param>
    /// <param name="skipFilter">Skip filtering expression</param>
    /// <param name="skipTop">Skip paging (top/skip)</param>
    /// <param name="skipOrder">Skip ordering</param>
    /// <returns>Modified IQueryable</returns>
    IQueryable<T> AddOdataFilter(ODataQueryOptions<T> options, IQueryable<T> query, bool skipFilter = false, bool skipTop = false, bool skipOrder = false);

    /// <summary>
    /// Applies and executes OData query options on a LINQ query, returning results and count.
    /// Useful when building paged API responses.
    /// Note: The base LINQ query is preserved when applying OData filters.
    /// Any filtering logic from the original query is combined with the OData filter using logical AND.
    /// This means OData cannot override or remove the base constraints — it only narrows the result.
    /// </summary>
    /// <param name="options">Incoming ODataQueryOptions from controller</param>
    /// <param name="query">Base query to modify</param>
    /// <param name="skipFilter">Skip filtering expression</param>
    /// <param name="skipTop">Skip paging (top/skip)</param>
    /// <param name="skipOrder">Skip ordering</param>
    /// <returns>Full api result with populated date based on the original query were odada has been aded to</returns>
    Task<ApiQueryResult<T>> ApplyOdataFilterAsync(ODataQueryOptions<T> options, IQueryable<T> query, bool skipFilter = false, bool skipTop = false, bool skipOrder = false);

    /// <summary>
    /// Run a raw SQL statement returning a nullable boolean.
    /// Typically used to retrieve scalar values such as flags or existence checks.
    /// </summary>
    Task<bool?> GetBoolScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Run a raw SQL statement returning a nullable long.
    /// Commonly used for IDs or aggregate results.
    /// </summary>
    Task<long?> GetLongScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Run a raw SQL statement returning a nullable decimal.
    /// Suitable for retrieving totals or monetary values.
    /// </summary>
    Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Returns EDM metadata as XML for the given entity.
    /// Used to support metadata exchange in OData setups.
    /// </summary>
    string EntityMeta();
}

public interface IAsyncRepo<T> : IAsyncReadRepo<T>, IODataQueryable<T> where T : class
{
    public IQueryable<T> Query();
    Task<T?> FirstAsync(Expression<Func<T, bool>> filter);
    void Add(T entity);
    void AddRaw(T entity);
    void AddRange(IEnumerable<T> entities);
    void AddRangeRaw(IEnumerable<T> entities);
    void Delete(T entity);
    void DeleteMany(IEnumerable<T> entities);
    Task BulkDeleteAsync(IEnumerable<long> ids);
    Task BulkInsertAsync(List<T> items);
    void MarkModified(T entity);
}

public interface IAsyncLongIdRepo<T> : IAsyncRepo<T> where T : class
{
    Task<T?> ByIdNoTrackAsync(long id);
    Task<T?> ByIdAsync(long id);
}

public interface IAsyncLongIdReadRepo<T> : IAsyncReadRepo<T> where T : class
{
    Task<T?> ByIdNoTrackAsync(long id);
}

public class EfReadRepo<T>(DbContext dbContext, DatabaseType dbType = DatabaseType.SqlServer) : IAsyncReadRepo<T>, IODataQueryable<T> where T : class
{
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();
    protected readonly DbContext Ctx = dbContext;
    protected readonly DatabaseType DbType = dbType;


    public IQueryable<T> QueryNoTrack() => DbSet.AsNoTracking();

    public IQueryable<T> AsODataQueryable() => DbSet.AsNoTracking();

    public async Task<T?> ByKeyNoTrackAsync(params object[] key)
    {
        var e = await DbSet.FindAsync(key);
        if (e != null)
            Ctx.Entry(e).State = EntityState.Detached; return e;
    }
    public Task<T?> ByKeyAsync(params object[] key) => DbSet.FindAsync(key).AsTask();

    public async Task<T?> FirstNoTrackAsync(Expression<Func<T, bool>> filter) =>
        await DbSet.AsNoTracking().FirstOrDefaultAsync(filter);

    public async Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return await query.ToListAsync();
    }

    public IAsyncEnumerable<T> StreamNoTrackAsync(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return query.AsAsyncEnumerable();
    }

    public async Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter) =>
        await DbSet.AsNoTracking().AnyAsync(filter);

    public async Task<long> CountAsync(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return await query.LongCountAsync();
    }

    /// <summary>
    /// Add odata-queryoptions onto our query: Filter, OrderBy, Skip and Top
    /// The query itsef is not appled, you must execute the query afterwards i.e. by  await query.ToListAsync();
    /// We can choose to not apply some part of the options, like paging or ordrby.  This is handy if we intend to use the query later for group by or some other oredering
    /// For example if we are to use the odata-filter for filtering a query and then want to add our group by and or counting on the dataset, then it makes sense to make sure 
    /// queryoptions for top/skip and sorting is not applied
    /// </summary>
    /// <param name="options">ODataQueryOptions, usually from the controller handling the odata querye</param>
    /// <param name="query">Base LinQ query we want to add our odata filter,sort and paging onto</param>
    /// <param name="skipFilter">If we do not want to add the filter in the options</param>
    /// <param name="skipTop">If we do not want to add the paging in the options (skip/top)</param>
    /// <param name="skipOrder">If we dnot want to add the order by part of the options</param>
    /// <returns></returns>
    public IQueryable<T> AddOdataFilter(ODataQueryOptions<T> options, IQueryable<T> query, bool skipFilter = false, bool skipTop = false, bool skipOrder = false)
    {
        if (options == null)
        {
            return query;
        }
        else
        {
            if (!skipFilter && options.Filter != null)
                query = options.Filter.ApplyTo(query, new ODataQuerySettings()).OfType<T>();
            if (options.OrderBy != null && !skipOrder)
                query = options.OrderBy.ApplyTo(query);
            if (options.Skip != null && !skipTop)
                query = options.Skip.ApplyTo(query, new ODataQuerySettings());
            if (options.Top != null && !skipTop)
                query = options.Top.ApplyTo(query, new ODataQuerySettings());
        }
        return query;
    }

    /// <summary>
    /// Apply and execute odata-queryoptions onto our query, returning the data and count as apiresult
    /// </summary>
    /// <param name="options"></param>
    /// <param name="query"></param>
    /// <param name="skipFilter"></param>
    /// <param name="skipTop"></param>
    /// <param name="skipOrder"></param>
    /// <returns></returns>
    public async Task<ApiQueryResult<T>> ApplyOdataFilterAsync(ODataQueryOptions<T> options, IQueryable<T> query, bool skipFilter = false, bool skipTop = false, bool skipOrder = false)
    {
        var res = new ApiQueryResult<T> { PageNo = 1 };
        if (options == null)
        {
            res.Results = await query.ToListAsync();
            res.InlineCount = res.Results.Count;
        }
        else
        {
            if (!skipFilter && options.Filter != null)
                query = options.Filter.ApplyTo(query, new ODataQuerySettings()).OfType<T>();

            res.InlineCount = await query.CountAsync();

            if (res.InlineCount == 0)
            {
                res.Results = new List<T>();
            }
            else
            {
                if (options.OrderBy != null && !skipOrder)
                    query = options.OrderBy.ApplyTo(query);

                if (options.Skip != null && !skipTop)
                    query = options.Skip.ApplyTo(query, new ODataQuerySettings());

                if (options.Top != null && !skipTop)
                    query = options.Top.ApplyTo(query, new ODataQuerySettings());

                res.Results = await query.ToListAsync();

                if (!skipTop && options.Skip != null && options.Top != null)
                    res.PageNo = (options.Skip.Value / options.Top.Value) + 1;
            }
        }

        res.Results ??= new List<T>();
        return res;
    }

    public async Task<bool?> GetBoolScalarAsync(string query, params object[] parameters)
    {
        var result = await Ctx.Database.ExecuteSqlRawAsync(query, parameters);
        return result != 0;
    }

    public async Task<long?> GetLongScalarAsync(string query, params object[] parameters)
    {
        using var cmd = Ctx.Database.GetDbConnection().CreateCommand();
        if (cmd == null || cmd.Connection == null) return null;
        cmd.CommandText = query;
        foreach (var p in parameters)
        {
            if (p is not null) cmd.Parameters.Add(p);
        }
        if (cmd.Connection.State != ConnectionState.Open)
            await cmd.Connection.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt64(result) : null;
    }

    public async Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters)
    {
        using var cmd = Ctx!.Database.GetDbConnection().CreateCommand();
        if (cmd == null || cmd.Connection == null) return null;
        cmd.CommandText = query;
        foreach (var p in parameters)
        {
            if (p is not null) cmd.Parameters.Add(p);
        }
        if (cmd.Connection.State != ConnectionState.Open)
            await cmd.Connection.OpenAsync();
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToDecimal(result) : null;
    }

    public string EntityMeta()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<T>(typeof(T).Name);
        var edmModel = builder.GetEdmModel();
        return EdmToXml(edmModel);
    }

    private static string EdmToXml(IEdmModel model)
    {
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });
        CsdlWriter.TryWriteCsdl(model, xmlWriter, CsdlTarget.OData, out _);
        xmlWriter.Flush();
        return stringWriter.ToString();
    }
}

public partial class EfRepo<T>(DbContext dbContext, DatabaseType dbType) : EfReadRepo<T>(dbContext, dbType), IAsyncRepo<T> where T : class
{
    public IQueryable<T> Query() => DbSet;
    public new IQueryable<T> AsODataQueryable() => DbSet;


    public Task<T?> FirstAsync(Expression<Func<T, bool>> filter) => DbSet.FirstOrDefaultAsync(filter);

    public void Add(T entity) => DbSet.Add(entity);

    public void AddRaw(T entity)
    {
        Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        DbSet.Add(entity);
    }

    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => DbSet.Where(predicate).ExecuteDeleteAsync();

    public void AddRange(IEnumerable<T> entities) => DbSet.AddRange(entities);

    public void AddRangeRaw(IEnumerable<T> entities)
    {
        Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
        DbSet.AddRange(entities);
    }

    public void Delete(T entity)
    {
        var entry = Ctx.Entry(entity);
        if (entry.State != EntityState.Deleted)
            entry.State = EntityState.Deleted;
        else
        {
            DbSet.Attach(entity);
            DbSet.Remove(entity);
        }
    }

    public void DeleteMany(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
            Delete(entity);
    }

    public async Task BulkDeleteAsync(IEnumerable<long> ids)
    {
        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity type not found in model.");
        var schema = entityType.GetSchema();
        var tableName = entityType.GetTableName();
        var fullTableName = DbType switch
        {
            DatabaseType.SqlServer => $"[{schema}].[{tableName}]",
            DatabaseType.PostgreSql => $"\"{schema}\".\"{tableName}\"",
            DatabaseType.MySql => $"`{schema}`_`{tableName}`", //mySQl has no schemas -> the schema is mapped to the start of the table
            _ => throw new NotSupportedException($"Bulk delete not supported for DB type: {DbType}")
        };

        var idList = ids.Select(id => Quote(id.ToString())).ToList();
        var quotedIdColumn = Quote("Id");
        var cmdText = $"DELETE FROM {fullTableName} WHERE {quotedIdColumn} IN ({string.Join(",", idList)})";
        await Ctx.Database.ExecuteSqlRawAsync(cmdText);
    }

    public async Task BulkInsertAsync(List<T> items)
    {
        if (DbType != DatabaseType.SqlServer)
            throw new NotSupportedException("Bulk insert is supported only for SQL Server");

        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var tableName = $"[{entityType.GetSchema()}].[{entityType.GetTableName()}]";

        var dt = new DataTable();
        var props = typeof(T).GetProperties();
        var includedProps = new List<PropertyInfo>();
        foreach (var prop in props)
        {
            var dbGeneratedAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
            if (dbGeneratedAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                continue;
            if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                continue;
            includedProps.Add(prop);
            dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
        }
        foreach (var item in items)
        {
            var values = new object[includedProps.Count];
            for (int i = 0; i < includedProps.Count; i++)
            {
                var value = includedProps[i].GetValue(item);
                values[i] = value ?? DBNull.Value;
            }
            dt.Rows.Add(values);
        }

        var conn = Ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();
        if (conn is not SqlConnection sqlConn)
            throw new InvalidOperationException("Bulk insert requires SQL Server connection");

        using var bulkCopy = new SqlBulkCopy(sqlConn) { DestinationTableName = tableName };
        foreach (DataColumn col in dt.Columns)
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        await bulkCopy.WriteToServerAsync(dt);
    }

    public async Task PgBulkInsertAsync(List<T> items)
    {
        if (DbType != DatabaseType.PostgreSql)
            throw new NotSupportedException("Bulk insert is supported only for PostgreSQL");

        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var schema = entityType.GetSchema() ?? "public";
        var tableName = $"{schema}.\"{entityType.GetTableName()}\"";

        var props = typeof(T).GetProperties();
        var includedProps = new List<PropertyInfo>();
        foreach (var prop in props)
        {
            var dbGeneratedAttr = prop.GetCustomAttribute<DatabaseGeneratedAttribute>();
            if (dbGeneratedAttr?.DatabaseGeneratedOption == DatabaseGeneratedOption.Identity)
                continue;
            if (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string))
                continue;
            includedProps.Add(prop);
        }

        var conn = Ctx.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync();

        if (conn is not Npgsql.NpgsqlConnection pgConn)
            throw new InvalidOperationException("Bulk insert requires PostgreSQL connection");

        using var writer = await pgConn.BeginBinaryImportAsync($"COPY {tableName} ({string.Join(", ", includedProps.Select(p => $"\"{p.Name}\""))}) FROM STDIN (FORMAT BINARY)");
        foreach (var item in items)
        {
            await writer.StartRowAsync();
            foreach (var prop in includedProps)
            {
                var val = prop.GetValue(item);
                if (val == null)
                    await writer.WriteNullAsync();
                else
                {
                    var typ = InferNpgsqlDbType(prop.PropertyType);
                    if (typ == NpgsqlDbType.Timestamp)
                        val = PrepareForPostgres(val);
                    await writer.WriteAsync(val, typ); 
                }
            }
        }
        await writer.CompleteAsync();
    }

    public static object? PrepareForPostgres(object? value)
    {
        if (value is DateTime dt)
            return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        var type = value?.GetType();
        if (type == typeof(DateTime?))
        {
            var ndt = (DateTime?)value;
            return ndt.HasValue ? DateTime.SpecifyKind(ndt.Value, DateTimeKind.Unspecified) : null;
        }
        return value;
    }


    static NpgsqlDbType InferNpgsqlDbType(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        if (t == typeof(int)) return NpgsqlDbType.Integer;
        if (t == typeof(long)) return NpgsqlDbType.Bigint;
        if (t == typeof(short)) return NpgsqlDbType.Smallint;
        if (t == typeof(bool)) return NpgsqlDbType.Boolean;
        if (t == typeof(decimal)) return NpgsqlDbType.Numeric;
        if (t == typeof(double)) return NpgsqlDbType.Double;
        if (t == typeof(float)) return NpgsqlDbType.Real;
        if (t == typeof(string)) return NpgsqlDbType.Text;
        if (t == typeof(Guid)) return NpgsqlDbType.Uuid;
        if (t == typeof(DateTime)) return NpgsqlDbType.Timestamp;
        if (t == typeof(DateTimeOffset)) return NpgsqlDbType.TimestampTz;
        if (t == typeof(byte[])) return NpgsqlDbType.Bytea;
        if (t == typeof(TimeSpan)) return NpgsqlDbType.Interval;
        throw new NotSupportedException($"Type '{t.FullName}' is not supported in PostgreSQL bulk insert.");
    }

    public void MarkModified(T entity)
    {
        var entry = Ctx.Entry(entity);
        if (entry.State == EntityState.Detached)
            DbSet.Attach(entity);
        entry.State = EntityState.Modified;
    }

    private string Quote(string name) => DbType switch
    {
        DatabaseType.SqlServer => $"[{name}]",
        DatabaseType.PostgreSql => $"\"{name}\"",
        DatabaseType.MySql => $"`{name}`",
        _ => name
    };
}

public class EfLongIdRepo<T> : EfRepo<T>, IAsyncLongIdRepo<T> where T : class
{
    public EfLongIdRepo(DbContext ctx, DatabaseType dbType) : base(ctx, dbType) { }
    public Task<T?> ByIdNoTrackAsync(long id) => ByKeyNoTrackAsync(id);
    public Task<T?> ByIdAsync(long id) => ByKeyAsync(id);

}

// Read-only long-Id repo
public class EfLongIdReadRepo<T> : EfRepo<T>, IAsyncLongIdReadRepo<T> where T : class
{
    public EfLongIdReadRepo(DbContext ctx, DatabaseType dbType) : base(ctx, dbType) { }
    public Task<T?> ByIdNoTrackAsync(long id) => ByKeyNoTrackAsync(id);
}

public class ApiQueryResult<T> where T : class
{
    public IList<T> Results { get; set; } = new List<T>();
    public long? InlineCount { get; set; } = 0;
    public int PageNo { get; set; } = 0;
    public int ErrorNo { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;

    public ApiQueryResult() { }

    public ApiQueryResult(Exception e) => FillException(e);

    private static string ExceptRecurse(Exception e)
    {
        var details = e.Message + " \r\n";
        if (e.InnerException != null)
            details += "Inner exception: " + ExceptRecurse(e.InnerException);
        return details;
    }

    public void FillException(Exception e)
    {
        ErrorNo = 1;
        ErrorMessage = e.Message;
        ErrorDetails = ExceptRecurse(e);
    }

    public void ThrowIfError(string context)
    {
        if (ErrorNo != 0)
            throw new InvalidOperationException($"{context}: {ErrorMessage}\n{ErrorDetails}");
    }
}
