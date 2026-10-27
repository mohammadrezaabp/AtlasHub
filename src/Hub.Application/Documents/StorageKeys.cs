using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace Hub.Application.Documents;

public static class StorageKeys
{
    public static string Team(string name)
    {
        var normalized = name.Trim().Normalize(NormalizationForm.FormKC);
        var slug = Regex.Replace(normalized.ToLowerInvariant(), @"[^\p{L}\p{Nd}]+", "-").Trim('-');
        if (slug.Length == 0) slug = "team";
        return slug[..Math.Min(48, slug.Length)] + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..8].ToLowerInvariant();
    }
    public static string FileName(string name)
    {
        var leaf = name.Replace('\\', '/').Split('/').Last();
        var safe = Regex.Replace(leaf, @"[^\p{L}\p{Nd}._-]", "_").Trim('.');
        return safe.Length == 0 ? "file" : safe.Length <= 160 ? safe : safe[..140] + Path.GetExtension(safe)[..Math.Min(15, Path.GetExtension(safe).Length)];
    }
    public static string Document(Document d) => $"teams/{Team(d.Department)}/documents/{(d.Visibility switch { DocumentVisibility.Organization => "organization", DocumentVisibility.Team => "team", _ => "private/" + d.OwnerId.ToString("N") })}/{d.Id:N}/{FileName(d.FileName)}";
    public static string Voice(HubUser user, Guid id, string kind, string extension) => $"teams/{Team(user.Department)}/voices/private/{user.Id:N}/{kind}/{id:N}{extension}";
}
