using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DbRepo;


// EfRepo.OData.cs (partial) — optional OData adapter near the repo
// Requires: Microsoft.AspNetCore.OData + Microsoft.EntityFrameworkCore
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Query.Validator;
using Microsoft.EntityFrameworkCore;

namespace DbRepo;

public sealed record ODataPolicy(
    int? MaxTop = null,
    int? ServerPageSize = null,
    string[]? AllowedOrderBy = null,
    bool AllowFilter = true,
    bool AllowOrderBy = true,
    bool AllowSelect = true,
    bool AllowExpand = false,
    bool AllowCount = true
);

public partial class EfRepo<T> where T : class
{
    /// <summary>
    /// Apply OData to a queryable safely per policy (no materialization).
    /// Useful when you want to reuse the filtered IQueryable for summaries/grouping.
    /// Pass an optional 'source' (e.g., projected DTO query); otherwise uses QueryNoTrack().
    /// </summary>
    public IQueryable<T> ApplyToSafe(ODataQueryOptions<T> odata, ODataPolicy? policy = null, IQueryable<T>? source = null)
        => ApplyODataPolicy(odata, source ?? QueryNoTrack(), policy);

    /// <summary>
    /// One-call OData endpoint: applies OData + returns QueryResult{T} with Items, InlineCount, PageNo.
    /// </summary>
    public async Task<QueryResult<T>> QueryWithODataAsync(ODataQueryOptions<T> odata, ODataPolicy? policy = null, IQueryable<T>? source = null)
    {
        var res = new QueryResult<T>();
        try
        {
            var q = ApplyToSafe(odata, policy, source);
            (res.Results, res.InlineCount, res.PageNo) = await MaterializeWithCountAsync(odata, q);
        }
        catch (Exception ex) { res.FillException(ex); }
        return res;
    }

    // ----- internals -----
    static IQueryable<T> ApplyODataPolicy(ODataQueryOptions<T> opts, IQueryable<T> q, ODataPolicy? policy)
    {
        if (opts == null) return q;
        policy ??= new();

        var v = new ODataValidationSettings();
        if (policy.MaxTop is int mt) v.MaxTop = mt;

        if (!policy.AllowFilter) v.AllowedQueryOptions &= ~AllowedQueryOptions.Filter;
        if (!policy.AllowOrderBy) v.AllowedQueryOptions &= ~AllowedQueryOptions.OrderBy;
        if (!policy.AllowSelect) v.AllowedQueryOptions &= ~AllowedQueryOptions.Select;
        if (!policy.AllowExpand) v.AllowedQueryOptions &= ~AllowedQueryOptions.Expand;
        if (!policy.AllowCount) v.AllowedQueryOptions &= ~AllowedQueryOptions.Count;

        if (policy.AllowedOrderBy is { Length: > 0 })
        {
            v.AllowedOrderByProperties.Clear();
            foreach (var col in policy.AllowedOrderBy) v.AllowedOrderByProperties.Add(col);
        }

        opts.Validate(v);

        var s = new ODataQuerySettings();
        if (policy.ServerPageSize is int p && p > 0) s.PageSize = p;

        if (policy.AllowFilter && opts.Filter != null) q = (IQueryable<T>)opts.Filter.ApplyTo(q, s);
        if (policy.AllowOrderBy && opts.OrderBy != null) q = (IQueryable<T>)opts.OrderBy.ApplyTo(q, s);
        if (opts.Skip != null) q = (IQueryable<T>)opts.Skip.ApplyTo(q, s);
        if (opts.Top != null) q = (IQueryable<T>)opts.Top.ApplyTo(q, s);
        if (policy.AllowSelect && opts.SelectExpand != null) q = (IQueryable<T>)opts.SelectExpand.ApplyTo(q, s);

        return q;
    }

    static async Task<(List<T> Items, long? Count, int PageNo)> MaterializeWithCountAsync(ODataQueryOptions<T> opts, IQueryable<T> q)
    {
        long? count = (opts?.Count?.Value == true) ? await q.LongCountAsync() : null;
        var items = await q.ToListAsync();
        var pageNo = (opts?.Top?.Value is int top && top > 0 && opts?.Skip?.Value is int skip) ? (skip / top) + 1 : 1;
        return (items, count, pageNo);
    }
}

