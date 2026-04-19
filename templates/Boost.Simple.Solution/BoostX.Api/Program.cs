using Microsoft.AspNetCore.HttpOverrides;
using BoostX.Api.BLL;
using BoostX.Model;

var builder = WebApplication.CreateBuilder(args);

// Inject our UoW-Factory, business logic (IpLogic) and our BackgroundWorker to the DI container
builder.Services.AddSingleton<IUowBoostXFactory, UowBoostXFactory>();
builder.Services.AddScoped<IpLogic>();
builder.Services.AddHostedService<IpBackgroundWorker>();
// Enable IP Forwarding
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddControllers();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
