using BoostX.Api.BLL;
using BoostX.Api.DTO;
using BoostX.Model;
using EfCore.Boost.DbRepo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;

namespace BoostX.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class IpInfoController(IpLogic ipLogic) : ControllerBase
{
    private readonly IpLogic _ipLogic = ipLogic;

    [HttpGet("random")]
    public async Task<IpDto> GetRandomIp()
    {
        return await _ipLogic.GetRandomIp();
    }

    [HttpGet("ensure")]
    public async Task<IpDto> EnsureIp()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        return await _ipLogic.EnsureIp(ip);
    }

    [HttpGet("list")]
    public async Task<QueryResult<BoostCTX.IpInfoView>> ListIps(ODataQueryOptions<BoostCTX.IpInfoView> options, CancellationToken ct)
    {
        return await _ipLogic.ListIps(options, ct);
    }
}
