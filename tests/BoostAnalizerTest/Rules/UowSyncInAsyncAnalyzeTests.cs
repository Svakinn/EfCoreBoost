// EFB0002 tests – UowSyncInAsyncAnalyze

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<
    BoostAnalyzer.Rules.UowSyncInAsyncAnalyze>;

namespace BoostAnalyzer.Test.Rules
{
    [TestClass]
    public class UowSyncInAsyncAnalyzeTests
    {
        static readonly string[] Methods = [
            "SaveChangesSynchronized",
            "SaveChangesAndNewSynchronized",
            "CommitTransactionSynchronized",
            "ExecSqlScriptSynchronized",
            "ExecSqlCmdSynchronized",
            "ExecuteInTransactionSynchronized",
            "RollbackTransactionSynchronized",
            "BeginTransactionSynchronized"
        ];

        public static System.Collections.Generic.IEnumerable<object[]> GetMethods()
        {
            foreach (var m in Methods)
                yield return new object[] { m };
        }

        // EFB0002
        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task SyncUowMethod_InAsyncMethod_ProducesDiagnostic(string methodName)
        {
            var test = @"
using System.Threading.Tasks;
class Uow
{
    public void METHOD() { }
}
class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }
    public async Task M()
    {
        _uow.[|METHOD|]();
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // EFB0002
        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task SyncUowMethod_InSyncMethod_ProducesNoDiagnostic(string methodName)
        {
            var test = @"
using System.Threading.Tasks;
class Uow
{
    public void METHOD() { }
}
class C
{
    private readonly Uow _uow;
    public C(Uow uow) { _uow = uow; }
    public void M()
    {
        _uow.METHOD();
    }
}
";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
