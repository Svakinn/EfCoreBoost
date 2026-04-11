using EfCore.Boost.DbRepo;
using EfCore.Boost.UOW;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using static BoostX.Model.BoostCTX;

namespace BoostX.Model;

/// <summary>
/// Unit of Work for the BoostX application, providing access to repositories and routines.
/// Inherits from UowFactory to handle DbContext creation and database type detection.
/// </summary>
public class BoostXUow(IConfiguration cfg, string cfgName) : UowFactory<BoostCTX>(cfg, cfgName)
{
    #region dbsets

    /// <summary>
    /// Gets the repository for IpInfo entities.
    /// Uses EfLongIdRepo for optimized access by long ID.
    /// </summary>
    public EfLongIdRepo<IpInfo> IpInfos => new(Ctx, DbType);

    /// <summary>
    /// Gets the read-only repository for IpInfoView.
    /// </summary>
    public EfReadRepo<IpInfoView> IpInfoViews => new(Ctx, DbType); //Or even EfLongIdReadRepo<IpInfoView>

    #endregion

    #region SP

    /// <summary>
    /// Calls the 'GetIpId' stored procedure/function to retrieve the ID for a given IP number.
    /// </summary>
    /// <param name="ipNo">The IP number to look up.</param>
    /// <returns>The ID of the IP record, or null if not found.</returns>
    public async Task<long?> GetIpId(string ipNo)
    {
        return await this.RunRoutineLongAsync(BoostCTX.DefaultSchemaName, "GetIpId", [new DbParmInfo("@IpNo", ipNo)]);
    }

    /// <summary>
    /// Calls the 'GetIpViewByIpId' stored procedure/function to retrieve an IpInfoView by its ID.
    /// </summary>
    /// <param name="ipId">The ID of the IP record.</param>
    /// <returns>The IpInfoView if found; otherwise, null.</returns>
    public async Task<IpInfoView?> GetIpInfoViewByIdAsync(long ipId)
    {
        return await SetUpRoutineQuery<IpInfoView>(BoostCTX.DefaultSchemaName, "GetIpViewByIpId", [new DbParmInfo("@IpId", ipId)]).FirstOrDefaultAsync();
    }
    #endregion
}
