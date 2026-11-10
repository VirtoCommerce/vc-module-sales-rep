using System;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Tests.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

internal static class OrderSeeder
{
    public static void Seed(
        SalesRepTestContext ctx,
        string id,
        string org,
        string number,
        DateTime createdDate,
        string storeId = "B2B-store",
        int itemsCount = 0,
        int quantityPerItem = 1,
        string status = "New",
        string organizationName = null,
        string createdByUserId = null,
        decimal total = 123.45m,
        string addressLine1 = null,
        string couponCode = null)
    {
        using var db = ctx.NewOrderDbContext();
        var order = new CustomerOrderEntity
        {
            Id = id,
            Number = number,
            OrganizationId = org,
            OrganizationName = organizationName,
            // A rep-created order records the rep's user id as CustomerId - the value the queries filter on.
            CustomerId = createdByUserId ?? ctx.LastCreatedRepUserId ?? "customer-1",
            CustomerName = "Customer 1",
            StoreId = storeId,
            Status = status,
            Currency = "USD",
            Total = total,
            IsPrototype = false,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
        };

        if (addressLine1 != null)
        {
            order.Addresses.Add(new AddressEntity
            {
                Id = $"{id}-addr",
                AddressType = "BillingAndShipping",
                CountryCode = "USA",
                CountryName = "United States",
                City = "Los Angeles",
                PostalCode = "90001",
                Line1 = addressLine1,
            });
        }

        if (couponCode != null)
        {
            order.Discounts.Add(new DiscountEntity
            {
                Id = $"{id}-disc",
                Currency = "USD",
                CouponCode = couponCode,
                PromotionId = "promo-1",
                DiscountAmount = 2m,
            });
        }

        for (var i = 0; i < itemsCount; i++)
        {
            order.Items.Add(new LineItemEntity
            {
                Id = $"{id}-li-{i}",
                Currency = "USD",
                ProductId = $"prod-{i}",
                CatalogId = "catalog-1",
                Sku = $"SKU-{i}",
                Name = $"Product {i}",
                Quantity = quantityPerItem,
                CreatedDate = createdDate,
                ModifiedDate = createdDate,
            });
        }

        db.Add(order);
        db.SaveChangesKeepingDates();
    }

    // Reproduces what the orders REST update can do to an already-indexed order: the order moves organization through
    // the orders module's service (which drops its cached copy), the document does not (the module reindexes in a job).
    public static async Task MoveToOrganizationAsync(SalesRepTestContext ctx, string id, string organizationId)
    {
        var orders = ctx.GetRequiredService<ICustomerOrderService>();
        var order = await orders.GetByIdAsync(id, CustomerOrderResponseGroup.Full.ToString());

        order.OrganizationId = organizationId;
        await orders.SaveChangesAsync([order]);
    }
}
