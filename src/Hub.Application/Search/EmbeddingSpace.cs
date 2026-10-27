using System.Security.Cryptography;
using System.Text;
namespace Hub.Application.Search;

public static class EmbeddingSpace
{
    public static string CollectionName(string model) => "knowledge_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(model)))[..16].ToLowerInvariant();

}
