// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<BoostAnalyzer.Rules.UowAsyncAwaitAnalyze>;

///EFB0001 test
namespace BoostAnalyzer.Test.Rules
{

    [TestClass]
    public class UowAsyncAwaitAnalyzerTests
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

        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task AsyncDbMethod_NotAwaited_ProducesDiagnostic(string methodName)
        {
            var test = @"
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
        [|_svc.METHOD()|];
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task AsyncDbMethod_Awaited_ProducesNoDiagnostic(string methodName)
        {
            var test = @"
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
        await _svc.METHOD();
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
