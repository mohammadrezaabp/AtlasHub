using Xunit;

namespace Hub.Tests.Application;

public sealed class QueryRewriteTests
{
    [Theory]
    [InlineData("What was revenue in 2025?", "2025 revenue", true)]
    [InlineData("What was revenue in 2025?", "2026 revenue", false)]
    [InlineData("Explain the policy", "", false)]
    public void Rewrites_cannot_introduce_new_numbers(string prompt, string rewritten, bool expected) =>
        Assert.Equal(expected, QueryRewriter.IsGrounded(rewritten, prompt, []));

    [Fact]
    public void Conversation_numbers_can_be_used_to_resolve_a_followup() =>
        Assert.True(QueryRewriter.IsGrounded("2025 revenue", "What about revenue?", [new Message { Text = "The report is from 2025." }]));
}
