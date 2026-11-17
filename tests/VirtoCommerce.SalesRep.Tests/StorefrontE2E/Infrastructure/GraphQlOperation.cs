using System.Text.Json;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// One GraphQL operation the storefront sent: its name, the variables it went out with, and when, counted in the
/// recording's events (<see cref="GraphQlTraffic.FinishedAt"/> counts on the same scale).
/// </summary>
internal sealed record GraphQlOperation(string Name, JsonElement Variables, int SentAt);
