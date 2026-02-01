// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// Includes: EDM metadata support, scalar query helpers, async-only design, tracked vs. non-tracked distinction, OData integration, bulk insert/delete helpers

using EfCore.Boost.DbRepo;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.OData.Edm;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.ModelBuilder;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;

namespace EfCore.Boost;

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
/// Read-only repository interface supporting async operations and OData.
/// This provides non-tracking queries for efficiency and safety in readonly contexts.
public interface IReadRepo<T> where T : class
{
    /// <summary>
    /// Returns an IQueryable for LINQ composition, configured as no-tracking by default.
    /// </summary>
    /// <remarks>
    /// Use this as the starting point for read-only queries. Tracking should be avoided for large reads.
    /// </remarks>
    public IQueryable<T> QueryNoTrack();

    /// <summary>
    /// Finds an entity by primary key (tracking query).
    /// </summary>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    Task<T?> ByKeyAsync(params object[] key);

    /// <summary>
    /// Finds an entity by primary key (tracking query).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    Task<T?> ByKeyAsync(CancellationToken ct, params object[] key);

    /// <summary>
    /// Finds an entity by primary key using a no-tracking query.
    /// </summary>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    Task<T?> ByKeyNoTrackAsync(params object[] key);

    /// <summary>
    /// Finds an entity by primary key using a no-tracking query.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    Task<T?> ByKeyNoTrackAsync(CancellationToken ct, params object[] key);

    /// <summary>
    /// Synchronous version of ByKeyAsync (tracking query).
    /// </summary>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    T? ByKeySynchronized(params object[] key);

    /// <summary>
    /// Synchronous version of ByKeyNoTrackAsync (no-tracking query).
    /// </summary>
    /// <param name="key">Primary key values in the correct order.</param>
    /// <returns>The entity if found; otherwise null.</returns>
    T? ByKeyNoTrackSynchronized(params object[] key);

    /// <summary>
    /// Returns the first entity matching a filter, without tracking.
    /// </summary>
    /// <param name="filter">Predicate applied to the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The first matching entity; otherwise null.</returns>
    Task<T?> FirstNoTrackAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of FirstNoTrackAsync.
    /// </summary>
    /// <param name="filter">Predicate applied to the query.</param>
    /// <returns>The first matching entity; otherwise null.</returns>
    T? FirstNoTrackSynchronized(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Executes a no-tracking query and returns a list.
    /// </summary>
    /// <param name="filter">Optional predicate. If null, returns all rows within the base scope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Materialized list of entities.</returns>
    Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of QueryNoTrackAsync.
    /// </summary>
    /// <param name="filter">Optional predicate. If null, returns all rows within the base scope.</param>
    /// <returns>Materialized list of entities.</returns>
    List<T> QueryNoTrackSynchronized(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Streams entities using async iteration (no tracking).
    /// </summary>
    /// <remarks>
    /// Useful for large result sets. Enumeration executes the query.
    /// </remarks>
    /// <param name="filter">Optional predicate.</param>
    /// <returns>Async stream of entities.</returns>
    IAsyncEnumerable<T> StreamNoTrackAsync(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Streams entities synchronously (no tracking).
    /// </summary>
    /// <remarks>
    /// Enumeration executes the query.
    /// </remarks>
    /// <param name="filter">Optional predicate.</param>
    /// <returns>Enumerable stream of entities.</returns>
    IEnumerable<T> StreamNoTrackSynchronized(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Returns true if any entity matches the filter (no tracking).
    /// </summary>
    /// <param name="filter">Predicate applied to the query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if at least one match exists; otherwise false.</returns>
    Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of AnyNoTrackAsync.
    /// </summary>
    /// <param name="filter">Predicate applied to the query.</param>
    /// <returns>True if at least one match exists; otherwise false.</returns>
    bool AnyNoTrackSynchronized(Expression<Func<T, bool>> filter);

    /// <summary>
    /// Counts rows matching an optional filter.
    /// </summary>
    /// <param name="filter">Optional predicate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Row count.</returns>
    Task<long> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of CountAsync.
    /// </summary>
    /// <param name="filter">Optional predicate.</param>
    /// <returns>Row count.</returns>
    long CountSynchronized(Expression<Func<T, bool>>? filter = null);

    /// <summary>
    /// Executes a scalar query and returns a nullable boolean.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<bool?> GetBoolScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable boolean.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<bool?> GetBoolScalarAsync(string query, CancellationToken ct, params object[] parameters);

    /// <summary>
    /// Synchronous version of GetBoolScalarAsync.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    bool? GetBoolScalarSynchronized(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable Int64.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<long?> GetLongScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable Int64.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<long?> GetLongScalarAsync(string query, CancellationToken ct, params object[] parameters);

    /// <summary>
    /// Synchronous version of GetLongScalarAsync.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    long? GetLongScalarSynchronized(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable decimal.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable decimal.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<decimal?> GetDecimalScalarAsync(string query, CancellationToken ct, params object[] parameters);

    /// <summary>
    /// Synchronous version of GetDecimalScalarAsync.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    decimal? GetDecimalScalarSynchronized(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable string.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<string?> GetStringScalarAsync(string query, params object[] parameters);

    /// <summary>
    /// Executes a scalar query and returns a nullable string.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    Task<string?> GetStringScalarAsync(string query, CancellationToken ct, params object[] parameters);

    /// <summary>
    /// Synchronous version of GetStringScalarAsync.
    /// </summary>
    /// <remarks>
    /// The SQL passed to this method is executed verbatim against the underlying database provider and may not be portable across different database engines.
    /// For cross-database compatibility, prefer using mapped routines (views, functions, or procedures) exposed through the repository instead of raw SQL.
    /// </remarks>
    /// <param name="query">SQL query expected to return a single scalar value.</param>
    /// <param name="parameters">Provider parameters (implementation-defined).</param>
    /// <returns>Scalar value or null.</returns>
    string? GetStringScalarSynchronized(string query, params object[] parameters);

    /// <summary>
    /// Returns metadata for the entity type, used by EDM/OData metadata exchange.
    /// </summary>
    /// <returns>Entity metadata string.</returns>
    string EntityMeta();

    /// <summary>
    /// Builds an OData query plan for filtering, ordering, and paging.
    /// </summary>
    /// <remarks>
    /// No database queries are executed here. The returned plan contains separate queries for items and (optional) count.
    /// Disallowed options are ignored and recorded in plan.Report.
    /// </remarks>
    /// <param name="baseQuery">Base query defining security and scope boundaries.</param>
    /// <param name="options">OData options parsed from the HTTP request.</param>
    /// <param name="policy">Feature permissions and limits. If null, defaults are used.</param>
    /// <param name="forceCount">Forces inline count even when $count is not requested.</param>
    /// <returns>An ODataQueryPlan for the repository entity type.</returns>
    ODataQueryPlan<T> BuildODataQueryPlan(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = false);

    /// <summary>
    /// Applies OData $expand as Entity Framework Include paths on the plan's ItemsQuery.
    /// </summary>
    /// <remarks>
    /// Only navigation paths are applied. Any nested $expand options ($filter/$orderby/$top/$select) are ignored.
    /// Disallowed expands are ignored and recorded in plan.Report.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <returns>The same plan instance with Includes applied to ItemsQuery.</returns>
    ODataQueryPlan<T> ApplyODataExpandAsInclude(ODataQueryPlan<T> plan);

    /// <summary>
    /// Executes a non-shaped OData plan and materializes typed entities.
    /// </summary>
    /// <remarks>
    /// Throws if the plan is shaped ($select/$expand projection). Use the shaped materializer for projected results.
    /// InlineCount is computed from CountQuery only when CountRequested is true.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>QueryResult with typed entities and optional InlineCount.</returns>
    Task<QueryResult<T>> MaterializeODataAsync(ODataQueryPlan<T> plan, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of MaterializeODataAsync.
    /// </summary>
    /// <remarks>
    /// Throws if the plan is shaped ($select/$expand projection).
    /// InlineCount is computed from CountQuery only when CountRequested is true.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <returns>QueryResult with typed entities and optional InlineCount.</returns>
    QueryResult<T> MaterializeODataSynchronized(ODataQueryPlan<T> plan);

    /// <summary>
    /// Convenience method: BuildODataQueryPlan + MaterializeODataAsync (typed path).
    /// </summary>
    /// <remarks>
    /// $select and $expand shaping are not applied by this helper (they are effectively ignored).
    /// </remarks>
    /// <param name="baseQuery">Base query defining security and scope boundaries.</param>
    /// <param name="options">OData options parsed from the HTTP request.</param>
    /// <param name="policy">Feature permissions and limits. If null, defaults are used.</param>
    /// <param name="forceCount">Forces inline count even when $count is not requested.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>QueryResult with typed entities and optional InlineCount.</returns>
    Task<QueryResult<T>> FilterODataAsync(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = false, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of FilterODataAsync.
    /// </summary>
    /// <remarks>
    /// $select and $expand shaping are not applied by this helper (they are effectively ignored).
    /// </remarks>
    /// <param name="baseQuery">Base query defining security and scope boundaries.</param>
    /// <param name="options">OData options parsed from the HTTP request.</param>
    /// <param name="policy">Feature permissions and limits. If null, defaults are used.</param>
    /// <param name="forceCount">Forces inline count even when $count is not requested.</param>
    /// <returns>QueryResult with typed entities and optional InlineCount.</returns>
    QueryResult<T> FilterODataSynchronized(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = false);

    /// <summary>
    /// Applies OData $select/$expand shaping (projection) to the plan's ItemsQuery.
    /// </summary>
    /// <remarks>
    /// Shaping changes the result type from entities to OData projection wrapper objects.
    /// The returned IQueryable must be materialized using the shaped materializer.
    /// Disallowed select/expand requests are ignored and recorded in plan.Report.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <param name="settings">Optional OData settings for ApplyTo. If null, defaults are used.</param>
    /// <returns>An untyped IQueryable representing the shaped projection.</returns>
    IQueryable ApplyODataSelectExpand(ODataQueryPlan<T> plan, ODataQuerySettings? settings = null);

    /// <summary>
    /// Executes a shaped OData query and materializes OData projection objects.
    /// </summary>
    /// <remarks>
    /// shapedQuery should be created by ApplyODataSelectExpand. InlineCount is computed from plan.CountQuery
    /// only when plan.CountRequested is true.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <param name="shapedQuery">The shaped query returned by ApplyODataSelectExpand.</param>
    /// <param name="ct">Cancellation token for database operations.</param>
    /// <returns>QueryResult with shaped objects and optional InlineCount.</returns>
    Task<QueryResult<object>> MaterializeODataShapedAsync(ODataQueryPlan<T> plan, IQueryable shapedQuery, CancellationToken ct = default);

    /// <summary>
    /// Synchronous version of MaterializeODataShapedAsync.
    /// </summary>
    /// <remarks>
    /// shapedQuery should be created by ApplyODataSelectExpand. InlineCount is computed from plan.CountQuery
    /// only when plan.CountRequested is true.
    /// </remarks>
    /// <param name="plan">A plan produced by BuildODataQueryPlan.</param>
    /// <param name="shapedQuery">The shaped query returned by ApplyODataSelectExpand.</param>
    /// <returns>QueryResult with shaped objects and optional InlineCount.</returns>
    QueryResult<object> MaterializeODataShapedSynchronized(ODataQueryPlan<T> plan, IQueryable shapedQuery);

}

public interface IRepo<T> : IReadRepo<T> where T : class
{
    /// <summary>
    /// Query with tracked entities
    /// </summary>
    /// <returns></returns>
    public IQueryable<T> Query();
    /// <summary>
    /// Tracked first row
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<T?> FirstAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default);
    /// <summary>
    /// Syncronized version of First
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    T? FirstSynchronized(Expression<Func<T, bool>> filter);
    /// <summary>
    /// Delete rows by filter expression (predicate)
    /// </summary>
    /// <param name="predicate"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    /// <summary>
    /// Synchronized version of DeleteWhere
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    int DeleteWhereSynchronized(Expression<Func<T, bool>> predicate);
    /// <summary>
    /// Add row to the dataset
    /// </summary>
    /// <param name="entity"></param>
    void Add(T entity);
    /// <summary>
    /// Add row to the dataset without tracking
    /// </summary>
    /// <param name="entity"></param>
    void AddBrute(T entity);
    /// <summary>
    /// Add multiple rows to the dataset
    /// </summary>
    /// <param name="entities"></param>
    void AddRange(IEnumerable<T> entities);
    /// <summary>
    /// Add multiple rows to the dataset without tracking
    /// Fastest way to do inserts without turning to bulk actions
    /// </summary>
    /// <param name="entities"></param>
    void AddRangeBrute(IEnumerable<T> entities);
    /// <summary>
    /// Delete row from dataset
    /// </summary>
    /// <param name="entity"></param>
    void Delete(T entity);
    /// <summary>
    /// Delete row from dataset without provoking tracking
    /// </summary>
    /// <param name="entity"></param>
    void DeleteBrute(T entity);
    /// <summary>
    /// Remove list of rows from dataset
    /// </summary>
    /// <param name="entities"></param>
    void DeleteMany(IEnumerable<T> entities);
    /// <summary>
    /// Remove list of rows from dataset without tracking
    /// </summary>
    /// <param name="entities"></param>
    void DeleteManyBrute(IEnumerable<T> entities);

    /// <summary>
    /// Bulk deleting from database by primary key values.
    /// No checks and very fast
    /// Uses transaction block
    /// No effects on current dataset
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default);
    /// <summary>
    /// Syncronized version of BulkDeleteById
    /// </summary>
    /// <param name="ids"></param>
    void BulkDeleteByIdsSynchronized(IEnumerable<long> ids);
    /// <summary>
    /// Bulk insert action, very fast for MsSql and Postgres
    /// No checks performed
    /// Uses transaction (Dbs current if exists, otherwise a new ambient one)
    /// Allows for inserting identity values and maintaining the sequence
    /// Transation is committed if no transaction is passed in
    /// </summary>
    /// <param name="items">Rows to be inserted (preferably outside of the dbset scope)</param>
    /// <param name="includeIdentityValues">If we want to insert identity values</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken ct = default);
    /// <summary>
    /// Syncronized version of BulkInsert
    /// </summary>
    /// <param name="items"></param>
    /// <param name="includeIdentityValues"></param>
    void BulkInsertSynchronized(List<T> items, bool includeIdentityValues = false);
    /// <summary>
    /// Marks only this entity as Modified; no graph traversal, no DetectChanges.
    /// All columns will be updated to db
    /// </summary>
    /// <param name="entity"></param>
    void MarkModified(T entity);
    /// <summary>
    /// Lets EF walk the graph and mark all reachable entities as Modified/Added.
    /// Triggers DetectChanges when enabled.
    /// All columns will be updated to db
    /// </summary>
    /// <param name="entities"></param>
    void UpdateAllCols(T entities);

    //Odata methods ... todo


}

public interface ILongIdRepo<T> : IRepo<T> where T : class
{
    Task<T?> ByIdNoTrackAsync(long id);
    T? ByIdNoTrackSynchronized(long id);
    Task<T?> ByIdAsync(long id);
    T? ByIdSynchronized(long id);
}

public interface ILongIdReadRepo<T> : IReadRepo<T> where T : class
{
    Task<T?> ByIdNoTrackAsync(long id);
    T? ByIdNoTrackSynchronized(long id);
}

public partial class EfReadRepo<T>(DbContext dbContext, DatabaseType dbType = DatabaseType.SqlServer) : IReadRepo<T> where T : class
{
    protected readonly DbSet<T> DbSet = dbContext.Set<T>();
    protected readonly DbContext Ctx = dbContext;
    protected readonly DatabaseType DbType = dbType;

    public IQueryable<T> QueryNoTrack() => DbSet.AsNoTracking();

    public string EntityMeta()
    {
        var builder = new ODataConventionModelBuilder();
        builder.EntitySet<T>(typeof(T).Name);
        var edmModel = builder.GetEdmModel();
        return EdmToXml(edmModel);
    }

    #region key-lookups

    public Task<T?> ByKeyNoTrackAsync(params object[] key) => ByKeyNoTrackAsync(CancellationToken.None, key);
    public Task<T?> ByKeyNoTrackAsync(CancellationToken ct, params object[] key) => DbSet.AsNoTracking().FirstOrDefaultAsync(BuildPkPredicate(key), ct);

    public T? ByKeyNoTrackSynchronized(params object[] key) => DbSet.AsNoTracking().FirstOrDefault(BuildPkPredicate(key));

    public Task<T?> ByKeyAsync(params object[] key) => ByKeyAsync(CancellationToken.None, key);
    public Task<T?> ByKeyAsync(CancellationToken ct, params object[] key) => DbSet.FindAsync(key, ct).AsTask();

    public T? ByKeySynchronized(params object[] key) => DbSet.Find(key);

    /// <summary>
    /// Multi-use build query from primary key values mapped to parameters
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    protected Expression<Func<T, bool>> BuildPkPredicate(params object[] key)
    {
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"Entity {typeof(T).Name} not found in model.");
        var pk = et.FindPrimaryKey()?.Properties ?? throw new InvalidOperationException($"Entity {typeof(T).Name} has no primary key.");
        if (pk.Count != key.Length) throw new ArgumentException("Key count mismatch.", nameof(key));
        var p = Expression.Parameter(typeof(T), "e");
        Expression? body = null;
        for (int i = 0; i < pk.Count; i++)
        {
            var prop = pk[i];
            var clr = prop.ClrType;
            var keyVal = key[i];

            if (keyVal == null && clr.IsValueType && Nullable.GetUnderlyingType(clr) == null)
                throw new ArgumentException($"Null key value for non-nullable key '{prop.Name}'.", nameof(key));
            var left = Expression.Call(typeof(EF), nameof(EF.Property), new[] { clr }, p, Expression.Constant(prop.Name));
            ConstantExpression right;
            try
            {
                var converted = ConvertKeyValue(keyVal, clr);
                right = Expression.Constant(converted, clr);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Key value '{keyVal}' ({ (keyVal == null ? "Null" : keyVal.GetType().Name)}) cannot be converted to '{clr.Name}' for key '{prop.Name}'.",
                    nameof(key),
                    ex);
            }
            var eq = Expression.Equal(left, right);
            body = body == null ? eq : Expression.AndAlso(body, eq);
        }
        return Expression.Lambda<Func<T, bool>>(body!, p);
    }

    private static object? ConvertKeyValue(object? value, Type targetType)
    {
        if (value == null) return null;

        var nonNullType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var valueType = value.GetType();

        if (nonNullType.IsAssignableFrom(valueType))
            return value;

        if (nonNullType.IsEnum)
            return Enum.ToObject(nonNullType, value);

        return Convert.ChangeType(value, nonNullType, System.Globalization.CultureInfo.InvariantCulture);
    }

    #endregion

    #region queries

    public async Task<T?> FirstNoTrackAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default) => await DbSet.AsNoTracking().FirstOrDefaultAsync(filter, ct);

    public T? FirstNoTrackSynchronized(Expression<Func<T, bool>> filter) =>  DbSet.AsNoTracking().FirstOrDefault(filter);

    public async Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return await query.ToListAsync(ct);
    }

    public List<T> QueryNoTrackSynchronized(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return query.ToList();
    }

    public IAsyncEnumerable<T> StreamNoTrackAsync(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return query.AsAsyncEnumerable();
    }

    public IEnumerable<T> StreamNoTrackSynchronized(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return query.AsEnumerable();
    }

    public async Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default) =>  await DbSet.AsNoTracking().AnyAsync(filter,ct);

    public bool AnyNoTrackSynchronized(Expression<Func<T, bool>> filter) =>  DbSet.AsNoTracking().Any(filter);

    public async Task<long> CountAsync(Expression<Func<T, bool>>? filter = null, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return await query.LongCountAsync(ct);
    }

    public long CountSynchronized(Expression<Func<T, bool>>? filter = null)
    {
        var query = DbSet.AsNoTracking().AsQueryable();
        if (filter != null)
            query = query.Where(filter);
        return query.LongCount();
    }

    #endregion

    #region scalar readers
    public Task<bool?> GetBoolScalarAsync(string query, params object[] parameters) => GetBoolScalarAsync(query, CancellationToken.None, parameters);
    public async Task<bool?> GetBoolScalarAsync(string query, CancellationToken ct, params object[] parameters)
    {
        using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = await oc.Cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value)
            return null;
        return Convert.ToBoolean(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool? GetBoolScalarSynchronized(string query, params object[] parameters)
    {
        using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = oc.Cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToBoolean(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public Task<long?> GetLongScalarAsync(string query, params object[] parameters) => GetLongScalarAsync(query, CancellationToken.None, parameters);

    public async Task<long?> GetLongScalarAsync(string query, CancellationToken ct, params object[] parameters)
    {
        using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = await oc.Cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToInt64(result);
    }

    public long? GetLongScalarSynchronized(string query, params object[] parameters)
    {
        using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = oc.Cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToInt64(result);
    }

    public Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters) => GetDecimalScalarAsync(query, CancellationToken.None, parameters);
    public async Task<decimal?> GetDecimalScalarAsync(string query, CancellationToken ct, params object[] parameters)
    {
        using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = await oc.Cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToDecimal(result);
    }

    public decimal? GetDecimalScalarSynchronized(string query, params object[] parameters)
    {
        using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters) ;
        var result = oc.Cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToDecimal(result);
    }

    public Task<string?> GetStringScalarAsync(string query, params object[] parameters) => GetStringScalarAsync(query, CancellationToken.None, parameters);
    public async Task<string?> GetStringScalarAsync(string query, CancellationToken ct, params object[] parameters)
    {
        using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
        oc.Cmd.CommandText = query;
        AddParameters(oc.Cmd, parameters);
        var result = await oc.Cmd.ExecuteScalarAsync(ct);
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    public string? GetStringScalarSynchronized(string query, params object[] parameters)
    {
        using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
        oc.Cmd.CommandText = query;
        this.AddParameters(oc.Cmd, parameters);
        var result = oc.Cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value) return null;
        return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }
    #endregion

    #region helpers
    private static string EdmToXml(IEdmModel model)
    {
        using var stringWriter = new StringWriter();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = true });
        CsdlWriter.TryWriteCsdl(model, xmlWriter, CsdlTarget.OData, out _);
        xmlWriter.Flush();
        return stringWriter.ToString();
    }

    /// <summary>
    /// Adds provided parameters to a command, validating that they are proper DbParameter
    /// instances created by the provider.
    /// </summary>
    protected void AddParameters(DbCommand cmd, params object[] parameters)
    {
        if (parameters == null || parameters.Length == 0)
            return;
        foreach (var param in parameters)
        {
            if (param == null)
                continue;
            if (param is DbParameter dbParam)
                cmd.Parameters.Add(dbParam);
            else
                throw new ArgumentException($"Only {nameof(DbParameter)} instances are supported. Invalid parameter type: {param.GetType().Name}", nameof(parameters));
        }
    }
    #endregion
}

public partial class EfRepo<T>(DbContext dbContext, DatabaseType dbType) : EfReadRepo<T>(dbContext, dbType), IRepo<T> where T : class
{
    public IQueryable<T> Query() => DbSet;
    //public new IQueryable<T> QueryNoTrack() => DbSet.AsNoTracking();

    public Task<T?> FirstAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default) => DbSet.FirstOrDefaultAsync(filter, ct);

    public T? FirstSynchronized(Expression<Func<T, bool>> filter) => DbSet.FirstOrDefault(filter);

    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) => DbSet.Where(predicate).ExecuteDeleteAsync(ct);

    public int DeleteWhereSynchronized(Expression<Func<T, bool>> predicate) => DbSet.Where(predicate).ExecuteDelete();

    public void Add(T entity) => DbSet.Add(entity);

    /// <summary>
    /// Adds an entity with automatic change detection temporarily disabled
    /// to reduce overhead during bulk inserts
    /// </summary>
    /// <param name="entity"></param>
    public void AddBrute(T entity)
    {
        var prev = Ctx.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            DbSet.Add(entity);
        }
        finally
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }

    public void AddRange(IEnumerable<T> entities) => DbSet.AddRange(entities);

    /// <summary>
    /// Adds an entity with automatic change detection temporarily disabled
    /// to reduce overhead during bulk inserts.
    /// </summary>
    /// <param name="entities"></param>
    public void AddRangeBrute(IEnumerable<T> entities)
    {
        var prev = Ctx.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            DbSet.AddRange(entities);
        }
        finally
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }

    public void Delete(T entity)
    {
        DbSet.Remove(entity);
    }

    /// <summary>
    /// Speed up deletes by disabling autodetect changes while removing the entity
    /// </summary>
    /// <param name="entity"></param>
    public void DeleteBrute(T entity)
    {
        var prev = Ctx.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            var entry = Ctx.Entry(entity);
            if (entry.State == EntityState.Detached)
                DbSet.Attach(entity); //Attach does not inflict DetectChanges
            entry.State = EntityState.Deleted;
        }
        finally
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }

    public void DeleteMany(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
            Delete(entity);
    }

    /// <summary>
    /// Spead up bulk-delete by delaying Tracking checks
    /// </summary>
    /// <param name="entities"></param>
    public void DeleteManyBrute(IEnumerable<T> entities)
    {
        var prev = Ctx.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = false;
            foreach (var entity in entities)
            {
                var entry = Ctx.Entry(entity);
                if (entry.State == EntityState.Detached)
                    DbSet.Attach(entity);
                entry.State = EntityState.Deleted;
            }
        }
        finally
        {
            Ctx.ChangeTracker.AutoDetectChangesEnabled = prev;
        }
    }

    #region bulk-deletes

    /// <summary>
    /// Asynchronously deletes multiple records by primary key values from the database table
    /// associated with <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Bulk behavior:
    /// - Deletes are issued directly as raw SQL without involving the EF change tracker.
    /// - Records are deleted in batches to prevent oversized SQL commands and optimize performance.
    /// - If <paramref name="ids"/> contains no values, the method returns immediately without executing any SQL.
    /// - Important: This method uses ambient transaction if present, else creates its own + commits.
    ///
    /// Primary key requirements:
    /// - The entity type <typeparamref name="T"/> must have a single-column primary key.
    /// - The key must be of type <c>int</c>, <c>int?</c>, <c>long</c>, or <c>long?</c>.
    /// - The provided <paramref name="ids"/> are converted to the correct PK type automatically.
    /// - If the PK does not meet these requirements, a <see cref="NotSupportedException"/> is thrown.
    ///
    /// Transaction behavior:
    /// - If the DbContext already has an active transaction, that transaction is used.
    /// - If not, a new internal transaction is created and committed upon successful completion,
    ///   ensuring all-or-nothing deletion across all batches.
    ///
    /// No call to uow.SaveChanges is required, and the entity tracking state remains unaffected.
    /// </remarks>
    /// <param name="ids">Primary key values identifying the records to delete. Must not be null.</param>
    /// <param name="ct">Optional token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous bulk delete operation.</returns>
    public async Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default)
    {
        var ambient = Ctx.Database.CurrentTransaction?.GetDbTransaction();
        if (ambient != null)
        {
            await BulkDeleteByIdsAsync(ids, ambient, ct);
            return;
        }
        // Is required if dbcontext has not been saved/committed yet
        var strat = Ctx.Database.CreateExecutionStrategy();
        await strat.ExecuteAsync(async () =>
        {
            await Ctx.Database.OpenConnectionAsync(ct); //Make sure connection is open before starting transaction
            using var efTx = await Ctx.Database.BeginTransactionAsync(ct);
            try
            {
                await BulkDeleteByIdsAsync(ids, efTx.GetDbTransaction(), ct);
                await efTx.CommitAsync(ct);
            }
            catch(Exception)
            {
                try {
                    await efTx.RollbackAsync(ct);
                }
                catch { }
                throw;
            }
        });
    }

    /// <summary>
    /// Synchronized version of BulkDeleteByIdsAsync
    /// </summary>
    /// <param name="ids"></param>
    public void BulkDeleteByIdsSynchronized(IEnumerable<long> ids)
    {
        var ambient = Ctx.Database.CurrentTransaction?.GetDbTransaction();
        if (ambient != null)
        {
            BulkDeleteByIdsCoreSynchronized(ids, ambient);
            return;
        }
        // Is required if dbcontext has not been saved/committed yet
        var strat = Ctx.Database.CreateExecutionStrategy();
        strat.Execute(() =>
        {
            Ctx.Database.OpenConnection(); //Make sure connection is open before starting transaction
            using var efTx = Ctx.Database.BeginTransaction();
            try
            {
                BulkDeleteByIdsCoreSynchronized(ids, efTx.GetDbTransaction());
                efTx.Commit();
            }
            catch
            {
                try { efTx.Rollback(); } catch { }
                throw;
            }
        });
    }

    protected async Task BulkDeleteByIdsAsync(IEnumerable<long> ids, DbTransaction trans, CancellationToken ct = default)
    {
        var idArray = ids as long[] ?? ids.ToArray();
        if (idArray.Length == 0)
            return;
        var fullTableName = this.CalcSqlTableName();
        var (pkColumnName, keyClrType) = this.FindTabPrimIdCol();
        var quotedIdColumn = Quote(pkColumnName);
        using var oc = await CmdHelper.OpenCmdAsync(Ctx, ct);
        oc.Cmd.Transaction = trans;
        const int batchSize = 1000;
        for (int i = 0; i < idArray.Length; i += batchSize)
        {
            oc.Cmd.Parameters.Clear();
            var sb = new System.Text.StringBuilder();
            sb.Append($"DELETE FROM {fullTableName} WHERE {quotedIdColumn} IN (");
            int batchEnd = Math.Min(i + batchSize, idArray.Length);
            int localIndex = 0;
            for (int j = i; j < batchEnd; j++)
            {
                if (localIndex > 0) sb.Append(", ");
                var paramName = $"@p{localIndex++}";
                sb.Append(paramName);
                var p = oc.Cmd.CreateParameter();
                p.ParameterName = paramName;
                object value = keyClrType == typeof(long)
                ? idArray[j] : Convert.ChangeType(idArray[j], keyClrType, System.Globalization.CultureInfo.InvariantCulture);
                p.Value = value ?? DBNull.Value;
                oc.Cmd.Parameters.Add(p);
            }
            sb.Append(")");
            oc.Cmd.CommandText = sb.ToString();
            await oc.Cmd.ExecuteNonQueryAsync(ct);
        }
    }

    protected void BulkDeleteByIdsCoreSynchronized(IEnumerable<long> ids, DbTransaction trans)
    {
        // Materialize once, and bail early if nothing to do
        var idArray = ids as long[] ?? ids.ToArray();
        if (idArray.Length == 0)
            return;
        var fullTableName = this.CalcSqlTableName();
        var (pkColumnName, keyClrType) = this.FindTabPrimIdCol();
        var quotedIdColumn = Quote(pkColumnName);
        using var oc = CmdHelper.OpenCmdSyncronized(Ctx);
        oc.Cmd.Transaction = trans;
        const int batchSize = 1000;
        for (int i = 0; i < idArray.Length; i += batchSize)
        {
            oc.Cmd.Parameters.Clear();
            var sb = new System.Text.StringBuilder();
            sb.Append($"DELETE FROM {fullTableName} WHERE {quotedIdColumn} IN (");
            int batchEnd = Math.Min(i + batchSize, idArray.Length);
            int localIndex = 0;
            for (int j = i; j < batchEnd; j++)
            {
                if (localIndex > 0) sb.Append(", ");
                var paramName = $"@p{localIndex++}";
                sb.Append(paramName);
                var p = oc.Cmd.CreateParameter();
                p.ParameterName = paramName;
                object value = keyClrType == typeof(long)
                 ? idArray[j] : Convert.ChangeType(idArray[j], keyClrType, System.Globalization.CultureInfo.InvariantCulture);
                p.Value = value ?? DBNull.Value;
                oc.Cmd.Parameters.Add(p);
            }
            sb.Append(")");
            oc.Cmd.CommandText = sb.ToString();
            oc.Cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Search for single integer valued primary key, throws exeption if cnditions are not matched
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    /// <exception cref="NotSupportedException"></exception>
    protected (string ColumnName, Type KeyClrType) FindTabPrimIdCol()
    {
        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} is not part of the current DbContext model.");
        var key = entityType.FindPrimaryKey() ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} does not have a primary key defined.");
        if (key.Properties.Count != 1)
            throw new NotSupportedException($"Bulk operations require a single-column primary key. " + $"{typeof(T).Name} has {key.Properties.Count} key properties.");
        var pkProperty = key.Properties[0];
        var clrType = Nullable.GetUnderlyingType(pkProperty.ClrType) ?? pkProperty.ClrType;
        if (clrType != typeof(long) && clrType != typeof(int))
            throw new NotSupportedException($"Bulk operations currently only support int/long primary keys. " + $"{typeof(T).Name}.{pkProperty.Name} is of type {pkProperty.ClrType.Name}.");
        var tableName = entityType.GetTableName() ??
            throw new InvalidOperationException($"Table name not resolved for {typeof(T).Name}.");
        var schema = entityType.GetSchema();
        var tableId = StoreObjectIdentifier.Table(tableName, schema);
        var columnName = pkProperty.GetColumnName(tableId) ?? pkProperty.GetColumnName() ?? pkProperty.Name;
        return (columnName, clrType);
    }

    #endregion

    #region bulk-inserts

    /// <summary>
    /// Synchronized version of BulkInsertAsync
    /// </summary>
    /// <param name="items"></param>
    /// <param name="includeIdentityValues"></param>
    public void BulkInsertSynchronized(List<T> items, bool includeIdentityValues = false)
    {
        var ambient = Ctx.Database.CurrentTransaction?.GetDbTransaction();
        if (ambient != null)
        {
            BulkInsertCoreSynchronized(items, ambient, includeIdentityValues);
            return;
        }
        // Is required if dbcontext has not been saved/committed yet
        var strat = Ctx.Database.CreateExecutionStrategy();
        strat.Execute(() =>
        {
            Ctx.Database.OpenConnection();
            using var efTx = Ctx.Database.BeginTransaction();
            try
            {
                BulkInsertCoreSynchronized(items, efTx.GetDbTransaction(), includeIdentityValues);
                efTx.Commit();
            }
            catch
            {
                try { efTx.Rollback(); } catch { }
                throw;
            }
        });
    }

    protected void BulkInsertCoreSynchronized(List<T> items, DbTransaction trans, bool includeIdentityValues = false)
    {
        if (this.DbType == DatabaseType.SqlServer)
            this.MSBulkInsertSynchronized(items, trans, includeIdentityValues);
        else if (DbType == DatabaseType.PostgreSql)
            this.PgBulkInsertSynchronized(items, trans, includeIdentityValues);
        else //Assume mysql
            this.MyBulkInsertSynchronized(items, trans, includeIdentityValues);
    }

    /// <summary>
    /// Performs a bulk insert of the provided items using the most efficient method
    /// supported by the current database provider:
    /// - PostgreSQL: COPY (binary import)
    /// - SQL Server: SqlBulkCopy
    /// - MySQL: batched parameterized INSERT statements
    ///
    /// Transaction behavior:
    /// - If the DbContext already has an active transaction, that will be used.
    /// - If not, an internal transaction is created to ensure all-or-nothing bulk behavior.
    ///
    /// Additional behavior:
    /// - Supports explicitly inserting identity values (for a single identity column).
    ///   When enabled, identity reseeding will be handled automatically afterwards (where applicable).
    /// - Entities are not tracked by the DbContext during this operation.
    ///   No call to uow.SaveChanges is required unless the caller manages its own transaction.
    ///
    /// This operation does not modify or rely on the DbSet change tracker, and does not
    /// commit any unrelated pending EF changes.
    /// </summary>
    /// <param name="items">The entity instances to insert in bulk.</param>
    /// <param name="includeIdentityValues">
    /// If <c>true</c>, identity column values from the provided entities are preserved.
    /// Otherwise, identity values are generated by the database.
    /// </param>
    /// <param name="ct">A token for cancelling the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous bulk insert.</returns>
    public async Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken ct = default)
    {
        var ambient = Ctx.Database.CurrentTransaction?.GetDbTransaction();
        if (ambient != null)
        {
            await BulkInsertCoreAsync(items, ambient, includeIdentityValues, ct);
            return;
        }
        // Is required if dbcontext has not been saved/committed yet
        var strat = Ctx.Database.CreateExecutionStrategy();
        await strat.ExecuteAsync(async () =>
        {
            await Ctx.Database.OpenConnectionAsync(ct);
            await using var efTx = await Ctx.Database.BeginTransactionAsync(ct);
            try
            {
                await BulkInsertCoreAsync(items, efTx.GetDbTransaction(), includeIdentityValues, ct);
                await efTx.CommitAsync(ct);
            }
            catch
            {
                try {
                    await efTx.RollbackAsync(ct);
                }
                catch { }
                throw;
            }
        });
    }

    protected async Task BulkInsertCoreAsync(List<T> items, DbTransaction trans, bool includeIdentityValues = false, CancellationToken ct = default)
    {
        if (this.DbType == DatabaseType.SqlServer)
            await this.MSBulkInsertAsync(items, trans, includeIdentityValues, ct);
        else if (DbType == DatabaseType.PostgreSql)
            await this.PgBulkInsertAsync(items, trans, includeIdentityValues, ct);
        else //Assume mysql
            await this.MyBulkInsertAsync(items, trans, includeIdentityValues, ct);
    }

    readonly SemaphoreSlim _bulkSync = new(1, 1);
    protected sealed record BulkColPlan(PropertyInfo ClrProp, string ColumnName, Type DataType);

    protected List<BulkColPlan> BuildBulkPlan<TEntity>(bool includeStoreGenerated, Func<Microsoft.EntityFrameworkCore.Metadata.IProperty, bool>? extraSkipEfProp = null)
    {
        var et = Ctx.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException($"Entity metadata not found for {typeof(TEntity).Name}");
        var schema = et.GetSchema() ?? null;
        var table = et.GetTableName() ?? throw new InvalidOperationException($"Table name not found for {typeof(TEntity).Name}");
        var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, schema);

        var cols = new List<BulkColPlan>();
        foreach (var p in et.GetProperties())
        {
            if (p.IsShadowProperty()) continue;
            if (p.PropertyInfo == null) continue;

            if (!includeStoreGenerated && p.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd)
                continue;

            if (extraSkipEfProp != null && extraSkipEfProp(p))
                continue;

            var colName = p.GetColumnName(store);
            if (string.IsNullOrWhiteSpace(colName)) continue;

            var t = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            cols.Add(new BulkColPlan(p.PropertyInfo, colName!, t));
        }
        return cols;
    }

    protected string? GetAutoIncrementColumnName<TEntity>()
    {
        var et = Ctx.Model.FindEntityType(typeof(TEntity)) ?? throw new InvalidOperationException($"Entity metadata not found for {typeof(TEntity).Name}");
        var schema = et.GetSchema();
        var table = et.GetTableName()
            ?? throw new InvalidOperationException($"Table name not found for {typeof(TEntity).Name}");
        var store = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(table, schema);
        //Prefer PK columns (99% case)
        var pk = et.FindPrimaryKey();
        if (pk != null)
        {
            foreach (var p in pk.Properties)
            {
                var col = TryGetAutoInc(p, store);
                if (col != null) return col;
            }
        }
        //Fallback: any mapped scalar property
        foreach (var p in et.GetProperties())
        {
            var col = TryGetAutoInc(p, store);
            if (col != null) return col;
        }
        return null;
        string? TryGetAutoInc(
            Microsoft.EntityFrameworkCore.Metadata.IProperty p,
            Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier store)
        {
            if (p.IsShadowProperty() || p.PropertyInfo == null) return null;
            return DbType switch
            {
                DatabaseType.SqlServer =>
                    p.FindAnnotation("SqlServer:ValueGenerationStrategy")?.Value?.ToString() == "IdentityColumn" ? p.GetColumnName(store) : null,
                DatabaseType.MySql =>
                    p.FindAnnotation("MySql:ValueGenerationStrategy")?.Value?.ToString() == "IdentityColumn" ? p.GetColumnName(store) : null,
                DatabaseType.PostgreSql =>
                    p.FindAnnotation("Npgsql:ValueGenerationStrategy")?.Value?.ToString() is "IdentityAlwaysColumn" or "IdentityByDefaultColumn" or "SerialColumn"  ? p.GetColumnName(store) : null,
                _ => null
            };
        }
    }

    protected DataTable BuildDataTable<TEntity>(IReadOnlyList<TEntity> items, IReadOnlyList<BulkColPlan> cols)
    {
        var dt = new DataTable();
        for (int i = 0; i < cols.Count; i++)
            dt.Columns.Add(cols[i].ColumnName, cols[i].DataType);

        foreach (var item in items)
        {
            var row = dt.NewRow();
            for (int i = 0; i < cols.Count; i++)
            {
                var v = cols[i].ClrProp.GetValue(item);
                row[i] = v ?? DBNull.Value;
            }
            dt.Rows.Add(row);
        }
        return dt;
    }

    /// <summary>
    /// Ms-SQl version
    /// </summary>
    /// <param name="items"></param>
    /// <param name="trans"></param>
    /// <param name="includeIdentityValues"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    ///
    protected async Task MSBulkInsertAsync(List<T> items, DbTransaction trans, bool includeIdentityValues = false, CancellationToken ct = default)
    {
        if (DbType != DatabaseType.SqlServer)
            throw new NotSupportedException("MSBulkinsert is supported only for SQL Server");
        if (trans.Connection is not SqlConnection sqlConn)
            throw new InvalidOperationException("Bulk insert requires SQL Server transaction/connection");
        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var tableName = $"[{entityType.GetSchema()}].[{entityType.GetTableName()}]";
        var cols = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        var dt = BuildDataTable(items, cols);
        SqlBulkCopyOptions opts = SqlBulkCopyOptions.Default;
        if (includeIdentityValues) opts |= SqlBulkCopyOptions.KeepIdentity;
        //NOTE: ms-table locking is only beneficial to large data inserts
        //In multi threaded env where lots of inserts take place, locking can be counter productive
        //Thus we set the lock limit to 5000 records.
        if (items.Count >= 5000) opts |= SqlBulkCopyOptions.TableLock;
        SqlCommand? cmd = null;
        if (includeIdentityValues)
        {
            cmd = sqlConn.CreateCommand();
            cmd.Transaction = (SqlTransaction)trans;
            cmd.CommandText = $"SET IDENTITY_INSERT {tableName} ON;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        try
        {
            using var bulkCopy = new SqlBulkCopy(sqlConn, opts, (SqlTransaction)trans) { DestinationTableName = tableName };
            foreach (DataColumn col in dt.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            await bulkCopy.WriteToServerAsync(dt, ct);
        }
        finally
        {
            if (includeIdentityValues)
            {
                if (cmd != null)
                {
                    cmd.CommandText = $"SET IDENTITY_INSERT {tableName} OFF;";
                    await cmd.ExecuteNonQueryAsync(ct);
                    cmd.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// Synchronized Ms-Sql version
    /// </summary>
    /// <param name="items"></param>
    /// <param name="includeIdentityValues"></param>
    /// <param name="trans"></param>
    protected void MSBulkInsertSynchronized(List<T> items, DbTransaction trans, bool includeIdentityValues = false)
    {
        if (DbType != DatabaseType.SqlServer)
            throw new NotSupportedException("Bulk insert is supported only for SQL Server");
        if (trans.Connection is not SqlConnection sqlConn)
            throw new InvalidOperationException("Bulk insert requires SQL Server transaction/connection");

        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var tableName = $"[{entityType.GetSchema()}].[{entityType.GetTableName()}]";
        var cols = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        var dt = BuildDataTable(items, cols);
        SqlBulkCopyOptions opts = SqlBulkCopyOptions.Default;
        if (includeIdentityValues) opts |= SqlBulkCopyOptions.KeepIdentity;
        if (items.Count >= 5000) opts |= SqlBulkCopyOptions.TableLock;

        SqlCommand? cmd = null;
        if (includeIdentityValues)
        {
            cmd = sqlConn.CreateCommand();
            cmd.Transaction = (SqlTransaction)trans;
            cmd.CommandText = $"SET IDENTITY_INSERT {tableName} ON;";
            cmd.ExecuteNonQuery();
        }
        try
        {
            using var bulkCopy = new SqlBulkCopy(sqlConn, opts, (SqlTransaction)trans) { DestinationTableName = tableName };
            foreach (DataColumn col in dt.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
            bulkCopy.WriteToServer(dt);
        }
        finally
        {
            if (includeIdentityValues)
            {
                if (cmd != null)
                {
                    cmd.CommandText = $"SET IDENTITY_INSERT {tableName} OFF;";
                    cmd.ExecuteNonQuery();
                    cmd.Dispose();
                }
            }
        }
    }

    protected void MyBulkInsertSynchronized(List<T> items, DbTransaction trans, bool includeIdentityValues = false)
    {
        if (items.Count == 0) return;
        if (DbType != DatabaseType.MySql)
            throw new NotSupportedException("MySQL bulk insert only valid for MySQL.");
        if (trans.Connection!.GetType().FullName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("Bulk insert requires MySQL connection");
        var fullTableName = this.CalcSqlTableName();
        var includedProps = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        //Build inserts
        int total = items.Count;
        int index = 0;
        const int batchSize = 1000;
        using (var cmd = trans.Connection.CreateCommand())
        {
            cmd.Transaction = trans;
            while (index < total)
            {
                cmd.Parameters.Clear();
                var sb = new System.Text.StringBuilder();
                sb.Append($"INSERT INTO {fullTableName} (");
                sb.Append(string.Join(", ", includedProps.Select(p => $"`{p.ColumnName}`")));
                sb.Append(") VALUES ");
                int batchEnd = Math.Min(index + batchSize, total);
                int paramCounter = 0;
                for (int i = index; i < batchEnd; i++)
                {
                    if (i > index) sb.Append(", ");
                    sb.Append("(");
                    for (int j = 0; j < includedProps.Count; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        var prop = includedProps[j];
                        var paramName = $"@p_{paramCounter++}";
                        sb.Append(paramName);
                        var p = cmd.CreateParameter();
                        p.ParameterName = paramName;
                        p.Value = prop.ClrProp.GetValue(items[i]) ?? DBNull.Value;
                        cmd.Parameters.Add(p);
                    }
                    sb.Append(")");
                }
                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
                index = batchEnd;
            }
        }
        var inclColName = includeIdentityValues ? GetAutoIncrementColumnName<T>() : null;
        if (includeIdentityValues && !string.IsNullOrWhiteSpace(inclColName))
            MySqlReseedAutoIncrementSyncronized(fullTableName, inclColName, trans);
    }

    protected async Task MyBulkInsertAsync(List<T> items, DbTransaction trans, bool includeIdentityValues = false, CancellationToken ct = default)
    {
        if (items.Count == 0) return;
        if (DbType != DatabaseType.MySql)
            throw new NotSupportedException("MySQL bulk insert only valid for MySQL.");
        if (trans.Connection!.GetType().FullName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException("Bulk insert requires MySQL connection");
        var fullTableName = this.CalcSqlTableName();
        var includedProps = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        int total = items.Count;
        int index = 0;
        const int batchSize = 1000;
        await using (var cmd = trans.Connection.CreateCommand())
        {
            cmd.Transaction = trans;
            while (index < total)
            {
                cmd.Parameters.Clear();
                var sb = new System.Text.StringBuilder();
                sb.Append($"INSERT INTO {fullTableName} (");
                sb.Append(string.Join(", ", includedProps.Select(p => $"`{p.ColumnName}`")));
                sb.Append(") VALUES ");
                int batchEnd = Math.Min(index + batchSize, total);
                int paramCounter = 0;
                for (int i = index; i < batchEnd; i++)
                {
                    if (i > index) sb.Append(", ");
                    sb.Append("(");
                    for (int j = 0; j < includedProps.Count; j++)
                    {
                        if (j > 0) sb.Append(", ");
                        var prop = includedProps[j];
                        var paramName = $"@p_{paramCounter++}";
                        sb.Append(paramName);
                        var p = cmd.CreateParameter();
                        p.ParameterName = paramName;
                        p.Value = prop.ClrProp.GetValue(items[i]) ?? DBNull.Value;
                        cmd.Parameters.Add(p);
                    }
                    sb.Append(")");
                }
                cmd.CommandText = sb.ToString();
                await cmd.ExecuteNonQueryAsync(ct);
                index = batchEnd;
            }
        }
        var inclColName = includeIdentityValues ? GetAutoIncrementColumnName<T>() : null;
        if (includeIdentityValues && !string.IsNullOrWhiteSpace(inclColName))
            await MySqlReseedAutoIncrementAsync(fullTableName, inclColName, trans, ct);
    }


    protected async Task PgBulkInsertAsync(List<T> items, DbTransaction trans, bool includeIdentityValues = false, CancellationToken ct = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) return;
        if (trans == null) throw new ArgumentNullException(nameof(trans));
        if (DbType != DatabaseType.PostgreSql) throw new NotSupportedException("Bulk insert is supported only for PostgreSQL");
        if (trans.Connection is not Npgsql.NpgsqlConnection pgConn) throw new InvalidOperationException("Bulk insert requires PostgreSQL connection");
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var schema = et.GetSchema() ?? "public";
        var table = et.GetTableName() ?? throw new InvalidOperationException("Table name not found");
        var fullTableName = $"\"{schema}\".\"{table}\"";
        //Note: for COPY, Postgres will accept explicit values for identity columns anyway.
        //So includeIdentityValues should ONLY affect whether we include the identity column in cols,
        //not any OVERRIDING clause (COPY doesn't take that clause).
        var cols = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        var copySql = $"COPY {fullTableName} ({string.Join(", ", cols.Select(c => $"\"{c.ColumnName}\""))}) FROM STDIN (FORMAT BINARY)";
        //await using var writer = await pgConn.BeginBinaryImportAsync(copySql, ct);
        await using (var writer = pgConn.BeginBinaryImport(copySql))
        {
            foreach (var item in items)
            {
                await writer.StartRowAsync(ct);
                foreach (var col in cols)
                {
                    var val = col.ClrProp.GetValue(item);
                    if (val == null) { await writer.WriteNullAsync(ct); continue; }
                    var typ = InferNpgsqlDbType(col.ClrProp.PropertyType);
                    await writer.WriteAsync(val, typ, ct);
                }
            }
            await writer.CompleteAsync(ct);
        }
        if (includeIdentityValues)
        {
            //NOTE: Posgres requires manual id-re-seeding after identity-bulk-insert
            var inclColName = includeIdentityValues ? GetAutoIncrementColumnName<T>() : null;
            if (includeIdentityValues && !string.IsNullOrWhiteSpace(inclColName))
                await PgReseedAutoIncrementAsync(fullTableName, inclColName, trans, ct);
       }
    }

    protected void PgBulkInsertSynchronized(List<T> items, DbTransaction trans, bool includeIdentityValues = false)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) return;
        if (trans == null) throw new ArgumentNullException(nameof(trans));
        if (DbType != DatabaseType.PostgreSql) throw new NotSupportedException("Bulk insert is supported only for PostgreSQL");
        if (trans.Connection is not Npgsql.NpgsqlConnection pgConn) throw new InvalidOperationException("Bulk insert requires PostgreSQL connection");
        var et = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException("Entity metadata not found");
        var schema = et.GetSchema() ?? "public";
        var table = et.GetTableName() ?? throw new InvalidOperationException("Table name not found");
        var fullTableName = $"\"{schema}\".\"{table}\"";
        var cols = BuildBulkPlan<T>(includeStoreGenerated: includeIdentityValues);
        var copySql = $"COPY {fullTableName} ({string.Join(", ", cols.Select(c => $"\"{c.ColumnName}\""))}) FROM STDIN (FORMAT BINARY)";
        using (var writer = pgConn.BeginBinaryImport(copySql))
        {
            foreach (var item in items)
            {
                writer.StartRow();
                foreach (var col in cols)
                {
                    var val = col.ClrProp.GetValue(item);
                    if (val == null) { writer.WriteNull(); continue; }
                    //If you trust Npgsql inference, this is usually enough:
                    //writer.Write(val);
                    //If you prefer explicit typing:
                    var typ = InferNpgsqlDbType(col.ClrProp.PropertyType);
                    writer.Write(val, typ);
                }
            }
            writer.Complete();
        }
        if (includeIdentityValues)
        {
            //NOTE: Posgres requires manual id-re-seeding after identity-bulk-insert
            var inclColName = includeIdentityValues ? GetAutoIncrementColumnName<T>() : null;
            if (includeIdentityValues && !string.IsNullOrWhiteSpace(inclColName))
               PgReseedAutoIncrementSyncronized(fullTableName, inclColName, trans);
        }
    }

    protected static object? PrepareForPostgres(object? value)
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

    /// <summary>
    /// MYSQL only
    /// Reset Increment after identity insert
    /// </summary>
    /// <param name="fullTableName"></param>
    /// <param name="idColumnName"></param>
    /// <param name="trans"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private async Task MySqlReseedAutoIncrementAsync(string fullTableName, string idColumnName, DbTransaction trans, CancellationToken ct = default)
    {
        var conn = trans.Connection ?? throw new InvalidOperationException("Transaction has no connection.");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = $"SELECT IFNULL(MAX(`{idColumnName}`), 0) + 1 FROM {fullTableName};";
        var scalar = await cmd.ExecuteScalarAsync(ct);
        var nextVal = Convert.ToInt64(scalar ?? 1);
        cmd.CommandText = $"ALTER TABLE {fullTableName} AUTO_INCREMENT = {nextVal};";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private void MySqlReseedAutoIncrementSyncronized(string fullTableName, string idColumnName, DbTransaction trans)
    {
        var conn = trans.Connection ?? throw new InvalidOperationException("Transaction has no connection.");
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = $"SELECT IFNULL(MAX(`{idColumnName}`), 0) + 1 FROM {fullTableName};";
        var scalar = cmd.ExecuteScalar();
        var nextVal = Convert.ToInt64(scalar ?? 1);
        cmd.CommandText = $"ALTER TABLE {fullTableName} AUTO_INCREMENT = {nextVal};";
        cmd.ExecuteNonQuery();
    }

    private void PgReseedAutoIncrementSyncronized(string fullTableName, string idColumnName, DbTransaction trans)
    {
        var conn = trans.Connection ?? throw new InvalidOperationException("Transaction has no connection.");
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "SELECT setval( pg_get_serial_sequence('"+ fullTableName + "', '" + idColumnName + "'), (SELECT COALESCE(MAX( \"" + idColumnName + "\"), 0) FROM " + fullTableName + "), true );";
        cmd.ExecuteNonQuery();
    }

    private async Task PgReseedAutoIncrementAsync(string fullTableName, string idColumnName, DbTransaction trans, CancellationToken ct = default)
    {
        var conn = trans.Connection ?? throw new InvalidOperationException("Transaction has no connection.");
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "SELECT setval( pg_get_serial_sequence('" + fullTableName + "', '" + idColumnName + "'), (SELECT COALESCE(MAX( \"" + idColumnName + "\"), 0) FROM " + fullTableName + "), true );";
        await cmd.ExecuteNonQueryAsync(ct);
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
    #endregion

    /// <summary>
    /// Marks only this entity as Modified, no graph walk, no DetectChanges
    /// All columns will be saved
    /// </summary>
    /// <param name="entity"></param>
    public void MarkModified(T entity)
    {
        var entry = Ctx.Entry(entity);
        if (entry.State == EntityState.Detached)
            DbSet.Attach(entity);
        entry.State = EntityState.Modified;
    }

    /// <summary>
    /// Marks the entire entity as Modified so every mapped column is updated on SaveChanges.
    /// Walks nav-graph, marks everything Modified, may trigger wide updates
    /// </summary>
    public void UpdateAllCols(T entities) => DbSet.Update(entities);

    protected string Quote(string name) => DbType switch
    {
        DatabaseType.SqlServer => $"[{name}]",
        DatabaseType.PostgreSql => $"\"{name}\"",
        DatabaseType.MySql => $"`{name}`",
        _ => name
    };

    protected string CalcSqlTableName()
    {
        var entityType = Ctx.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException($"Entity type {typeof(T).Name} not found in the EF model.");
        var schema = entityType.GetSchema();
        var table = entityType.GetTableName() ?? throw new InvalidOperationException($"Table mapping not resolved for {typeof(T).Name}.");
        string fullName = DbType switch
        {
            DatabaseType.SqlServer => $"[{schema ?? "dbo"}].[{table}]",
            DatabaseType.PostgreSql => $"\"{schema ?? "public"}\".\"{table}\"",
            DatabaseType.MySql => string.IsNullOrWhiteSpace(schema) ? table : $"`{schema}`_`{table}`",
            _ => throw new NotSupportedException($"Unsupported DB type: {DbType}")
        };
        return fullName;
    }
}

public class EfLongIdRepo<T> : EfRepo<T>, ILongIdRepo<T> where T : class
{
    public EfLongIdRepo(DbContext ctx, DatabaseType dbType) : base(ctx, dbType) { }
    public Task<T?> ByIdNoTrackAsync(long id) => ByKeyNoTrackAsync(id);
    public T? ByIdNoTrackSynchronized(long id) => ByKeyNoTrackSynchronized(id);
    public Task<T?> ByIdAsync(long id) => ByKeyAsync(id);
    public T? ByIdSynchronized(long id) => ByKeySynchronized(id);

}

// Read-only long-Id repo
public class EfLongIdReadRepo<T> : EfReadRepo<T>, ILongIdReadRepo<T> where T : class
{
    public EfLongIdReadRepo(DbContext ctx, DatabaseType dbType) : base(ctx, dbType) { }
    public Task<T?> ByIdNoTrackAsync(long id) => ByKeyNoTrackAsync(id);
    public T? ByIdNoTrackSynchronized(long id) => ByKeyNoTrackSynchronized(id);
}


public class QueryResult<T> where T : class
{
    public IList<T> Results { get; set; } = new List<T>();
    public long? InlineCount { get; set; }
    public int PageNo { get; set; } = 0;
    public int ErrorNo { get; set; } = 0;
    public string ErrorMessage { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;

    public QueryResult() { }
    public QueryResult(Exception e) => FillException(e);

    public bool HasError => ErrorNo != 0;

    public static QueryResult<T> Ok(IList<T> results, long? inlineCount = null, int pageNo = 0)
         => new() { Results = results ?? new List<T>(), InlineCount = inlineCount, PageNo = pageNo };

    public static QueryResult<T> Fail(Exception e)
    {
        var r = new QueryResult<T>();
        r.FillException(e);
        return r;
    }

    static string ExceptRecurse(Exception e)
        => e.Message + " \r\n" + (e.InnerException != null ? "Inner exception: " + ExceptRecurse(e.InnerException) : "");


    public void FillException(Exception e) { ErrorNo = 1; ErrorMessage = e.Message; ErrorDetails = ExceptRecurse(e); }
    public void ThrowIfError(string context) { if (ErrorNo != 0) throw new InvalidOperationException($"{context}: {ErrorMessage}\n{ErrorDetails}"); }
}
