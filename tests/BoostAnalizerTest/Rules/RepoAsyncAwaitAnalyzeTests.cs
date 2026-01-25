// EFB0005 tests – RepoAsyncAwaitAnalyze
// Async repository methods must be awaited.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<
    BoostAnalyzer.Rules.RepoAsyncAwaitAnalyze>;

namespace BoostAnalyzer.Test.Rules
{
    [TestClass]
    public class RepoAsyncAwaitAnalyzeTests
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

        // EFB0005
        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task RepoAsyncMethod_NotAwaited_InAsyncMethod_ProducesDiagnostic(string methodName)
        {
            var test = @"
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
    public Task<QueryResult<T>> ApplyOdataFilterAsync(object options) => Task.FromResult<QueryResult<T>>(null);
    public Task<bool?> GetBoolScalarAsync(string query, params object[] parameters) => Task.FromResult<bool?>(null);
    public Task<long?> GetLongScalarAsync(string query, params object[] parameters) => Task.FromResult<long?>(null);
    public Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters) => Task.FromResult<decimal?>(null);
    public Task<T> FirstAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(0);
    public Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task<QueryResult<T>> QueryWithODataAsync(ODataQueryOptions<T> odata, ODataPolicy policy = null, IQueryable<T> source = null) => Task.FromResult<QueryResult<T>>(null);
}

class QueryResult<T> { }
class QueryResult<T> { }
class ODataQueryOptions<T> { }
class ODataPolicy { }

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
        _uow.Customers.[|METHOD|](null);
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // EFB0005
        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task RepoAsyncMethod_Awaited_InAsyncMethod_ProducesNoDiagnostic(string methodName)
        {
            var test = @"
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
    public Task<QueryResult<T>> ApplyOdataFilterAsync(object options) => Task.FromResult<QueryResult<T>>(null);
    public Task<bool?> GetBoolScalarAsync(string query, params object[] parameters) => Task.FromResult<bool?>(null);
    public Task<long?> GetLongScalarAsync(string query, params object[] parameters) => Task.FromResult<long?>(null);
    public Task<decimal?> GetDecimalScalarAsync(string query, params object[] parameters) => Task.FromResult<decimal?>(null);
    public Task<T> FirstAsync(Expression<Func<T, bool>> filter) => Task.FromResult(default(T));
    public Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate) => Task.FromResult(0);
    public Task BulkDeleteByIdsAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task BulkInsertAsync(List<T> items, bool includeIdentityValues = false, CancellationToken cancellationToken = default(CancellationToken)) => Task.CompletedTask;
    public Task<QueryResult<T>> QueryWithODataAsync(ODataQueryOptions<T> odata, ODataPolicy policy = null, IQueryable<T> source = null) => Task.FromResult<QueryResult<T>>(null);
}

class QueryResult<T> { }
class QueryResult<T> { }
class ODataQueryOptions<T> { }
class ODataPolicy { }

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
        await _uow.Customers.METHOD(null);
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        public static System.Collections.Generic.IEnumerable<object[]> GetMethods()
        {
            foreach (var m in Methods)
                yield return new object[] { m };
        }
    }
}
