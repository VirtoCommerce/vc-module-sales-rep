using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The GraphQL operations a browser context sends, recorded from its first navigation on, in the order they go out and
/// their responses arrive, and the ones a test makes fail. <see cref="AttachAsync"/> goes in as <c>OpenAsync</c>'s
/// <c>beforeLoad</c>: a route added once the page is up, while the hub plugin still loads, can leave the page blank.
/// </summary>
internal sealed class GraphQlTraffic(Func<string, bool> failing = null)
{
    private readonly List<GraphQlOperation> _sent = [];
    private readonly List<(string Name, int At)> _finished = [];
    private readonly Dictionary<IRequest, string> _inFlight = [];
    private int _events;

    public async Task AttachAsync(IBrowserContext context)
    {
        context.Request += (_, request) =>
        {
            if (Read(request) is { } operation)
            {
                lock (_sent)
                {
                    _sent.Add(operation with { SentAt = ++_events });
                    _inFlight[request] = operation.Name;
                }
            }
        };

        // A response that arrived in full; a failed request never finishes.
        context.RequestFinished += (_, request) =>
        {
            lock (_sent)
            {
                if (_inFlight.Remove(request, out var name))
                {
                    _finished.Add((name, ++_events));
                }
            }
        };

        if (failing != null)
        {
            // Aborted, as a dropped connection or a server that never answers would leave it.
            await context.RouteAsync("**/graphql", route => Read(route.Request) is { } operation && failing(operation.Name)
                ? route.AbortAsync("failed")
                : route.ContinueAsync());
        }
    }

    /// <summary>The operations sent under this name so far, in the order they went out.</summary>
    public IReadOnlyList<GraphQlOperation> Sent(string name)
    {
        lock (_sent)
        {
            return [.. _sent.Where(x => x.Name == name)];
        }
    }

    /// <summary>When the first response to this operation arrived, on the scale of <see cref="GraphQlOperation.SentAt"/>; null before one has.</summary>
    public int? FinishedAt(string name)
    {
        lock (_sent)
        {
            return _finished.Where(x => x.Name == name).Select(x => (int?)x.At).FirstOrDefault();
        }
    }

    private static GraphQlOperation Read(IRequest request)
    {
        if (request.Method != "POST" || !new Uri(request.Url).AbsolutePath.EndsWith("/graphql", StringComparison.Ordinal) || request.PostData is not { } body)
        {
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("operationName", out var name) && name.ValueKind == JsonValueKind.String
            ? new GraphQlOperation(name.GetString(), root.TryGetProperty("variables", out var variables) ? variables.Clone() : default, 0)
            : null;
    }
}
