using EfCore.Boost;
using EfCore.Boost.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static BoostTest.TestDb.DbTest;

namespace BoostTest.TestDb
{
    public sealed class UOWTestView(IConfiguration cfg, string cfgName) : ReadUowFactory<DbTest>(cfg, cfgName)
    {
        public EfReadRepo<MyTableRefView> MyTableRefViews => new(Ctx, DbType);

        /// <summary>
        /// Example of using the routine helpers with a row-returning routine.
        /// Retrieves records from MyTableRefView filtered by MyId.
        /// This exercises RoutineConvention + SetUpRoutineQuery&lt;T&gt; across providers.
        /// </summary>
        public async Task<List<MyTableRefView>> GetMyTableRefViewByMyIdAsync(long myId)
        {
            var paList = new List<DbParmInfo> { new("@MyId", myId) };
            return await SetUpRoutineQuery<MyTableRefView>("my", "GetMyTableRefViewByMyId", paList).ToListAsync();
        }
        public List<MyTableRefView> GetMyTableRefViewByMyIdSynchronized(long myId)
        {
            var paList = new List<DbParmInfo> { new("@MyId", myId) };
            return [.. SetUpRoutineQuery<MyTableRefView>("my", "GetMyTableRefViewByMyId", paList)];
        }
    }
}
