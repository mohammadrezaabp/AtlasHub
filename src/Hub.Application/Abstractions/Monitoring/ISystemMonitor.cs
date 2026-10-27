namespace Hub.Application.Abstractions.Monitoring;

public interface ISystemMonitor
{
    Task<SystemStatus> Status(CancellationToken ct);
}
