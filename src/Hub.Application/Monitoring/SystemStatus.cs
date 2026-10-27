namespace Hub.Application.Monitoring;

public sealed record SystemStatus(IReadOnlyList<ServiceHealth> Services, string Deployment, string Gpu, string Inference, string Architecture);
