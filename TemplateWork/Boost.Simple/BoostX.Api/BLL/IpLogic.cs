using BoostX.Api.DTO;
using BoostX.Model;
using EfCore.Boost.DbRepo;
using Microsoft.AspNetCore.OData.Query;

namespace BoostX.Api.BLL;

public sealed class IpLogic(IUowBoostXFactory uowBoostXFactory)  //Factory injected from DI
{
    private readonly IUowBoostXFactory _uowBoostXFactory = uowBoostXFactory;
    private  BoostXUow? _uowBoostX;

    private BoostXUow UoW => _uowBoostX ??= _uowBoostXFactory.Create(); //Lazy init of the UoW

    /// <summary>
    /// Saves random IP to a database if not already there
    /// </summary>
    /// <param name="ip"></param>
    /// <returns>Returns the info we got about the Ip</returns>
    public async Task<IpDto> GetRandomIp()
    {
        var ret = new DTO.IpDto();
        var ip = DemoIpCatalog.GetRandomIp();
        var ipId = await UoW.GetIpId(ip);
        if (ipId == null) return new DTO.IpDto();
        var ipRow = await UoW.IpInfos.RowByIdUnTrackedAsync((long)ipId);
        if (ipRow == null) return new DTO.IpDto();
        ret.Ip = ip;
        ret.HostName = ipRow.HostName ?? "";
        ret.IsReady = ipRow.Processed;
        ret.LastChanged = ipRow.LastChangedUtc;
        return ret;
    }

    /// <summary>
    /// Saves your IP to a database if not already there
    /// </summary>
    /// <param name="ip"></param>
    /// <returns>Returns the info we got about the Ip</returns>
    public async Task<IpDto> EnsureIp(string ip)
    {
        var ret = new DTO.IpDto();
        var ipId = await UoW.GetIpId(ip);
        if (ipId == null) return new DTO.IpDto();
        var ipRow = await UoW.IpInfos.RowByIdUnTrackedAsync((long)ipId);
        if (ipRow == null) return new DTO.IpDto();
        ret.Ip = ip;
        ret.HostName = ipRow.HostName ?? "";
        ret.IsReady = ipRow.Processed;
        ret.LastChanged = ipRow.LastChangedUtc;
        return ret;
    }

    /// <summary>
    /// Demonstrates how easy it is to set up Odata filtered Queries
    /// The client sets upt the query (but we handle the boundaries for that query)
    /// </summary>
    /// <param name="options"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<QueryResult<BoostCTX.IpInfoView>> ListIps(ODataQueryOptions<BoostCTX.IpInfoView> options, CancellationToken ct)
    {
        //Example base safe query (here we exclude the seeded Ip number with negative ID from the model (HasData))
        var baseQuery = UoW.IpInfoViews.QueryUnTracked().Where((tt => tt.Id > 0));
        //Client determines filters, paging, sorting etc., we don't care except by setting optional boundaries via ODataPolicy
        // policy = new ODataPolicy() { MaxExpansionDepth = 3, MaxTop = 100, AllowExpand = false}; //etc
        return await UoW.IpInfoViews.FilterODataAsync(baseQuery, options, null, false, ct); //, policy, false, ct);
    }
}
