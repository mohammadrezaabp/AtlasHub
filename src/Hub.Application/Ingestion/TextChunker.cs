using System.Text;
using System.Text.RegularExpressions;
namespace Hub.Application.Ingestion;

public static class TextChunker
{
    public static string Normalize(string s) => s.Replace("\0", "").Normalize(NormalizationForm.FormKC).Replace('ي', 'ی').Replace('ك', 'ک').Replace('\u200c', ' ');
    public static List<Chunk> Split(Guid doc, string text, int page)
    {
        text = Normalize(text).Replace("\r\n", "\n");
        var result = new List<Chunk>();
        var section = page > 0 ? $"Page {page}" : "Overview";
        var buffer = new StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            var value = buffer.ToString().Trim();
            if (value.Length > 15) result.Add(new Chunk { DocumentId = doc, Text = value, Section = section, Page = page, Position = page * 10000 + result.Count });
            buffer.Clear();
        }
        foreach (var line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith('#')) { Flush(); section = line.Trim().TrimStart('#').Trim(); }
            foreach (var part in Regex.Split(line, @"(?<=\s)"))
            {
                if (buffer.Length + part.Length > 1700)
                {
                    var overlap = buffer.ToString();
                    Flush();
                    if (overlap.Length > 200) buffer.Append(overlap[^200..]);
                }
                // Long unbroken tokens are bounded as well.
                for (var offset = 0; offset < part.Length; offset += 1500)
                { buffer.Append(part.Substring(offset, Math.Min(1500, part.Length - offset))); if (buffer.Length >= 1700) Flush(); }
            }
            buffer.AppendLine();
        }
        Flush();
        return result;
    }

}
