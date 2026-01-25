// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.BlockingEfQueryTaskWaitAnalyzer,
    BoostAnalyzer.Fixers.BlockingEfQueryTaskWaitCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class BlockingEfQueryTaskWaitCodeFixTests
    {
        // EFB0004
        [TestMethod]
        public async Task ToListAsync_Result_InAsyncMethod_IsConverted_To_Await()
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
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) => Task.FromResult(new List<int>());
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
            var fixedCode = @"
using System.Collections.Generic;
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

        // EFB0004
        [TestMethod]
        public async Task ToListAsync_Wait_InAsyncMethod_IsConverted_To_Await()
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
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) => Task.FromResult(new List<int>());
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
            var fixedCode = @"
using System.Collections.Generic;
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
        await _repo.Query().ToListAsync();
    }
}
";
            await VerifyCS.VerifyCodeFixAsync(test, fixedCode);
        }

        // EFB0004
        [TestMethod]
        public async Task ToListAsync_GetAwaiter_GetResult_InAsyncMethod_IsConverted_To_Await()
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
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) =>  Task.FromResult(new List<int>());
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
            var fixedCode = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
class FakeRepo
{
    public IQueryable<int> Query() => new List<int>().AsQueryable();
}
static class QueryExtensions
{
    public static Task<List<int>> ToListAsync(this IQueryable<int> source) =>  Task.FromResult(new List<int>());
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
    }
}
