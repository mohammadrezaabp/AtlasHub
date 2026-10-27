using System.Diagnostics;

namespace Hub.Application.Common;

public static class HubTelemetry
{
    public static readonly ActivitySource Activity = new("AtlasHub");
}
