// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// EFB0005 CodeFix tests – RepoAsyncAwaitCodeFixProvider
// Ensures async repository calls on _uow.{Repo}.METHOD(...) are wrapped in await.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.RepoAsyncAwaitAnalyze,
    BoostAnalyzer.Fixers.RepoAsyncAwaitCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class RepoAsyncAwaitCodeFixTests
    {

        static readonly string[] Methods = [
            "ByKeyAsync",
            "ByKeyNoTrackAsync",
            "FirstNoTrackAsync",
            "QueryNoTrackAsync",
            "AnyNoTrackAsync",
            "CountAsync",
            "ApplyOdataFilterAsync",
            "GetBoolScalarAsync",
            "GetLongScalarAsync",
            "GetDecimalScalarAsync",
            "FirstAsync",
            "DeleteWhereAsync",
            "BulkDeleteByIdsAsync",
            "BulkInsertAsync",
            "QueryWithODataAsync"
        ];

        public static System.Collections.Generic.IEnumerable<object[]> GetMethods()
        {
            foreach (var m in Methods)
                yield return new object[] { m };
        }

        // EFB0005
        [TestMethod]
        [DynamicData(nameof(GetMethods))]
        public async Task RepoAsyncMethod_NotAwaited_IsConverted_To_Await(string methodName)
        {
            var before = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;

class Repo<T>
{
    public Task<T> ByKeyAsync(params object[] key) => Task.FromResult(default(T));
    public Task<T> ByKeyNoTrackAsync(params object[] key) => Task.FromResult(default(T));
    public Task<T> FirstNoTrackAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>> filter = null) => Task.FromResult(new List<T>());
    public Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter) => Task.FromResult(false);
    public Task<long> CountAsync(Expression<Func<T, bool>> filter = null) => Task.FromResult(0L);

    // Simplified for test: 1-arg version that matches METHOD(null)
    public Task<QueryResult<T>> ApplyOdataFilterAsync(object options) => Task.FromResult<QueryResult<T>>(null);

    public Task<bool?> GetBoolScalarAsync(string query, params object[] parameters) => Task.FromResult<bool?>(null);
    public Task<long?> GetLongScalarAsync(string query, params object[] parameters) => Task.FromResult<long?>(null);
    public Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters) => Task.FromResult<decimal?>(null);
    public Task<T> FirstAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(0);
    public Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;

    // New simple stub for QueryWithODataAsync so METHOD(null) compiles
    public Task<QueryResult<T>> QueryWithODataAsync(object options) => Task.FromResult<QueryResult<T>>(null);
}

class QueryResult<T> { }

class Uow
{
    public Repo<int> Customers { get; } = new Repo<int>();
}

class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }

    public async Task M()
    {
        Console.WriteLine(""before"");
        _uow.Customers.[|METHOD|](null);
        Console.WriteLine(""after"");
    }
}
";
            var after = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;

class Repo<T>
{
    public Task<T> ByKeyAsync(params object[] key) => Task.FromResult(default(T));
    public Task<T> ByKeyNoTrackAsync(params object[] key) => Task.FromResult(default(T));
    public Task<T> FirstNoTrackAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<List<T>> QueryNoTrackAsync(Expression<Func<T, bool>> filter = null) => Task.FromResult(new List<T>());
    public Task<bool> AnyNoTrackAsync(Expression<Func<T, bool>> filter) => Task.FromResult(false);
    public Task<long> CountAsync(Expression<Func<T, bool>> filter = null) => Task.FromResult(0L);

    // Simplified for test: 1-arg version that matches METHOD(null)
    public Task<QueryResult<T>> ApplyOdataFilterAsync(object options) => Task.FromResult<QueryResult<T>>(null);

    public Task<bool?> GetBoolScalarAsync(string query, params object[] parameters) => Task.FromResult<bool?>(null);
    public Task<long?> GetLongScalarAsync(string query, params object[] parameters) => Task.FromResult<long?>(null);
    public Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters) => Task.FromResult<decimal?>(null);
    public Task<T> FirstAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(0);
    public Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;

    // New simple stub for QueryWithODataAsync so METHOD(null) compiles
    public Task<QueryResult<T>> QueryWithODataAsync(object options) => Task.FromResult<QueryResult<T>>(null);
}

class QueryResult<T> { }

class Uow
{
    public Repo<int> Customers { get; } = new Repo<int>();
}

class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }

    public async Task M()
    {
        Console.WriteLine(""before"");
        await _uow.Customers.METHOD(null);
        Console.WriteLine(""after"");
    }
}
";
            before = before.Replace("METHOD", methodName);
            after = after.Replace("METHOD", methodName);

            await VerifyCS.VerifyCodeFixAsync(before, after);
        }
    }
}
