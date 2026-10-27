using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Hub.Infrastructure.Security;

public static class InternalRequest
{
    public static bool IsAuthorized(string? header, IConfiguration config)
    {
        var expected = config["Security:InternalToken"] ?? "";
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
