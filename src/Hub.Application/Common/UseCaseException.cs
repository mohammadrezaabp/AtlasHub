namespace Hub.Application.Common;

public sealed class UseCaseException(ErrorKind kind, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
}
