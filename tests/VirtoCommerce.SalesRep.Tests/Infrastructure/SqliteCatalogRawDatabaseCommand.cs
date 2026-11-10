using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Data.Extensions;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Data.Extensions;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The catalog module's raw SQL for SQLite, which the module ships for SQL Server, PostgreSQL and MySQL only: the same
/// statements as its PostgreSQL command (vc-module-catalog 3.1048.0), with SQLite's paging and a LIKE where PostgreSQL
/// splits the tags. Ported is what the storefront's pages reach: a category save clears its children's cache, a
/// product's outline walks its parents, the product page searches the product's associations. The deletes and the SEO
/// duplicates check, which nothing here runs, throw with their name.
/// </summary>
internal sealed class SqliteCatalogRawDatabaseCommand : ICatalogRawDatabaseCommand
{
    private const int BatchSize = 500;

    public async Task<IList<CategoryHierarchyItem>> GetChildCategoriesAsync(CatalogDbContext dbContext, IList<string> categoryIds)
    {
        if (categoryIds.IsNullOrEmpty())
        {
            return [];
        }

        const string commandTemplate =
            """
            WITH RECURSIVE CategoryHierarchy AS (
                SELECT a."Id", a."ParentCategoryId", 0 AS "Depth" FROM "Category" a
                WHERE a."Id" IN ({0})
                UNION ALL
                SELECT c."Id", c."ParentCategoryId", (ch."Depth" + 1) AS "Depth" FROM "Category" c
                INNER JOIN CategoryHierarchy ch ON c."ParentCategoryId" = ch."Id"
            )
            SELECT "Id", "ParentCategoryId", "Depth" FROM CategoryHierarchy WHERE "Id" NOT IN ({0});
            """;

        var result = new List<CategoryHierarchyItem>();
        foreach (var idsPage in categoryIds.Paginate(BatchSize))
        {
            var command = CreateCommand(commandTemplate, idsPage);
            var batch = await dbContext.ExecuteEntityArrayAsync<CategoryHierarchyItem>(command.Text, [.. command.Parameters]);
            if (!batch.IsNullOrEmpty())
            {
                result.AddRange(batch);
            }
        }

        return result;
    }

    public async Task<IList<CategoryEntity>> SearchCategoriesHierarchyAsync(CatalogDbContext dbContext, string categoryId)
    {
        const string commandTemplate =
            """
            WITH RECURSIVE cp AS (
                SELECT * FROM "Category" WHERE "Id" = @categoryId
            UNION ALL
                SELECT c.* FROM "Category" AS c
                    JOIN cp
                        ON c."Id" = cp."ParentCategoryId"
            )
            SELECT * FROM cp
            """;

        return await dbContext.Set<CategoryEntity>().FromSqlRaw(commandTemplate, new SqliteParameter("@categoryId", categoryId)).ToListAsync();
    }

    public async Task<GenericSearchResult<AssociationEntity>> SearchAssociations(CatalogDbContext dbContext, ProductAssociationSearchCriteria criteria)
    {
        var countCommand = CreateCommand(GetAssociationsSqlCommandText(criteria, "SELECT COUNT(*) FROM Item_CTE WHERE \"AssociatedItemId\" IS NOT NULL"), criteria.ObjectIds ?? []);
        var queryCommand = CreateCommand(GetAssociationsSqlCommandText(criteria, "SELECT * FROM Item_CTE WHERE \"AssociatedItemId\" IS NOT NULL ORDER BY \"Priority\" LIMIT @take OFFSET @skip"), criteria.ObjectIds ?? []);
        queryCommand.Parameters.Add(new SqliteParameter("@skip", criteria.Skip));
        queryCommand.Parameters.Add(new SqliteParameter("@take", criteria.Take));

        foreach (var command in new[] { countCommand, queryCommand })
        {
            if (!string.IsNullOrEmpty(criteria.Group))
            {
                command.Parameters.Add(new SqliteParameter("@group", criteria.Group));
            }

            if (!string.IsNullOrEmpty(criteria.Keyword))
            {
                command.Parameters.Add(new SqliteParameter("@keyword", $"%{criteria.Keyword}%"));
            }

            AddParameters(command, "@tags", criteria.Tags);
            AddParameters(command, "@associatedObjectIds", criteria.AssociatedObjectIds);
        }

        var totalCount = await dbContext.ExecuteScalarAsync<long>(countCommand.Text, [.. countCommand.Parameters]);

        return new GenericSearchResult<AssociationEntity>
        {
            TotalCount = (int)totalCount,
            Results = criteria.Take > 0
                ? await dbContext.Set<AssociationEntity>().FromSqlRaw(queryCommand.Text, [.. queryCommand.Parameters]).ToListAsync()
                : [],
        };
    }

    public Task<IList<string>> GetAllSeoDuplicatesIdsAsync(CatalogDbContext dbContext) => throw NotPorted();

    public Task RemoveItemsAsync(CatalogDbContext dbContext, IList<string> itemIds) => throw NotPorted();

    public Task RemoveCategoriesAsync(CatalogDbContext dbContext, IList<string> ids) => throw NotPorted();

    public Task RemoveCatalogsAsync(CatalogDbContext dbContext, IList<string> ids) => throw NotPorted();

    public Task RemoveAllPropertyValuesAsync(CatalogDbContext dbContext, PropertyEntity catalogProperty, PropertyEntity categoryProperty, PropertyEntity itemProperty)
        => throw NotPorted();

    // The PostgreSQL command's association query: the associations themselves, plus every product under an associated
    // category; the caller's final SELECT counts or pages them.
    private static string GetAssociationsSqlCommandText(ProductAssociationSearchCriteria criteria, string select)
    {
        var command = new StringBuilder(
            """
            WITH RECURSIVE Association_CTE AS
            (
                SELECT
                     a."Id",
                     a."AssociationType",
                     a."Priority",
                     a."ItemId",
                     a."CreatedDate",
                     a."ModifiedDate",
                     a."CreatedBy",
                     a."ModifiedBy",
                     a."AssociatedItemId",
                     a."AssociatedCategoryId",
                     a."Tags",
                     a."Quantity",
                     a."OuterId"
                FROM "Association" a
            """);

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            command.Append(" LEFT JOIN \"Item\" i ON i.\"Id\" = a.\"AssociatedItemId\"");
        }

        var conditions = new List<string>();
        if (!criteria.ObjectIds.IsNullOrEmpty())
        {
            conditions.Add("\"ItemId\" IN ({0})");
        }

        if (!criteria.AssociatedObjectIds.IsNullOrEmpty())
        {
            conditions.Add("a.\"AssociatedItemId\" IN (@associatedObjectIds)");
        }

        if (!string.IsNullOrEmpty(criteria.Group))
        {
            conditions.Add("\"AssociationType\" = @group");
        }

        if (!criteria.Tags.IsNullOrEmpty())
        {
            conditions.Add($"({string.Join(" OR ", criteria.Tags.Select((_, i) => $"(';' || \"Tags\" || ';') LIKE ('%;' || @tags{i} || ';%')"))})");
        }

        if (!string.IsNullOrEmpty(criteria.Keyword))
        {
            conditions.Add("i.\"Name\" LIKE @keyword");
        }

        if (conditions.Count > 0)
        {
            command.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        command.Append(
            """

            ), Category_CTE AS
            (
                SELECT "AssociatedCategoryId" Id, "AssociatedCategoryId"
                FROM Association_CTE
                WHERE "AssociatedCategoryId" IS NOT NULL
                UNION ALL
                SELECT c."Id", cte."AssociatedCategoryId"
                FROM "Category" c
                INNER JOIN Category_CTE cte ON c."ParentCategoryId" = cte.Id
            ),
            Item_CTE AS
            (
                SELECT
                    a."Id",
                    a."AssociationType",
                    a."Priority",
                    a."ItemId",
                    a."CreatedDate",
                    a."ModifiedDate",
                    a."CreatedBy",
                    a."ModifiedBy",
                    i."Id" "AssociatedItemId",
                    a."AssociatedCategoryId",
                    a."Tags",
                    a."Quantity",
                    a."OuterId"
                FROM Category_CTE cat
                LEFT JOIN "Item" i ON cat.Id = i."CategoryId"
                LEFT JOIN "Association" a ON cat."AssociatedCategoryId" = a."AssociatedCategoryId"
                WHERE i."ParentId" IS NULL
                UNION
                SELECT * FROM Association_CTE
            )

            """);
        command.Append(select);

        return command.ToString();
    }

    // {0} becomes the values as parameters @p0, @p1...; as the PostgreSQL command does.
    private static Command CreateCommand(string commandTemplate, IEnumerable<string> values)
    {
        var parameters = values.Select((value, i) => new SqliteParameter($"@p{i}", value)).ToList();

        return new Command
        {
            Text = string.Format(commandTemplate, string.Join(",", parameters.Select(x => x.ParameterName))),
            Parameters = [.. parameters],
        };
    }

    // An "IN (@name)" list becomes "IN (@name0, @name1...)", one parameter per value, as the PostgreSQL command does.
    private static void AddParameters(Command command, string name, IList<string> values)
    {
        if (values.IsNullOrEmpty())
        {
            return;
        }

        var names = values.Select((_, i) => $"{name}{i}").ToList();
        command.Text = command.Text.Replace($"({name})", $"({string.Join(", ", names)})");
        for (var i = 0; i < values.Count; i++)
        {
            command.Parameters.Add(new SqliteParameter(names[i], values[i]));
        }
    }

    private static NotSupportedException NotPorted([System.Runtime.CompilerServices.CallerMemberName] string method = null)
        => new($"ICatalogRawDatabaseCommand.{method} is not ported to SQLite: nothing the storefront E2E pages do runs it.");

    private sealed class Command
    {
        public string Text { get; set; } = string.Empty;

        public List<object> Parameters { get; set; } = [];
    }
}
