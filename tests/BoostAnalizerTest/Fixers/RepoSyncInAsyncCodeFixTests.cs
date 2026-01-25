// EFB0006 CodeFix tests – RepoSyncInAsyncCodeFixProvider
// Synchronous repo methods in async methods should be replaced with async+await.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpCodeFixVerifier<
    BoostAnalyzer.Rules.RepoSyncInAsyncAnalyze,
    BoostAnalyzer.Fixers.RepoSyncInAsyncCodeFixProvider>;

namespace BoostAnalyzer.Test.Fixers
{
    [TestClass]
    public class RepoSyncInAsyncCodeFixTests
    {
        static readonly string[] Methods = [
            "ByKeySynchronized",
            "ByKeyNoTrackSynchronized",
            "FirstNoTrackSynchronized",
            "QueryNoTrackSynchronized",
            "AnyNoTrackSynchronized",
            "CountSynchronized",
            "ApplyOdataFilterSynchronized",
            "GetBoolScalarSynchronized",
            "GetLongScalarSynchronized",
            "GetDecimalScalarSynchronized",
            "FirstSynchronized",
            "DeleteWhereSynchronized",
            "BulkDeleteByIdsSynchronized",
            "BulkInsertSynchronized",
            "QueryWithODataSynchronized"
        ];

        public static System.Collections.Generic.IEnumerable<object[]> GetMethods()
        {
            foreach (var m in Methods)
                yield return new object[] { m };
        }

        // EFB0006
        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task SyncRepoMethod_InAsyncMethod_IsConverted_To_AsyncAwait(string syncName)
        {
            var asyncName = syncName.Replace("Synchronized", "Async");

            var before = @"
using System.Threading.Tasks;
class Repo<T> {
    public void SYNC() { }
    public Task ASYNC() => Task.CompletedTask;
}
class Uow { public Repo<int> Customers { get; } = new Repo<int>(); }
class C {
    private readonly Uow _uow;
    public C(Uow uow){_uow=uow;}
    public async Task M(){
        _uow.Customers.[|SYNC|]();
    }
}
";

            var after = @"
using System.Threading.Tasks;
class Repo<T> {
    public void SYNC() { }
    public Task ASYNC() => Task.CompletedTask;
}
class Uow { public Repo<int> Customers { get; } = new Repo<int>(); }
class C {
    private readonly Uow _uow;
    public C(Uow uow){_uow=uow;}
    public async Task M(){
        await _uow.Customers.ASYNC();
    }
}
";

            // IMPORTANT: replace ASYNC first, then SYNC so "ASYNC" doesn't get mangled
            before = before.Replace("ASYNC", asyncName).Replace("SYNC", syncName);
            after = after.Replace("ASYNC", asyncName).Replace("SYNC", syncName);

            await VerifyCS.VerifyCodeFixAsync(before, after);
        }
    }
}
