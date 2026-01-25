// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

//
// OData pipeline overview (EfReadRepo<T>)
// --------------------------------------
// This implementation supports two execution modes:
//
//   1) Typed mode (entities):
//      BuildODataQueryPlan → (optional) ApplyODataExpandAsInclude → MaterializeODataAsync / Synchronized
//
//      - ItemsQuery returns IQueryable<T> and can be materialized as List<T>.
//      - Includes are applied only in this mode (Expand-as-Include).
//      - InlineCount is computed from CountQuery only when plan.CountRequested is true.
//
//   2) Shaped mode (projection objects):
//      BuildODataQueryPlan → ApplyODataSelectExpand → MaterializeODataShapedAsync / Synchronized
//
//      - ApplyTo($select/$expand) projects into OData wrapper/projection types.
//      - The shaped query can no longer be materialized as T.
//      - MaterializeODataAsync throws when plan.IsShaped is true.
//
// Policy behavior
// ---------------
// - The ODataPolicy controls allowed features and limits.
// - When a feature is disallowed (e.g., Filter/OrderBy/Expand/Select/Count),
//   the implementation ignores that option and appends a message to plan.Report.
// - This is "soft enforcement" (ignore + report), not "hard enforcement" (throw).
//
// Count behavior
// --------------
// - BuildODataQueryPlan never executes COUNT.
// - plan.CountRequested indicates whether InlineCount should be computed at materialization time.
// - In this implementation CountRequested is set when:
//     • forceCount is true, OR
//     • $count=true, OR
//     • paging is used (skip/top) and the server wants an InlineCount for paging UX.
// - If AllowCount is false, InlineCount is always null and a report entry is emitted.
//
// Expand-as-Include behavior
// --------------------------
// - ApplyODataExpandAsInclude parses the $expand clause and applies EF .Include(path) to ItemsQuery.
// - Only the navigation paths are used.
// - Nested expand options ($filter/$orderby/$top/$select) are ignored.
// - AllowedExpand acts as an allow-list; MaxExpansionDepth limits traversal.
// - This can be expensive: Includes may produce large joins and duplication.
//   Use AllowedExpand and MaxExpansionDepth to keep it safe.
//
// Shaping behavior
// ----------------
// - ApplyODataSelectExpand uses OData's ApplyTo(...) which produces a projected element type.
// - After shaping, plan.IsShaped is set to true.
// - Always materialize shaped queries using QueryResult<object>.
//
// Error handling style
// --------------------
// - Typed materializers throw for invalid plan state (null queries, shaped plan).
// - Shaped materializers currently return QueryResult<object> with ErrorNo/ErrorMessage for null inputs.
//   Keep this consistent across methods if you want a single error-handling style.
//
//Se Iterface for function documentation

using EfCore.Boost.DbRepo.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.UriParser;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfCore.Boost;

/// <summary>
/// Defines a set of OData query constraints and permissions that control which query options are allowed and their limits for an OData endpoint.
/// By default everything is allowed except Expand and OrderBy.
/// </summary>
/// <remarks>
/// Use this record to configure which OData query options are enabled and to enforce limits on query
/// complexity. These settings help protect the service from overly complex or expensive queries and can be tailored to
/// the needs of each endpoint.
/// </remarks>
/// <param name="MaxTop">The maximum number of items that can be returned in a single query using the $top option. By default this is null to allow any value.</param>
/// <param name="ServerPageSize">If specified the maximum number of items returned per server-generated page. Null by default -> no limit on page size.</param>
/// <param name="AllowOrderBy">To allow the use of the $orderby query option; Allowed by default. Overrides AllowedOrderBy of not set.</param>
/// <param name="AllowedOrderBy">If you want to allow sorting by some columns but not others ($orderby), then list the column names that are permitted here. Null by default. </param>
/// <param name="AllowFilter">To allow the use of the $filter query option. Allowed by default.</param>
/// <param name="AllowExpand">To allow the use of the $expand query option. Disallowed by default.</param>
/// <param name="AllowedExpand">An array of navigation property names that are permitted in the $expand query option. Specify null to allow all navigation properties.</param>
/// <param name="MaxExpansionDepth">The maximum depth allowed for $expand operations. Must be a non-negative integer.</param>
/// <param name="AllowSelect">To allow the use of the $select query option. Allowed by default.</param>
/// <param name="AllowCount">true to allow the use of the $count query option; otherwise, false.</param>
public sealed record ODataPolicy(
    int? MaxTop = null,
    int? ServerPageSize = null,
    bool AllowOrderBy = true,
    string[]? AllowedOrderBy = null,
    bool AllowFilter = true,
    bool AllowExpand = false,
    string[]? AllowedExpand = null,
    int MaxExpansionDepth = 5,
    bool AllowSelect = true,
    bool AllowCount = true
);

/// <summary>
/// Represents a compiled OData query plan, including the queryable items, count query, options, and policy for a
/// specific entity type.
/// </summary>
/// <remarks>An ODataQueryPlan encapsulates the results of parsing and applying OData query options to a data
/// source. It provides access to the filtered and shaped query, an optional count query, and the applied OData options
/// and policy. This class is typically used to execute OData queries against an IQueryable data source and to retrieve
/// both the result set and the total count when requested.</remarks>
/// <typeparam name="T">The entity type in the Reposituary to which the OData query applies.</typeparam>
public sealed class ODataQueryPlan<T> where T : class
{
    public IQueryable<T>? ItemsQuery { get; set; }
    public IQueryable<T>? CountQuery { get; set; }
    public ODataQueryOptions<T> Options { get; }
    public ODataPolicy Policy { get; } = new ODataPolicy();
    public bool CountRequested { get; set; } = false;
    public bool IsShaped { get; set; } = false;
    public List<string> Report { get; set; } = [];

    public ODataQueryPlan(ODataQueryOptions<T> options, ODataPolicy policy, bool countRequested)
    {
        Options = options ?? throw new ArgumentNullException("Options are missing");
        Policy = policy ?? throw new ArgumentNullException("Plan is missing");
        CountRequested = countRequested;
    }
}

public partial class EfReadRepo<T> where T : class
{

    /// <summary>
    /// Prepares an OData query plan for filtering, ordering, and paging.
    /// The returned plan contains two IQueryable pipelines: one for the result items and one for the total count.
    /// No database queries are executed at this stage.    /// 
    /// After building the plan, the caller may:
    ///  • materialize it directly (typed results), or
    ///  • apply $expand / $select shaping before materialization.    /// 
    /// All operations are constrained by the provided ODataPolicy.
    /// Disallowed options (for example OrderBy or Filter) are ignored
    /// and recorded in the plan's Report list instead of throwing.
    /// </summary>
    /// <param name="baseQuery">
    /// The base IQueryable defining the security and scope boundaries of the query.
    /// </param>
    /// <param name="options">
    /// The OData query options parsed from the HTTP request.
    /// </param>
    /// <param name="policy">
    /// The policy that controls which OData features are allowed and how they are limited.
    /// </param>
    /// <param name="forceCount">
    /// Forces the plan to compute an inline count even if $count was not requested.
    /// This is typically used by server-side paging implementations.
    /// </param>
    /// <returns>
    /// An ODataQueryPlan for the repository entity type.
    /// The plan can be further shaped or materialized.
    /// </returns>
    public ODataQueryPlan<T> BuildODataQueryPlan(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = false)
    {
        if (baseQuery == null) throw new ArgumentNullException("Base query is missing");
        if (options == null) throw new ArgumentNullException("Options are missing");
        policy ??= new ODataPolicy();
        var settings = new ODataQuerySettings();
        var ret = new ODataQueryPlan<T>(options, policy, forceCount);
        ret.CountQuery = baseQuery;
        ret.ItemsQuery = baseQuery;
        if (options.Filter != null)
        {
            if (!policy.AllowFilter)
                ODQueryUtil.AddReport(ret.Report, "FilterIgnored");
            else
            {
                ret.CountQuery = (IQueryable<T>)options.Filter.ApplyTo(ret.CountQuery, settings);
                ret.ItemsQuery = (IQueryable<T>)options.Filter.ApplyTo(ret.ItemsQuery, settings);
            }
        }
        if (options.OrderBy != null)
        {
            if (!policy.AllowOrderBy)
                ODQueryUtil.AddReport(ret.Report, "OrderByIgnored");
            else
            {
                var orderByOk = true;
                if (policy.AllowedOrderBy != null && policy.AllowedOrderBy.Length != 0)
                {
                    var raw = options.RawValues?.OrderBy;
                    if (!ODQueryUtil.IsOrderByAllowed(raw, policy.AllowedOrderBy))
                        ODQueryUtil.AddReport(ret.Report, "OrderByIgnored.NotAllowed");
                    else
                        ret.ItemsQuery = (IQueryable<T>)options.OrderBy.ApplyTo(ret.ItemsQuery, settings);
                    orderByOk = false;
                }
                if (orderByOk)
                    ret.ItemsQuery = (IQueryable<T>)options.OrderBy.ApplyTo(ret.ItemsQuery, new ODataQuerySettings());
            }
        }

        int? skip = options.Skip?.Value;
        int? top = options.Top?.Value;
        if (skip.HasValue && skip.Value < 0)
        {
            ODQueryUtil.AddReport(ret.Report, "SkipIgnored.Negative");
            skip = null;
        }
        if (top.HasValue && top.Value < 0)
        {
            ODQueryUtil.AddReport(ret.Report, "TopIgnored.Negative");
            top = null;
        }
        if (!top.HasValue && policy.ServerPageSize.HasValue && policy.ServerPageSize.Value > 0)
        {
            top = policy.ServerPageSize.Value;
            ODQueryUtil.AddReport(ret.Report, "TopDefaulted.ServerPageSize");
        }
        if (top.HasValue && policy.MaxTop.HasValue && policy.MaxTop.Value > 0 && top.Value > policy.MaxTop.Value)
        {
            top = policy.MaxTop.Value;
            ODQueryUtil.AddReport(ret.Report, "TopClamped.MaxTop");
        }
        if (skip.HasValue)
            ret.ItemsQuery = ret.ItemsQuery.Skip(skip.Value);
        if (top.HasValue)
            ret.ItemsQuery = ret.ItemsQuery.Take(top.Value);

        //We do counting if allowed and (paging is used, count requested in options or forced to this function
        var countRequested = options.Count?.Value == true;
        var pagingUsed = (skip.HasValue && skip.Value > 0) || (top.HasValue && top.Value > 0);
        ret.CountRequested = policy.AllowCount && (forceCount || countRequested || pagingUsed);
        if (!policy.AllowCount && (forceCount || countRequested)) 
            ODQueryUtil.AddReport(ret.Report, "CountNotAllowed");
        return ret;
    }

    /// <summary>
    /// Applies OData $expand requests to the plan as Entity Framework Include paths.    
    /// Only the navigation paths themselves are used. Any nested options inside $expand
    /// such as $filter, $orderby, $top, or $select are ignored.    /// 
    /// This method modifies only the ItemsQuery of the plan. The CountQuery is not affected.    
    /// Expand requests are validated against the provided ODataPolicy. Disallowed or
    /// unsupported expands are ignored and recorded in the plan's Report instead of throwing. 
    /// This mode is intended for scenarios where the client is allowed to request navigation loading but not arbitrary shaping.
    /// Use with care, as Includes can significantly affect query performance.
    /// </summary>
    /// <param name="plan">
    /// The query plan produced by BuildODataQueryPlan.
    /// </param>
    /// <returns>
    /// The same plan instance with navigation Includes applied to its ItemsQuery.
    /// </returns>
    public ODataQueryPlan<T> ApplyODataExpandAsInclude(ODataQueryPlan<T> plan)
    {
        if (plan.ItemsQuery == null) throw new ArgumentNullException("ItemsQuery is null.");
        if (plan.Options == null) throw new ArgumentNullException("Options are missing");
        if (plan.Policy == null) throw new ArgumentNullException("Policy is missing");
        var policy = plan.Policy;
        var clause = plan.Options.SelectExpand?.SelectExpandClause;
        if (clause == null)
            return plan;
        if (!policy.AllowExpand)
        {
            ODQueryUtil.AddReport(plan.Report, "ExpandIgnored.PolicyDisallow");
            return plan;
        }
        var allowed = policy.AllowedExpand;
        HashSet<string>? allowedSet = null;
        if (allowed != null && allowed.Length != 0)
            allowedSet = new HashSet<string>(allowed.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        var includes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ODQueryUtil.ExtractIncludePaths(clause.SelectedItems, includes, allowedSet, policy.MaxExpansionDepth, plan.Report);
        // Always report nested options present in request (even if includes are empty / disallowed)
        ODQueryUtil.ReportNestedExpandOptions(clause, plan.Report);
        if (includes.Count == 0)
            return plan;
        foreach (var p in includes)
            plan.ItemsQuery = plan.ItemsQuery.Include(p);
        return plan;
    }

    /// <summary>
    /// Executes a prepared OData query plan and materializes the result as repository entities. 
    /// The ItemsQuery of the plan is executed to produce the result set.
    /// If the plan requires an inline count, the CountQuery is executed separately to compute the total number of matching root entities.    /// 
    /// This method can only be used for non-shaped plans. If the plan has been shapedusing $select or $expand projection, 
    /// this method will throw.
    /// </summary>
    /// <param name="plan">
    /// The query plan produced by BuildODataQueryPlan and optionally modified by ApplyODataExpandAsInclude.
    /// </param>
    /// <param name="ct">
    /// A cancellation token used to cancel the database operations.
    /// </param>
    /// <returns>
    /// A QueryResult containing the materialized entities and, if requested, the inline count of all matching root entities.
    /// </returns>
    public async Task<QueryResult<T>> MaterializeODataAsync(ODataQueryPlan<T> plan, CancellationToken ct = default)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.IsShaped) throw new InvalidOperationException($"Cannot materialize shaped OData query as {typeof(T).Name}. Use MaterializeODataShaped* and return QueryResult<object>.");
        if (plan.ItemsQuery == null) throw new InvalidOperationException("ItemsQuery is null.");
        if (plan.CountQuery == null) throw new InvalidOperationException("CountQuery is null.");
        var r = new QueryResult<T>();
        try
        {
            var skip = plan.Options.Skip?.Value;
            var top = plan.Options.Top?.Value;
            if (top.HasValue && top.Value > 0)
                r.PageNo = skip.HasValue && skip.Value > 0 ? (skip.Value / top.Value) + 1 : 1;
            else
                r.PageNo = 0;
            r.Results = await plan.ItemsQuery.ToListAsync(ct);
            if (plan.CountRequested)
            {
                if (!plan.Policy.AllowCount)
                    r.InlineCount = null;
                else
                    r.InlineCount = await plan.CountQuery.LongCountAsync(ct);
            }
            else
                r.InlineCount = null;
            return r;
        }
        catch (Exception e) { r.FillException(e); return r; }
    }

    //Synchronized version of MaterializeODataAsync
    public QueryResult<T> MaterializeODataSynchronized(ODataQueryPlan<T> plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.IsShaped) throw new InvalidOperationException($"Cannot materialize shaped OData query as {typeof(T).Name}. Use MaterializeODataShaped* and return QueryResult<object>.");
        if (plan.ItemsQuery == null) throw new InvalidOperationException("ItemsQuery is null.");
        if (plan.CountQuery == null) throw new InvalidOperationException("CountQuery is null.");
        var r = new QueryResult<T>();
        try
        {
            var skip = plan.Options.Skip?.Value;
            var top = plan.Options.Top?.Value;
            if (top.HasValue && top.Value > 0)
                r.PageNo = skip.HasValue && skip.Value > 0 ? (skip.Value / top.Value) + 1 : 1;
            else
                r.PageNo = 0;
            r.Results = plan.ItemsQuery.ToList();
            if (plan.CountRequested)
            {
                if (!plan.Policy.AllowCount)
                    r.InlineCount = null;
                else
                    r.InlineCount = plan.CountQuery.LongCount();
            }
            else
                r.InlineCount = null;
            return r;
        }
        catch (Exception e) { 
            r.FillException(e); 
            return r; 
        }
    }

    /// <summary>
    /// Convenience method that builds and executes an OData query plan in one step, without applying $select or $expand shaping.
    /// This is equivalent to calling BuildODataQueryPlan followed by MaterializeODataAsync on the resulting plan.
    /// Any $select or $expand options in the request are ignored.
    /// </summary>
    /// <param name="baseQuery">
    /// The base IQueryable defining the scope and security boundaries of the query.
    /// </param>
    /// <param name="options">
    /// The OData query options parsed from the HTTP request.
    /// </param>
    /// <param name="policy">
    /// The policy controlling which OData features are allowed.
    /// If null, default policy values are used.
    /// </param>
    /// <param name="forceCount">
    /// Forces the query to compute an inline count even if $count was not requested.
    /// </param>
    /// <param name="ct">
    /// A cancellation token used to cancel the database operations.
    /// </param>
    /// <returns>
    /// A QueryResult containing the filtered and paged entities and, if requested, the inline count of all matching root entities.
    /// </returns>
    public async Task<QueryResult<T>> FilterODataAsync(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = true, CancellationToken ct = default)
    {
        var plan = this.BuildODataQueryPlan(baseQuery, options, policy, forceCount);
        return await this.MaterializeODataAsync(plan, ct);
    }

    public QueryResult<T> FilterODataSynchronized(IQueryable<T> baseQuery, ODataQueryOptions<T> options, ODataPolicy? policy = null, bool forceCount = true)
    {
        var plan = this.BuildODataQueryPlan(baseQuery, options, policy, forceCount);
        return this.MaterializeODataSynchronized(plan);
    }

    /// <summary>
    /// Applies OData $select and/or $expand as projection (shaping) to the plan's ItemsQuery. 
    /// After this step, the query no longer returns repository entities. The result element type becomes an internal OData wrapper/projection type, 
    /// so the query must be materialized as shaped objects (for example QueryResult&lt;object&gt;).    /// 
    /// Policy rules are enforced before shaping. Disallowed select/expand requests are ignored and recorded in the plan's Report instead of throwing.
    /// </summary>
    /// <param name="plan">
    /// The query plan produced by BuildODataQueryPlan.
    /// </param>
    /// <param name="settings">
    /// Optional OData query settings used by the OData ApplyTo pipeline. If null, default settings are used.
    /// </param>
    /// <returns>
    /// An untyped IQueryable representing the shaped projection.
    /// </returns>

    public IQueryable ApplyODataSelectExpand(ODataQueryPlan<T> plan, ODataQuerySettings? settings = null)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        if (plan.ItemsQuery == null) throw new ArgumentNullException("ItemsQuery is null.");
        if (plan.Options == null) throw new ArgumentNullException("Options are missing");
        if (plan.Policy == null) throw new ArgumentNullException("Plan is missing");
        settings ??= new ODataQuerySettings();
        var policy = plan.Policy;
        var opts = plan.Options;
        var clause = opts.SelectExpand?.SelectExpandClause;
        if (clause == null) return plan.ItemsQuery;
        var hasSelect = ODQueryUtil.HasSelect(clause);
        var hasExpand = ODQueryUtil.HasExpand(clause);
        if (hasSelect && !policy.AllowSelect)
        {
            ODQueryUtil.AddReport(plan.Report, "SelectIgnored.PolicyDisallow");
            hasSelect = false;
        }
        if (hasExpand && !policy.AllowExpand)
        {
            ODQueryUtil.AddReport(plan.Report, "ExpandIgnored.PolicyDisallow");
            hasExpand = false;
        }
        // allowlist enforcement (paths)
        if (hasExpand && policy.AllowedExpand != null && policy.AllowedExpand.Length != 0)
        {
            var allowedSet = ODQueryUtil.BuildAllowSet(policy.AllowedExpand);
            if (allowedSet != null)
            {
                if (!ODQueryUtil.ExpandClauseAllowed(clause, allowedSet, policy.MaxExpansionDepth, plan.Report))
                {
                    ODQueryUtil.AddReport(plan.Report, "ExpandIgnored.NotAllowed");
                    hasExpand = false;
                }
            }
        }
        if (!hasSelect && !hasExpand) 
            return plan.ItemsQuery;
        plan.IsShaped = true;
        // We no longer can map it to T !
        // We rely on OData to do the projection; result element type will not be T.
        return opts.SelectExpand!.ApplyTo(plan.ItemsQuery, settings);
    }

    /// <summary>
    /// Executes a shaped OData query and materializes the result as OData projection objects.    
    /// The provided shapedQuery is expected to be the result of ApplyODataSelectExpand and therefore returns OData wrapper objects rather than
    /// repository entities.     
    /// The plan's CountQuery is used to compute the inline count when required.
    /// </summary>
    /// <param name="plan">
    /// The query plan produced by BuildODataQueryPlan.
    /// </param>
    /// <param name="shapedQuery">
    /// The shaped IQueryable returned by ApplyODataSelectExpand.
    /// </param>
    /// <param name="ct">
    /// A cancellation token used to cancel the database operations.
    /// </param>
    /// <returns>
    /// A QueryResult containing the shaped OData projection objects and, if requested, the inline count of all matching root entities.
    /// </returns>
    public async Task<QueryResult<object>> MaterializeODataShapedAsync(ODataQueryPlan<T> plan, IQueryable shapedQuery, CancellationToken ct = default)
    {
        var r = new QueryResult<object>();
        try
        {
            if (plan == null) { r.ErrorNo = 1; r.ErrorMessage = "Plan is null"; return r; }
            if (shapedQuery == null) { r.ErrorNo = 1; r.ErrorMessage = "ShapedQuery is null"; return r; }
            if (plan.CountQuery == null) { r.ErrorNo = 1; r.ErrorMessage = "CountQuery is null"; return r; }
            var skip = plan.Options.Skip?.Value;
            var top = plan.Options.Top?.Value;
            if (top.HasValue && top.Value > 0)
                r.PageNo = skip.HasValue && skip.Value > 0 ? (skip.Value / top.Value) + 1 : 1;
            else
                r.PageNo = 0;
            if (shapedQuery is IAsyncEnumerable<object>)
                r.Results = await shapedQuery.Cast<object>().ToListAsync(ct);
            else
                r.Results = shapedQuery.Cast<object>().ToList();
            if (plan.CountRequested)
            {
                if (!plan.Policy.AllowCount)
                    r.InlineCount = null;
                else
                    r.InlineCount = await plan.CountQuery.LongCountAsync(ct);
            }
            else
                r.InlineCount = null;
            return r;
        }
        catch (Exception e)
        {
            r.FillException(e);
            return r;
        }
    }

    public QueryResult<object> MaterializeODataShapedSynchronized(ODataQueryPlan<T> plan, IQueryable shapedQuery)
    {
        var r = new QueryResult<object>();
        try
        {
            if (plan == null) { r.ErrorNo = 1; r.ErrorMessage = "Plan is null"; return r; }
            if (shapedQuery == null) { r.ErrorNo = 1; r.ErrorMessage = "ShapedQuery is null"; return r; }
            if (plan.CountQuery == null) { r.ErrorNo = 1; r.ErrorMessage = "CountQuery is null"; return r; }
            var skip = plan.Options.Skip?.Value;
            var top = plan.Options.Top?.Value;
            if (top.HasValue && top.Value > 0)
                r.PageNo = skip.HasValue && skip.Value > 0 ? (skip.Value / top.Value) + 1 : 1;
            else
                r.PageNo = 0;
            if (shapedQuery is IAsyncEnumerable<object>)
                r.Results = shapedQuery.Cast<object>().ToList();
            else
                r.Results = shapedQuery.Cast<object>().ToList();
            if (plan.CountRequested)
            {
                if (!plan.Policy.AllowCount)
                    r.InlineCount = null;
                else
                    r.InlineCount = plan.CountQuery.LongCount();
            }
            else
                r.InlineCount = null;
            return r;
        }
        catch (Exception e)
        {
            r.FillException(e);
            return r;
        }
    }
}
