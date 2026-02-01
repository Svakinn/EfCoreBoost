// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<BoostAnalyzer.Rules.BlockingEfQueryTaskWaitAnalyzer>;

namespace BoostAnalyzer.Test.Rules
{
    [TestClass]
    public class BlockingEfQueryTaskWaitAnalyzerTests
    {
        [TestMethod]
        public async Task ToListAsync_Result_InAsyncMethod_ProducesDiagnostic()
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
        var list = _repo.Query().ToListAsync().[|Result|];
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0004 test
        [TestMethod]
        public async Task ToListAsync_Wait_InAsyncMethod_ProducesDiagnostic()
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
        _repo.Query().ToListAsync().[|Wait|]();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0004 test
        [TestMethod]
        public async Task ToListAsync_GetAwaiter_GetResult_InAsyncMethod_ProducesDiagnostic()
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
        var list = _repo.Query().ToListAsync().GetAwaiter().[|GetResult|]();
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //EFB0004 test
        [TestMethod]
        public async Task ToListAsync_Result_InSyncMethod_ProducesNoDiagnostic()
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
    public void M()
    {
        var list = _repo.Query().ToListAsync().Result;
    }
}
";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
