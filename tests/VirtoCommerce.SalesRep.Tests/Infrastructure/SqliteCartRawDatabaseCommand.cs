using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.CartModule.Data.Repositories;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The cart module's raw SQL for SQLite, which the module ships for SQL Server, PostgreSQL and MySQL only: the same
/// statements as its PostgreSQL command (vc-module-cart 3.1012.0), with SQLite's 0/1 booleans. X-Cart looks up which
/// of a buyer's wishlists hold the products a page shows.
/// </summary>
internal sealed class SqliteCartRawDatabaseCommand : ICartRawDatabaseCommand
{
    public Task SoftRemove(CartDbContext dbContext, IList<string> ids)
    {
        var parameters = ids.Select((id, i) => new SqliteParameter($"@p{i}", id)).ToList();
        var sql = $"UPDATE \"Cart\" SET \"IsDeleted\" = 1 WHERE \"Id\" IN ({string.Join(",", parameters.Select(x => x.ParameterName))})";

        return dbContext.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    public async Task<IList<ProductWishlistEntity>> FindWishlistsByProductsAsync(CartDbContext dbContext, string customerId, string organizationId, string storeId, IList<string> productIds)
    {
        var hasCustomer = !string.IsNullOrEmpty(customerId);
        var hasOrganization = !string.IsNullOrEmpty(organizationId);

        // Without an owner there is nothing to scope the query to, as the module's command says.
        if (!hasCustomer && !hasOrganization)
        {
            return [];
        }

        var parameters = new List<SqliteParameter>();
        var storeFilter = string.Empty;
        if (!string.IsNullOrEmpty(storeId))
        {
            storeFilter = " AND c.\"StoreId\" = @storeId";
            parameters.Add(new SqliteParameter("@storeId", storeId));
        }

        var ownerFilters = new List<string>();
        if (hasCustomer)
        {
            ownerFilters.Add("c.\"CustomerId\" = @customerId AND c.\"OrganizationId\" IS NULL");
            parameters.Add(new SqliteParameter("@customerId", customerId));
        }

        if (hasOrganization)
        {
            ownerFilters.Add("c.\"OrganizationId\" = @organizationId");
            parameters.Add(new SqliteParameter("@organizationId", organizationId));
        }

        var ownerFilter = ownerFilters.Count > 1 ? $"({string.Join(" OR ", ownerFilters)})" : ownerFilters[0];

        var productParameters = productIds.Select((id, i) => new SqliteParameter($"@productIds{i}", id)).ToList();
        parameters.AddRange(productParameters);

        var sql = $"""
            SELECT c."Id", li."ProductId"
            FROM "Cart" c
            INNER JOIN "CartLineItem" li
            ON c."Id" = li."ShoppingCartId"
            WHERE c."IsDeleted" = 0 AND c."Type" = 'Wishlist'
            AND li."IsGift" = 0
            AND li."ProductId" IN ({string.Join(",", productParameters.Select(x => x.ParameterName))}){storeFilter}
            AND {ownerFilter}
            """;

        return await dbContext.Set<ProductWishlistEntity>().FromSqlRaw(sql, [.. parameters]).ToListAsync();
    }
}
