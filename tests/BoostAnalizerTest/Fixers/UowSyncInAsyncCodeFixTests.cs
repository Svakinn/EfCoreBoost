// Copyright © 2026  Sveinn S. Erlendsson
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// EFB0002 CodeFix tests – UowSyncInAsyncCodeFixProvider
// Verifies that synchronous UOW methods in async methods are rewritten to async+await.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.UowSyncInAsyncAnalyze,
    BoostAnalyzer.Fixers.UowSyncInAsyncCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class UowSyncInAsyncCodeFixTests
    {
        // EFB0002
        [TestMethod]
        [DataRow("SaveChangesSynchronized", "SaveChangesAsync")]
        [DataRow("SaveChangesAndNewSynchronized", "SaveChangesAndNewAsync")]
        [DataRow("CommitTransactionSynchronized", "CommitTransactionAsync")]
        [DataRow("ExecSqlScriptSynchronized", "ExecSqlScriptAsync")]
        [DataRow("ExecSqlCmdSynchronized", "ExecSqlCmdAsync")]
        [DataRow("ExecuteInTransactionSynchronized", "ExecuteInTransactionAsync")]
        [DataRow("RollbackTransactionSynchronized", "RollbackTransactionAsync")]
        [DataRow("BeginTransactionSynchronized", "BeginTransactionAsync")]
        public async Task SyncUowMethod_InAsyncMethod_IsConverted_To_Await_Async(string syncName, string asyncName)
        {
            var before = @"
using System;
using System.Threading.Tasks;
class Uow
{
    public void SYNC() { }
    public Task ASYNC() => Task.CompletedTask;
}
class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }
    public async Task M()
    {
        Console.WriteLine(""before"");
        _uow.[|SYNC|]();
        Console.WriteLine(""after"");
    }
}
";

            var after = @"
using System;
using System.Threading.Tasks;
class Uow
{
    public void SYNC() { }
    public Task ASYNC() => Task.CompletedTask;
}
class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }
    public async Task M()
    {
        Console.WriteLine(""before"");
        await _uow.ASYNC();
        Console.WriteLine(""after"");
    }
}
";

            // IMPORTANT: replace ASYNC first, then SYNC
            before = before.Replace("ASYNC", asyncName).Replace("SYNC", syncName);
            after = after.Replace("ASYNC", asyncName).Replace("SYNC", syncName);

            await VerifyCS.VerifyCodeFixAsync(before, after);
        }
    }
}
