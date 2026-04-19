using BoostX.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BoostX.Api.BLL;

public class IpBackgroundWorker(IUowBoostXFactory uowFactory, ILogger<IpBackgroundWorker> logger) : BackgroundService
{
    private readonly IUowBoostXFactory _uowFactory = uowFactory;
    private readonly ILogger<IpBackgroundWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("IpBackgroundWorker running at: {time}", DateTimeOffset.Now);
                await ProcessUnprocessedIps(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred executing IpBackgroundWorker.");
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task ProcessUnprocessedIps(CancellationToken ct)
    {
        using var uow = _uowFactory.Create();
        // Find records aren't processed
        var unprocessed = await uow.IpInfos.QueryTracked().Where(x => !x.Processed).ToListAsync(ct);
        foreach (var ipInfo in unprocessed)
        {
            if (ct.IsCancellationRequested) break;
            _logger.LogInformation("Processing IP: {IpNo}", ipInfo.IpNo);
            // Simple "hostname" update logic - just for demo
            ipInfo.HostName = $"host-{ipInfo.IpNo.Replace(".", "-")}.example.com";
            ipInfo.Processed = true;
        }
        await uow.SaveChangesAsync(ct);
    }
}
