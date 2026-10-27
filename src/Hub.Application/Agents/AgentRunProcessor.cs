using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hub.Application.Agents;

public sealed class AgentRunProcessor(
    IHubSessionFactory factory,
    IModelClient models,
    RetrievalService retrieval,
    IHubToolClient tools,
    IEventStore events,
    IRunStore runs,
    IOptions<FeatureOptions> features,
    QueryRewriter rewriter,
    ImageEvidenceReader images,
    AgentToolLoop toolLoop,
    ILogger<AgentRunProcessor> logger)
{
    public async Task Execute(AgentRun run, string owner, CancellationToken host)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(host, deadline.Token);
        var ct = cancel.Token;
        var monitor = Monitor(run.Id, owner, cancel);
        var answer = new StringBuilder();
        var sources = new List<Citation>();
        var status = RunStatus.Succeeded;
        try
        {
            using var trace = HubTelemetry.Activity.StartActivity("agent.run");
            await using var db = await factory.CreateAsync(ct);
            var user = await db.SingleAsync(db.Users.Where(u => u.Id == run.UserId && u.Enabled), ct);
            if (run.CancelRequested)
                throw new OperationCanceledException(ct);
            await events.Append(run.Id, new { type = "run_started", runId = run.Id, model = models.Model("Chat") }, ct);
            async Task Stage(string message) => await events.Append(run.Id, new { type = "stage", message }, ct);
            var past = await db.ListAsync(db.Messages.Where(m => m.ConversationId == run.ConversationId && m.RunId != run.Id).OrderByDescending(m => m.Id).Take(20), ct);
            past.Reverse();
            var query = run.Prompt;
            if (Regex.IsMatch(query, @"\b(list|show)\b.*\b(documents|files)\b", RegexOptions.IgnoreCase))
            {
                await events.Append(run.Id, new { type = "tool_call", name = "workspace_overview" }, ct);
                var inventory = await tools.Call("workspace_overview", new JsonObject(), user.Id, ct);
                await events.Append(run.Id, new { type = "tool_result", name = "workspace_overview", status = "completed" }, ct);
                if (inventory is not JsonArray rows)
                    throw new ModelException("The document tool could not return a document list.");
                answer.Append(rows.Count == 0 ? "There are no documents you can access yet." : "Here are the documents you can access:\n\n");
                foreach (var row in rows)
                    answer.Append("- **").Append(row!["title"]!.ToString()).Append("** — ").Append(row["status"]!.ToString()).Append(" · ").Append(row["chunkCount"]!.ToString()).Append(" passages\n");
                await events.Append(run.Id, new { type = "text_delta", delta = answer.ToString() }, ct);
                return;
            }

            var imageOnly = run.ImagePath != null && !Regex.IsMatch(run.Prompt, @"\b(compare|documents|policy|knowledge|library)\b", RegexOptions.IgnoreCase);
            if (!imageOnly && features.Value.Rewrite)
            {
                await Stage("Understanding your question");
                try
                {
                    query = await rewriter.Rewrite(run.Prompt, past, user.Id, ct);
                }
                catch (ModelException)
                {
                    await Stage("Using your original question");
                }
            }

            if (!imageOnly)
            {
                await Stage("Searching your knowledge · hybrid retrieval");
                sources = await retrieval.Search(query, user, true, ct);
            }

            await events.Append(run.Id, new { type = "sources", sources }, ct);
            string? visualEvidence = null;
            if (run.ImagePath != null && features.Value.Vision)
            {
                await Stage("Reading your image with the vision model");
                var imageSource = await images.Read(run, user, ct);
                visualEvidence = imageSource.Excerpt;
                sources.Add(imageSource);
                await events.Append(run.Id, new { type = "sources", sources }, ct);
                if (imageOnly)
                {
                    answer.Append(visualEvidence).Append(" [1]");
                    await events.Append(run.Id, new { type = "text_delta", delta = answer.ToString() }, ct);
                    return;
                }
            }

            var messages = AgentPromptBuilder.Build(run.Prompt, past, sources, visualEvidence);
            var useTools = Regex.IsMatch(query, @"\b(list|documents|delete|remove|workspace|count|sources|inventory)\b", RegexOptions.IgnoreCase);
            var hasToolEvidence = false;
            if (useTools)
            {
                await Stage("Checking workspace tools");
                hasToolEvidence = await toolLoop.Execute(messages, run.Prompt, run.Id, user.Id, ct);
            }

            if (sources.Count == 0 && visualEvidence == null && !hasToolEvidence)
            {
                answer.Append("I couldn't find enough evidence in the documents you can access. Try a more specific question, or add a relevant document to the knowledge library.");
                await events.Append(run.Id, new { type = "text_delta", delta = answer.ToString() }, ct);
            }
            else
            {
                await Stage("Writing an answer grounded in your sources");
                var buffer = new StringBuilder();
                await foreach (var delta in models.Stream(messages.ToArray(), user.Id.ToString(), ct))
                {
                    answer.Append(delta);
                    buffer.Append(delta);
                    if (buffer.Length >= 80)
                    {
                        await events.Append(run.Id, new { type = "text_delta", delta = buffer.ToString() }, ct);
                        buffer.Clear();
                    }
                }

                if (buffer.Length > 0)
                    await events.Append(run.Id, new { type = "text_delta", delta = buffer.ToString() }, ct);
                if (answer.Length == 0)
                    throw new ModelException("The model returned an empty answer. Please retry your question.");
            }
        }
        catch (OperationCanceledException)
        {
            status = deadline.IsCancellationRequested ? RunStatus.TimedOut : RunStatus.Cancelled;
            await events.Append(run.Id, new { type = "run_cancelled", message = deadline.IsCancellationRequested ? "The answer reached its time limit." : "Answer stopped." });
        }
        catch (Exception e)
        {
            status = RunStatus.Failed;
            logger.LogWarning("Run {Run} failed with {Type}", run.Id, e.GetType().Name);
            await events.Append(run.Id, new { type = "error", message = e is ModelException ? e.Message : "Unable to finish this answer. Please retry." });
        }
        finally
        {
            cancel.Cancel();
            await monitor;
            try
            {
                await runs.Complete(run, owner, status, answer.ToString(), sources);
            }
            catch (Exception e)
            {
                logger.LogError("Run persistence failed with {Type}", e.GetType().Name);
            }
        }
    }

    async Task Monitor(Guid id, string owner, CancellationTokenSource source)
    {
        try
        {
            while (!source.IsCancellationRequested)
            {
                await Task.Delay(1500, source.Token);
                if (!await runs.Heartbeat(id, owner, source.Token))
                {
                    source.Cancel();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            source.Cancel();
        }
    }
}
