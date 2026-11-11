using System;
using System.Globalization;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Dashboard;

/// <summary>
/// Group 6: the hub dashboard. Its rep placed two orders for Tailspin Toys, one in this store and one in another; a test
/// that saves a layout signs in as a rep of its own, since a layout is saved per user.
/// </summary>
public sealed class DashboardEnvironment : StorefrontEnvironment
{
    public const string DashboardPath = "/company/dashboard";
    public const string TailspinId = "e2e-org-tailspin";
    public const string StoreOrderNumber = "ORD-2001";
    public const string OtherStoreOrderNumber = "ORD-2002";

    private int _orders;

    public string RepUserId { get; private set; }

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(TailspinId, org => org.Name = "Tailspin Toys");
        var rep = await context.CreateRepInStoreAsync("Dora", "Rep", "dora.rep@e2e.local", InProcessBackend.StoreId, TailspinId);
        RepUserId = rep.UserId;

        // Placed now, so both fall inside every period the cards show whatever the date: only the store tells them apart.
        OrderSeeder.Seed(context, "e2e-order-2001", TailspinId, StoreOrderNumber, DateTime.UtcNow, createdByUserId: rep.UserId, total: 200m);
        OrderSeeder.Seed(context, "e2e-order-2002", TailspinId, OtherStoreOrderNumber, DateTime.UtcNow, storeId: "Electronics", createdByUserId: rep.UserId, total: 75m);
        await context.IndexOrdersAsync("e2e-order-2001", "e2e-order-2002");
        await context.IndexMembersAsync(TailspinId);
    }

    /// <summary>Orders the rep places for Tailspin in this store, a minute apart; returns their numbers, newest first.</summary>
    internal Task<string[]> SeedOrdersAsync(SalesRepDetails rep, int count)
        => RunExclusiveAsync(async context =>
        {
            var ids = new string[count];
            var numbers = new string[count];

            for (var i = 0; i < count; i++)
            {
                var n = (++_orders).ToString("000", CultureInfo.InvariantCulture);
                ids[i] = $"e2e-order-3{n}";
                numbers[i] = $"ORD-3{n}";
                OrderSeeder.Seed(context, ids[i], TailspinId, numbers[i], DateTime.UtcNow.AddMinutes(-i), createdByUserId: rep.UserId);
            }

            await context.IndexOrdersAsync(ids);

            return numbers;
        });
}
