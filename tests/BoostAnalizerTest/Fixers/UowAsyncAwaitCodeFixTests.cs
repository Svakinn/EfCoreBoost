// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.UowAsyncAwaitAnalyze,
    BoostAnalyzer.Fixers.UowAsyncAwaitCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class UowAsyncAwaitCodeFixTests
    {
        static readonly string[] Methods = [
            "SaveChangesAsync",
            "SaveChangesAndNewAsync",
            "CommitTransactionAsync",
            "ExecSqlScriptAsync",
            "ExecSqlCmdAsync",
            "ExecuteInTransactionAsync",
            "RollbackTransactionAsync",
            "BeginTransactionAsync"
        ];

        public static System.Collections.Generic.IEnumerable<object[]> GetMethods()
        {
            foreach (var m in Methods)
                yield return new object[] { m };
        }

        [TestMethod]
        [DynamicData(nameof(GetMethods))]
        public async Task AsyncDbMethod_NotAwaited_IsConverted_To_Await(string methodName)
        {
            var before = @"
using System;
using System.Threading.Tasks;
class Svc
{
    public Task METHOD() => Task.CompletedTask;
}
class C
{
    private readonly Svc _svc;
    public C(Svc svc) { _svc = svc; }
    public async Task M()
    {
        Console.WriteLine(""before"");
        [|_svc.METHOD()|];
        Console.WriteLine(""after"");
    }
}
";

            var after = @"
using System;
using System.Threading.Tasks;
class Svc
{
    public Task METHOD() => Task.CompletedTask;
}
class C
{
    private readonly Svc _svc;
    public C(Svc svc) { _svc = svc; }
    public async Task M()
    {
        Console.WriteLine(""before"");
        await _svc.METHOD();
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
