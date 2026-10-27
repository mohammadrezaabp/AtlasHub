using System.Text.RegularExpressions;
namespace Hub.Application.Search;

public static partial class QueryTokenizer
{
    public static string[] Tokens(string text) => Words().Matches(TextChunker.Normalize(text).ToLowerInvariant())
        .Select(m => m.Value).Where(x => x.Length > 1).ToArray();
    [GeneratedRegex(@"[\p{L}\p{N}_-]+")]
    private static partial Regex Words();

}
