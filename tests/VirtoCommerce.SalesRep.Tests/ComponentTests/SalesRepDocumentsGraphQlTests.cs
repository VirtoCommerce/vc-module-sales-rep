using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using Xunit;
using Permissions = VirtoCommerce.SalesRep.Core.ModuleConstants.Security.Permissions;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests;

/// <summary>
/// The documents library X-API (VCST-5730 T3) end to end: real GraphQL query strings through the real scoped
/// schema, builders (claim-level permission gate) and the real document services over in-memory SQLite. Covers
/// the three queries' data shapes (paging, default createdDate:desc, keyword/category, categories with counts)
/// and the full authorization matrix: read passes, write implies read, Administrator passes, no-permission and
/// anonymous are denied on EVERY query with no data leaking.
/// </summary>
[Trait("Category", "Component")]
public class SalesRepDocumentsGraphQlTests
{
    private const string RepUserId = "rep-user";

    private static readonly string[] ReadPermission = [Permissions.DocumentsRead];
    private static readonly string[] WritePermission = [Permissions.DocumentsWrite];
    private static readonly string[] AccessOnlyPermission = [Permissions.Access];

    [Fact]
    public async Task SalesRepDocuments_WithReadPermission_PagesNewestFirstByDefault()
    {
        using var ctx = SalesRepTestContext.Create();
        var (oldest, middle, newest) = await SeedLibraryAsync(ctx);

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments { totalCount items { id name category contentType size createdDate modifiedDate url summary pageCount previewUrl } } }",
            userId: RepUserId,
            permissions: ReadPermission);

        var node = SalesRepTestContext.Node(json, "salesRepDocuments");
        node.GetProperty("totalCount").GetInt32().Should().Be(3);

        // No sort argument → default createdDate:desc (newest first).
        var ids = node.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
        ids.Should().Equal(newest.Id, middle.Id, oldest.Id);

        var priced = node.GetProperty("items")[0];
        priced.GetProperty("name").GetString().Should().Be("Price List.xlsx");
        priced.GetProperty("category").GetString().Should().Be("Pricing");
        priced.GetProperty("url").GetString().Should().Be($"/api/sales-rep/documents/{newest.Id}");
        priced.GetProperty("summary").GetString().Should().Be("Current prices");
        priced.GetProperty("pageCount").GetInt32().Should().Be(3);
        priced.GetProperty("size").GetInt64().Should().BeGreaterThan(0);

        // Page 1 (first:2): the two newest; more pages ahead.
        var page1 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments(first:2, after:\"0\") { totalCount pageInfo { hasNextPage } items { id } } }",
            userId: RepUserId,
            permissions: ReadPermission);
        var page1Node = SalesRepTestContext.Node(page1, "salesRepDocuments");
        page1Node.GetProperty("totalCount").GetInt32().Should().Be(3);
        page1Node.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
        page1Node.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString())
            .Should().Equal(newest.Id, middle.Id);

        // Page 2 (after:"2"): the oldest — no overlap, last page.
        var page2 = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments(first:2, after:\"2\") { pageInfo { hasNextPage } items { id } } }",
            userId: RepUserId,
            permissions: ReadPermission);
        var page2Node = SalesRepTestContext.Node(page2, "salesRepDocuments");
        page2Node.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean().Should().BeFalse();
        page2Node.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString())
            .Should().Equal(oldest.Id);

        // Explicit sort overrides the default.
        var byName = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments(sort:\"name:asc\") { items { name } } }",
            userId: RepUserId,
            permissions: ReadPermission);
        SalesRepTestContext.Node(byName, "salesRepDocuments").GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Equal("Lookbook.pdf", "Price List.xlsx", "Spring Catalog.pdf");
    }

    [Fact]
    public async Task SalesRepDocuments_SupportsKeywordAndCategoryFiltering()
    {
        using var ctx = SalesRepTestContext.Create();
        var (oldest, middle, _) = await SeedLibraryAsync(ctx);

        var byKeyword = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments(keyword:\"lookbook\") { totalCount items { id } } }",
            userId: RepUserId,
            permissions: ReadPermission);
        var keywordNode = SalesRepTestContext.Node(byKeyword, "salesRepDocuments");
        keywordNode.GetProperty("totalCount").GetInt32().Should().Be(1);
        keywordNode.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(middle.Id);

        var byCategory = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocuments(category:\"Catalogs\") { totalCount items { id category } } }",
            userId: RepUserId,
            permissions: ReadPermission);
        var categoryNode = SalesRepTestContext.Node(byCategory, "salesRepDocuments");
        categoryNode.GetProperty("totalCount").GetInt32().Should().Be(1);
        categoryNode.GetProperty("items")[0].GetProperty("id").GetString().Should().Be(oldest.Id);
    }

    [Fact]
    public async Task SalesRepDocument_ById_ReturnsTheDocumentOrNull()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Guide.pdf", "Guides", new SalesRepDocumentMetadata { Summary = "How-to" });

        var json = await ctx.ExecuteGraphQlAsync(
            $"query {{ salesRepDocument(id:\"{document.Id}\") {{ id name category url summary }} }}",
            userId: RepUserId,
            permissions: ReadPermission);

        var node = SalesRepTestContext.Node(json, "salesRepDocument");
        node.GetProperty("id").GetString().Should().Be(document.Id);
        node.GetProperty("name").GetString().Should().Be("Guide.pdf");
        node.GetProperty("category").GetString().Should().Be("Guides");
        node.GetProperty("url").GetString().Should().Be($"/api/sales-rep/documents/{document.Id}");
        node.GetProperty("summary").GetString().Should().Be("How-to");

        // Unknown id → null, no error.
        var missing = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocument(id:\"no-such-id\") { id } }",
            userId: RepUserId,
            permissions: ReadPermission);
        missing.Should().NotContain("\"errors\"");
        missing.Should().Contain("\"salesRepDocument\":null");
    }

    [Fact]
    public async Task SalesRepDocumentCategories_ReturnsNamesWithCounts()
    {
        using var ctx = SalesRepTestContext.Create();
        await SeedLibraryAsync(ctx);
        await UploadAsync(ctx, "Fall Catalog.pdf", "Catalogs");

        var json = await ctx.ExecuteGraphQlAsync(
            "query { salesRepDocumentCategories { name count } }",
            userId: RepUserId,
            permissions: ReadPermission);

        var node = SalesRepTestContext.Node(json, "salesRepDocumentCategories");
        node.EnumerateArray()
            .Select(x => (x.GetProperty("name").GetString(), x.GetProperty("count").GetInt32()))
            .Should().Equal(("Catalogs", 2), ("Lookbooks", 1), ("Pricing", 1));
    }

    [Fact]
    public async Task AllQueries_WithoutDocumentsPermission_AreDeniedAndLeakNothing()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Secret.pdf", "Catalogs");

        // An authenticated rep whose roles carry only sales-rep:access — not documents:read.
        foreach (var query in AllQueries(document.Id))
        {
            var json = await ctx.ExecuteGraphQlAsync(query, userId: RepUserId, permissions: AccessOnlyPermission);

            json.Should().Contain("\"errors\"");
            json.Should().Contain(Permissions.DocumentsRead);
            json.Should().NotContain("Secret.pdf");
            json.Should().NotContain(document.Id);
        }
    }

    [Fact]
    public async Task AllQueries_Anonymous_AreDenied()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Secret.pdf", "Catalogs");

        foreach (var query in AllQueries(document.Id))
        {
            var json = await ctx.ExecuteGraphQlAnonymousAsync(query);

            json.Should().Contain("\"errors\"");
            json.Should().MatchRegex("(?i)anonym");
            json.Should().NotContain("Secret.pdf");
        }
    }

    [Fact]
    public async Task AllQueries_WithWriteOnlyPermission_AreAllowed()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Managed.pdf", "Catalogs");

        // Write implies read.
        foreach (var query in AllQueries(document.Id))
        {
            var json = await ctx.ExecuteGraphQlAsync(query, userId: RepUserId, permissions: WritePermission);

            json.Should().NotContain("\"errors\"");
        }
    }

    [Fact]
    public async Task AllQueries_AsAdministrator_AreAllowed()
    {
        using var ctx = SalesRepTestContext.Create();
        var document = await UploadAsync(ctx, "Managed.pdf", "Catalogs");

        // The platform Administrator role passes without any documents permission.
        foreach (var query in AllQueries(document.Id))
        {
            var json = await ctx.ExecuteGraphQlAsync(query, userId: "admin-user", isAdministrator: true);

            json.Should().NotContain("\"errors\"");
        }
    }

    private static string[] AllQueries(string documentId) =>
    [
        "query { salesRepDocuments { totalCount items { id name } } }",
        $"query {{ salesRepDocument(id:\"{documentId}\") {{ id name }} }}",
        "query { salesRepDocumentCategories { name count } }",
    ];

    /// <summary>Three documents in three categories with deterministic, distinct creation dates.</summary>
    private static async Task<(SalesRepDocument Oldest, SalesRepDocument Middle, SalesRepDocument Newest)> SeedLibraryAsync(SalesRepTestContext ctx)
    {
        var oldest = await UploadAsync(ctx, "Spring Catalog.pdf", "Catalogs");
        var middle = await UploadAsync(ctx, "Lookbook.pdf", "Lookbooks");
        var newest = await UploadAsync(ctx, "Price List.xlsx", "Pricing", new SalesRepDocumentMetadata { Summary = "Current prices", PageCount = 3 });

        await ctx.SetDocumentCreatedDateAsync(oldest.Id, Utc(2026, 1, 1));
        await ctx.SetDocumentCreatedDateAsync(middle.Id, Utc(2026, 2, 1));
        await ctx.SetDocumentCreatedDateAsync(newest.Id, Utc(2026, 3, 1));

        return (oldest, middle, newest);
    }

    private static async Task<SalesRepDocument> UploadAsync(
        SalesRepTestContext ctx,
        string fileName,
        string category,
        SalesRepDocumentMetadata metadata = null)
    {
        var service = ctx.GetRequiredService<ISalesRepDocumentService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("content"));
        return await service.UploadAsync(stream, fileName, category, metadata);
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
