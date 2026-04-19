using Microsoft.Extensions.Configuration;

namespace BoostX.Model;

// This is our useful bit when injecting the Uow into an application.
// This factory enables us to do it like this:
// builder.Services.AddSingleton<IUowBoostXFactory, UowBoostXFactory>();
public interface IUowBoostXFactory
{
    BoostXUow Create(string? connectionName = null);
}

public sealed class UowBoostXFactory(IConfiguration cfg) : IUowBoostXFactory
{
    public BoostXUow Create(string? connectionName = null) => new BoostXUow(cfg, string.IsNullOrWhiteSpace(connectionName) ? cfg["DefaultAppConnName"]! : connectionName);
}
