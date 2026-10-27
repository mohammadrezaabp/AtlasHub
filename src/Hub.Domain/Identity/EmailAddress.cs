using Hub.Domain.Common;

namespace Hub.Domain.Identity;

public sealed record EmailAddress
{
    public string Value { get; }

    public EmailAddress(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(normalized, out _))
            throw new DomainRuleException("Enter a valid email address.");
        Value = normalized;
    }
}
