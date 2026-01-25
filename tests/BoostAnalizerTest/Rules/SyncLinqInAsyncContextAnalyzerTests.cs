// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<BoostAnalyzer.Rules.SyncLinqInAsyncContextAnalyzer>;

namespace BoostAnalyzer.Test.Rules
{
    [TestClass]
    public class SyncLinqInAsyncContextAnalyzerTests
    {
        #region EFB0003

        //EFB0003 test

        [TestMethod]
        public async Task ToList_OnIQueryable_InAsyncMethod_ProducesDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) =>
        Task.FromResult(new List<int>());
}
class C
{
    private readonly FakeRepo _repo;
    public C(FakeRepo repo) { _repo = repo; }
    public async Task M()
    {
        var list = _repo.Query().[|ToList|]();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0003 test
        [TestMethod]
        public async Task First_OnIQueryable_InAsyncMethod_ProducesDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}

static class QueryExtensions
{
    public static Task<int> FirstAsync(this IQueryable<int> source) =>
        Task.FromResult(0);
}
class C
{
    private readonly FakeRepo _repo;
    public C(FakeRepo repo) { _repo = repo; }
    public async Task M()
    {
        var value = _repo.Query().[|First|]();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0003 test
        [TestMethod]
        public async Task ToList_OnIQueryable_InSyncMethod_ProducesNoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
class C
{
    private readonly FakeRepo _repo;
    public C(FakeRepo repo) { _repo = repo; }
    public void M()
    {
        var list = _repo.Query().ToList();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0003 test
        [TestMethod]
        public async Task ToList_OnIEnumerable_InAsyncMethod_ProducesNoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class C
{
    public async Task M()
    {
        var data = new List<int> { 1, 2, 3 };
        // data is IEnumerable<int>, not IQueryable<int> → analyzer should ignore
        var list = data.ToList();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }

    #endregion

}
