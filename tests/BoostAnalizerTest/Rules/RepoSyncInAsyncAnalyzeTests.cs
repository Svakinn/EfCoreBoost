using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

using VerifyCS = BoostAnalyzer.Test.CSharpAnalyzerVerifier<
    BoostAnalyzer.Rules.RepoSyncInAsyncAnalyze>;

namespace BoostAnalyzer.Test.Rules
{
    [TestClass]
    public class RepoSyncInAsyncAnalyzeTests
    {
        static readonly string[] Methods = {
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
        };

        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task SyncRepoMethod_InAsyncMethod_ProducesDiagnostic(string methodName)
        {
            var test = @"
using System.Threading.Tasks;
class Repo<T> { public void METHOD(){} }
class Uow { public Repo<int> Customers { get; } = new Repo<int>(); }
class C {
    private readonly Uow _uow;
    public C(Uow uow){_uow=uow;}
    public async Task M(){
        _uow.Customers.[|METHOD|]();
    }
}";
            test = test.Replace("METHOD", methodName);
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetMethods), DynamicDataSourceType.Method)]
        public async Task SyncRepoMethod_InSyncMethod_NoDiagnostic(string methodName)
        {
            var test = @"
using System.Threading.Tasks;
class Repo<T> { public void METHOD(){} }
class Uow { public Repo<int> Customers { get; } = new Repo<int>(); }
class C {
    private readonly Uow _uow;
    public C(Uow uow){_uow=uow;}
    public void M(){
        _uow.Customers.METHOD();
    }
}";
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
