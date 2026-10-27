using Hub.Domain.Common;
using Xunit;

namespace Hub.Tests.Domain;

public sealed class AccountTests
{
    [Fact]
    public void Five_failed_attempts_lock_the_account_until_the_deadline()
    {
        var user = HubUser.Create("Person@Example.com", "Person", UserRoles.Member, "Sales");
        var now = DateTime.UtcNow;
        for (var i = 0; i < 5; i++) user.RecordFailedLogin(now);
        Assert.False(user.CanSignIn(now.AddMinutes(14)));
        Assert.True(user.CanSignIn(now.AddMinutes(16)));
        user.RecordSuccessfulLogin();
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLogins);
        Assert.Equal("person@example.com", user.Email);
    }

    [Fact]
    public void Password_and_access_changes_invalidate_existing_sessions()
    {
        var user = HubUser.Create("person@example.com", "Person", UserRoles.Member, "Sales");
        user.SetInitialPassword("initial-hash");
        Assert.Throws<DomainRuleException>(() => user.SetInitialPassword("replacement"));
        user.ChangePassword("new-hash");
        Assert.Equal(1, user.SessionVersion);
        user.SetEnabled(false);
        Assert.Equal(2, user.SessionVersion);
        Assert.False(user.CanSignIn(DateTime.UtcNow));
    }

    [Theory]
    [InlineData("invalid-email", "Person", "Member", "Sales")]
    [InlineData("person@example.com", "", "Member", "Sales")]
    [InlineData("person@example.com", "Person", "SuperAdmin", "Sales")]
    [InlineData("person@example.com", "Person", "Member", " ")]
    public void Invalid_account_details_are_rejected(string email, string name, string role, string team) =>
        Assert.Throws<DomainRuleException>(() => HubUser.Create(email, name, role, team));
}
