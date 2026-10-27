using Hub.Domain.Common;

namespace Hub.Domain.Workspace;

public sealed record TeamName
{
    public string Value { get; }

    public TeamName(string value)
    {
        var name = value?.Trim() ?? "";
        if (name.Length is < 1 or > 100) throw new DomainRuleException("Team names require 1–100 characters.");
        Value = name;
    }
}
