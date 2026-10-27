using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Hub.Gateway.Proxy;

public sealed class ModelGatewayProxy(IConfiguration config, IHttpClientFactory clients, IModelProviderCatalog providerCatalog, UsageAccountingService usageAccounting, ILogger<ModelGatewayProxy> log)
{
    readonly SemaphoreSlim admission = new(4, 4);
    int failures;
    DateTime openUntil;
    public Task<JsonArray> Catalog(CancellationToken ct) => providerCatalog.List(ct);

    public async Task Forward(string endpoint, HttpContext ctx)
    {
        if (endpoint is not ("chat/completions" or "embeddings"))
        {
            ctx.Response.StatusCode = 404;
            return;
        }

        if (!await admission.WaitAsync(0, ctx.RequestAborted))
        {
            await Error(ctx, 429, "The model gateway is busy. Retry shortly.");
            return;
        }

        ModelUsage? usage = null;
        try
        {
            var key = config["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(key))
            {
                await Error(ctx, 503, "Configure the OpenRouter API key in your local environment.");
                return;
            }

            JsonObject body;
            try
            {
                body = (await JsonNode.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted))!.AsObject();
            }
            catch
            {
                await Error(ctx, 400, "Invalid model request.");
                return;
            }

            var role = ctx.Request.Headers["X-Hub-Role"].ToString();
            if (!new[]
            {
                "Chat",
                "Fast",
                "Rerank",
                "Vision",
                "Embedding"
            }.Contains(role) || (role == "Embedding") != (endpoint == "embeddings"))
            {
                await Error(ctx, 403, "Model role is not granted.");
                return;
            }

            var model = config[$"Models:{role}"] ?? "";
            if (body["model"]?.GetValue<string>() != model)
            {
                await Error(ctx, 403, "Model is not granted for this role.");
                return;
            }

            if (endpoint != "embeddings")
                body["max_tokens"] = Math.Clamp(body["max_tokens"]?.GetValue<int>() ?? 1000, 1, 2000);
            var actor = ctx.Request.Headers["X-Hub-Actor"].ToString();
            var stream = body["stream"]?.GetValue<bool>() == true;
            var reservation = decimal.Parse(config["Budget:MaxRequestUsd"] ?? "0.05", CultureInfo.InvariantCulture);
            usage = await usageAccounting.Reserve(actor, role, model, ctx.RequestAborted);
            var allowedModels = await Catalog(ctx.RequestAborted);
            var price = allowedModels.FirstOrDefault(m => m?["id"]?.GetValue<string>() == model)?["pricing"];
            if (price == null)
            {
                await Error(ctx, 503, "The configured model is absent from the provider catalog.");
                return;
            }

            var inputRate = ParseRate(price["prompt"]);
            var outputRate = ParseRate(price["completion"]);
            var estimatedTokens = Encoding.UTF8.GetByteCount(body.ToJsonString()) / 2 + 100;
            if (inputRate * estimatedTokens + outputRate * (body["max_tokens"]?.GetValue<int>() ?? 0) > reservation)
            {
                await Error(ctx, 413, "This request exceeds the per-request AI spending limit.");
                return;
            }

            var fallback = config["Models:Fallback"];
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, deadline.Token);
            HttpResponseMessage? upstream = null;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (attempt > 0)
                {
                    if (endpoint == "embeddings" || role == "Vision" || string.IsNullOrEmpty(fallback))
                        break;
                    var fp = allowedModels.FirstOrDefault(m => m?["id"]?.GetValue<string>() == fallback)?["pricing"];
                    if (fp == null || ParseRate(fp["prompt"]) * estimatedTokens + ParseRate(fp["completion"]) * 2000 > reservation)
                        break;
                    body["model"] = fallback;
                    usage.Model = fallback;
                    inputRate = ParseRate(fp["prompt"]);
                    outputRate = ParseRate(fp["completion"]);
                }

                if (openUntil > DateTime.UtcNow && attempt == 0 && endpoint != "embeddings" && role != "Vision")
                    continue;
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/" + endpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                req.Headers.Add("HTTP-Referer", "http://localhost:5080");
                req.Headers.Add("X-Title", "Atlas Knowledge Hub");
                req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
                upstream = await clients.CreateClient("external").SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token);
                if (upstream.IsSuccessStatusCode)
                {
                    failures = 0;
                    break;
                }

                if ((int)upstream.StatusCode < 500 && upstream.StatusCode != HttpStatusCode.TooManyRequests)
                    break;
                if (Interlocked.Increment(ref failures) >= 3)
                    openUntil = DateTime.UtcNow.AddSeconds(30);
                if (attempt == 0 && endpoint != "embeddings" && role != "Vision")
                {
                    upstream.Dispose();
                    upstream = null;
                }
            }

            using var owned = upstream;
            if (upstream == null || !upstream.IsSuccessStatusCode)
            {
                var status = upstream == null ? 503 : (int)upstream.StatusCode;
                await Error(ctx, status, status switch
                {
                    401 => "OpenRouter rejected the configured API key.",
                    402 => "Add credits to your OpenRouter account.",
                    _ => $"Model provider unavailable ({status}). Retry shortly."
                });
                usage.Status = ModelUsageStatus.Failed;
                usage.CostUsd = 0;
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = stream ? "text/event-stream" : "application/json";
            ctx.Response.Headers.CacheControl = "no-cache";
            var receivedUsage = false;
            if (stream)
            {
                using var reader = new StreamReader(await upstream.Content.ReadAsStreamAsync(linked.Token));
                var disconnected = false;
                while (await reader.ReadLineAsync(deadline.Token) is { } line)
                {
                    if (line.StartsWith("data: ") && line != "data: [DONE]")
                    {
                        try
                        {
                            var node = JsonNode.Parse(line[6..]);
                            if (node?["usage"] is JsonObject u)
                            {
                                Record(usage, u, inputRate, outputRate);
                                receivedUsage = true;
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                        }
                    }

                    if (!disconnected)
                    {
                        try
                        {
                            await ctx.Response.WriteAsync(line + "\n", ctx.RequestAborted);
                            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException or IOException)
                        {
                            disconnected = true;
                        }
                    }
                }
            }
            else
            {
                var text = await upstream.Content.ReadAsStringAsync(linked.Token);
                var node = JsonNode.Parse(text);
                if (node?["usage"] is JsonObject u)
                {
                    Record(usage, u, inputRate, outputRate);
                    receivedUsage = true;
                }

                await ctx.Response.WriteAsync(text, ctx.RequestAborted);
            }

            usage.Status = receivedUsage ? ModelUsageStatus.Succeeded : ModelUsageStatus.UsageMissing;
            // Missing usage retains the conservative reservation instead of reporting a false zero cost.
        }
        catch (UseCaseException ex) { await Error(ctx, ex.Kind == ErrorKind.RateLimited ? 429 : 400, ex.Message); }
        catch (Exception ex)
        {
            log.LogWarning("Gateway call failed with {Type}", ex.GetType().Name);
            if (usage != null)
                usage.Status = ModelUsageStatus.Interrupted;
            if (!ctx.Response.HasStarted)
                await Error(ctx, 502, "The model connection failed. Please retry.");
        }
        finally
        {
            if (usage != null)
            {
                try
                {
                    await usageAccounting.Settle(usage);
                }
                catch (Exception ex)
                {
                    log.LogError("Usage persistence failed with {Type}", ex.GetType().Name);
                }
            }

            admission.Release();
        }
    }

    static decimal ParseRate(JsonNode? n) => decimal.TryParse(n?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    static void Record(ModelUsage target, JsonObject u, decimal input, decimal output)
    {
        target.InputTokens = u["prompt_tokens"]?.GetValue<int>() ?? u["total_tokens"]?.GetValue<int>() ?? 0;
        target.OutputTokens = u["completion_tokens"]?.GetValue<int>() ?? 0;
        target.CostReported = u["cost"] != null;
        target.CostUsd = u["cost"] != null ? ParseRate(u["cost"]) : target.InputTokens * input + target.OutputTokens * output;
    }

    static async Task Error(HttpContext ctx, int status, string message)
    {
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { error = new { message } });
    }
}
