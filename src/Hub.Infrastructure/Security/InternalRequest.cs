using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.Security;

public static class InternalRequest
{
    public static bool IsAuthorized(string? header, IConfiguration config) => Matches(header, config["Security:InternalToken"]);

    public static bool IsMetricsScrape(string? authorization, IConfiguration config) =>
        authorization?.StartsWith("Bearer ", StringComparison.Ordinal) == true && Matches(authorization["Bearer ".Length..], config["Security:MetricsToken"]);

    private static bool Matches(string? header, string? expected)
    {
        expected ??= "";
        return expected.Length >= 32 && header != null && CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(header)), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    }

    public static HttpRequestMessage Create(HttpMethod method, string url, IConfiguration config, string actor = "system")
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Hub-Token", config["Security:InternalToken"]);
        request.Headers.Add("X-Hub-Actor", actor);
        return request;
    }
}
