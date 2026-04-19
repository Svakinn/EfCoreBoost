namespace BoostX.Api.DTO;

public class IpDto
{
    public string Ip { get; set; } = "";
    public string HostName { get; set; } = "";
    public bool IsReady { get; set; }
    public DateTimeOffset LastChanged { get; set; } = DateTimeOffset.UtcNow;
}
