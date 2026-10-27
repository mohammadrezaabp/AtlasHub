using System.Text;

namespace Hub.Application.Documents;

public static class DocumentFormats
{
    public static readonly string[] Extensions = [".md", ".txt", ".pdf", ".csv", ".png", ".jpg", ".jpeg", ".webp"];
    public static bool IsImage(string fileName) => new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(fileName).ToLowerInvariant());
    public static bool Matches(string extension, byte[] bytes) => extension switch
    {
        ".pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
        ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }),
        ".jpg" or ".jpeg" => bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 }),
        ".webp" => bytes.Length > 12 && bytes.AsSpan().StartsWith("RIFF"u8) && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
        _ => !bytes.Contains((byte)0)
    };
}
