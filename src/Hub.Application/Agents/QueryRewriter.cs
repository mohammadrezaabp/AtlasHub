using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hub.Application.Agents;

public sealed class QueryRewriter(IModelClient models)
{
    public async Task<string> Rewrite(string prompt, IReadOnlyList<Message> history, Guid actor, CancellationToken ct)
    {
        var rewritten = await models.Complete("Fast", [new { role = "system", content = "Rewrite the user's question into one standalone search query in English, using conversation context only to resolve references. Do not answer. Preserve original names, acronyms and exact numbers. Do not introduce new facts, names or numbers. Return only the rewritten query, at most 500 characters." },
            new { role = "user", content = JsonSerializer.Serialize(new { history = history.TakeLast(6).Select(m => new { m.Role, text = m.Text[..Math.Min(m.Text.Length, 700)] }), question = prompt }) }], actor.ToString(), 160, ct);
        return IsGrounded(rewritten, prompt, history) ? rewritten.Trim() : prompt;
    }

    public static bool IsGrounded(string rewritten, string prompt, IReadOnlyList<Message> history)
    {
        var original = prompt + string.Join(' ', history.Select(m => m.Text));
        return rewritten.Length is > 0 and <= 700 && Regex.Matches(rewritten, @"\d+(?:[.,]\d+)*").All(m => original.Contains(m.Value));
    }
}
