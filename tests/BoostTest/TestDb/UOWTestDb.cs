using EfCore.Boost;
using EfCore.Boost.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static BoostTest.TestDb.DbTest;

namespace BoostTest.TestDb
{
    //DbTest
    public sealed class UOWTestDb(IConfiguration cfg, string cfgName) : UowFactory<DbTest>(cfg, cfgName)
    {

        #region tabular data

        public EfRepo<MyTable> MyTables => new(Ctx, DbType);
        public EfRepo<MyTableRef> MyTableRefs => new(Ctx, DbType);

        // Views are read-only and we can have readonly repos within RW-uow like here below
        // However we move this to the UOWTestView.cs to demonstrate Unit of Work that is entirely readonly - cannot be used to update any data in the underlying DbContext.
        //public EfReadRepo<MyTableRefView> MyTableRefViews => new(Ctx, DbType);

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

        #endregion

    }
}
