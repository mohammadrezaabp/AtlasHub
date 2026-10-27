using System.Text.Json.Nodes;

namespace Hub.Application.Abstractions.AI;

public interface IModelProviderCatalog
{
    Task<JsonArray> List(CancellationToken ct);
}
