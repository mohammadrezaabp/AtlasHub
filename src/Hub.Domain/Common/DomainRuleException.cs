namespace Hub.Domain.Common;

public sealed class DomainRuleException(string message, bool isConflict = false) : Exception(message)
{
    public bool IsConflict { get; } = isConflict;
}
