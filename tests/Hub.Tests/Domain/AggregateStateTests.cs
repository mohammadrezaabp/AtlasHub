using Hub.Domain.Common;
using Xunit;

namespace Hub.Tests.Domain;

public sealed class AggregateStateTests
{
    [Fact]
    public void A_chat_accepts_one_active_run_and_cannot_be_deleted_while_active()
    {
        var chat = Conversation.Create(Guid.NewGuid(), " Chat ");
        Assert.Equal("Chat", chat.Title);
        chat.BeginRun(Guid.NewGuid());
        Assert.Throws<DomainRuleException>(() => chat.BeginRun(Guid.NewGuid()));
        Assert.Throws<DomainRuleException>(chat.EnsureCanDelete);
        Assert.Throws<DomainRuleException>(() => chat.Rename(" "));
    }

    [Fact]
    public void Approval_decisions_cannot_be_replayed()
    {
        var approval = Approval.Propose(Guid.NewGuid(), Guid.NewGuid());
        approval.Resolve(true);
        Assert.Equal("Approved", approval.Status);
        Assert.Throws<DomainRuleException>(() => approval.Resolve(false));
    }

    [Fact]
    public void Documents_retain_lexical_search_when_embeddings_fail_and_can_be_requeued()
    {
        var user = HubUser.Create("person@example.com", "Person", UserRoles.Member, "Sales");
        var document = Document.Create(user, "Policy", "policy.txt", "hash", "text/plain", "Department", "Sales");
        document.CompleteIndexing(3, "Provider unavailable");
        Assert.Equal("LexicalOnly", document.Status);
        Assert.Equal(3, document.ChunkCount);
        document.QueueReindex();
        Assert.Equal("Queued", document.Status);
        Assert.Null(document.Error);
        document.BeginIndexing(DateTime.UtcNow);
        Assert.Throws<DomainRuleException>(document.EnsureCanDelete);
        Assert.Throws<DomainRuleException>(document.QueueReindex);
        Assert.Throws<DomainRuleException>(() => document.CompleteIndexing(0, null));
    }

    [Fact]
    public void Agent_runs_cannot_start_twice_or_finish_with_an_active_status()
    {
        var run = AgentRun.Create(Guid.NewGuid(), Guid.NewGuid(), "Question");
        Assert.Throws<DomainRuleException>(() => run.Finish(RunStatus.Succeeded));
        run.Start("worker", DateTime.UtcNow.AddMinutes(1));
        Assert.Throws<DomainRuleException>(() => run.Start("other-worker", DateTime.UtcNow.AddMinutes(1)));
        Assert.Throws<DomainRuleException>(() => run.Finish(RunStatus.Queued));
        run.Finish(RunStatus.Succeeded);
        Assert.Null(run.LeaseUntil);
    }

    [Fact]
    public void Access_policy_allows_team_members_owners_and_admins()
    {
        var owner = HubUser.Create("owner@example.com", "Owner", UserRoles.Member, "Sales");
        var peer = HubUser.Create("peer@example.com", "Peer", UserRoles.Member, "Sales");
        var other = HubUser.Create("other@example.com", "Other", UserRoles.Member, "Finance");
        var admin = HubUser.Create("admin@example.com", "Admin", UserRoles.Administrator, "Finance");
        var team = Document.Create(owner, "Policy", "policy.txt", "hash", "text/plain", "Department", "Sales");
        Assert.True(DocumentAccessPolicy.For(owner).Compile()(team));
        Assert.True(DocumentAccessPolicy.For(peer).Compile()(team));
        Assert.False(DocumentAccessPolicy.For(other).Compile()(team));
        Assert.True(DocumentAccessPolicy.For(admin).Compile()(team));
        Assert.False(DocumentAccessPolicy.CanManage(team, peer));
    }
}
