using EfCore.Boost;
using EfCore.Boost.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BoostTest.TestDb.DbTest;

namespace BoostTest.TestDb
{
    public partial class UOWTestDb(IConfiguration cfg, string cfgName) : DbUow<DbTest>(() => SecureContextFactory.CreateDbContext<DbTest>(cfg, cfgName))
    {

        #region tabular data

        public EfRepo<MyTable> MyTables => new(Ctx, DbType);
        public EfRepo<MyTableRef> MyTableRefs => new(Ctx, DbType);

        //Views are read-only, so we use a read-only repository for those
        public EfReadRepo<MyTableRefView> MyTableRefViews => new(Ctx, DbType);

        #endregion

        #region using the uow to call custom objects
        /// <summary>
        /// Calling Stored Procedure cannot be 100% streamlined between database flavors, format and naming is different.
        /// Postgres does not support procedures and MySQL not shcemas )
        /// Ef-Boost provides some helpers, performing the Ado-level-commands and retreiving data.
        /// This method retrieves and reserves sequence numbers to list.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<List<long>> GetNextSequenceIds(int count)
        {
            var paList = new List<DbParmInfo> { new("@IdCount", count) };
            return await this.RunRoutineLongListAsync("my", "ReserveMyIds", paList);
        }

        public List<long> GetNextSequenceIdsSynchronized(int count)
        {
            var paList = new List<DbParmInfo> { new("@IdCount", count) };
            return this.RunRoutineLongListSynchronized("my", "ReserveMyIds", paList);
        }

        /// <summary>
        /// Scalar routine example.   
        /// </summary>
        /// <param name="myId"></param>
        /// <returns></returns>
        public async Task<long?> GetMaxIdByChanger(string changer)
        {
            var paList = new List<DbParmInfo> { new("@Changer", changer) };
            return await RunRoutineLongAsync("my", "GetMaxIdByChanger", paList);
        }

        //Normally we would only need async func, but we provide this one for testing purposes
        public long? GetMaxIdByChangerSynchronized(string changer)
        {
            var paList = new List<DbParmInfo> { new("@Changer", changer) };
            return RunRoutineLongSynchronized("my", "GetMaxIdByChanger", paList);
        }

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
            return SetUpRoutineQuery<MyTableRefView>("my", "GetMyTableRefViewByMyId", paList).ToList();
        }
        #endregion

    }
}
