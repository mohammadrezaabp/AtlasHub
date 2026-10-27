namespace Hub.Application.Agents;

public sealed record RunEventPage(IReadOnlyList<RunEvent> Events, bool Finished);
