// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.SyncLinqInAsyncContextAnalyzer,
    BoostAnalyzer.Fixers.SyncLinqInAsyncContextCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class SyncLinqInAsyncContextCodeFixTests
    {
        // EFB0003
        [TestMethod]
        public async Task ToList_InAsyncMethod_IsConverted_To_Await_ToListAsync()
        {
            var test =
@"using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) => Task.FromResult(new List<int>());
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
            var fixedCode =
@"using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) => Task.FromResult(new List<int>());
}
class C
{
    private readonly FakeRepo _repo;
    public C(FakeRepo repo) { _repo = repo; }
    public async Task M()
    {
        var list = await _repo.Query().ToListAsync();
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(test, fixedCode);
        }

        // EFB0003
        [TestMethod]
        public async Task First_InAsyncMethod_IsConverted_To_Await_FirstAsync()
        {
            var test =
@"using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<int> FirstAsync(this IQueryable<int> source) => Task.FromResult(0);
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
            var fixedCode =
@"using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<int> FirstAsync(this IQueryable<int> source) => Task.FromResult(0);
}
class C
{
    private readonly FakeRepo _repo;
    public C(FakeRepo repo) { _repo = repo; }
    public async Task M()
    {
        var value = await _repo.Query().FirstAsync();
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(test, fixedCode);
        }
    }
}
