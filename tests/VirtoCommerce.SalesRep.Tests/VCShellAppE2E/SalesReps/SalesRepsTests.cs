using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Playwright;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Tests.VCShellAppE2E.Infrastructure;
using Xunit;

namespace VirtoCommerce.SalesRep.Tests.VCShellAppE2E.SalesReps;

/// <summary>
/// The sales-rep admin app (VC-Shell) driven by Playwright against the in-process backend: an administrator creates
/// and blocks reps through the blades, the tests assert on the rows the module wrote.
/// </summary>
[Collection(SalesRepsCollection.Name)]
[Trait("Category", "VCShellAppE2E")]
public sealed class SalesRepsTests(SalesRepsEnvironment env)
{
    private const string ListRoute = "sales-reps";

    [Fact(Timeout = 300_000, Skip = VCShellAppAvailability.SkipReason, SkipUnless = nameof(VCShellAppAvailability.IsAvailable), SkipType = typeof(VCShellAppAvailability))]
    public async Task Details_blade_creates_a_rep_with_account_and_memberships()
    {
        const string email = "sam.seller@e2e.local";

        await using var session = await env.OpenAsync(env.AdminUserId, ListRoute);

        await session.RunAsync(nameof(Details_blade_creates_a_rep_with_account_and_memberships), async page =>
        {
            // The list blade shows the seeded rep before anything is clicked.
            await Assertions.Expect(page.GetByText(SalesRepsEnvironment.RitaEmail)).ToBeVisibleAsync();

            await page.GetByTestId("add").ClickAsync();
            await Assertions.Expect(page.GetByText("New Sales Rep")).ToBeVisibleAsync();

            await page.GetByLabel("Email (login)").FillAsync(email);
            // Exact: the field's show/hide button is labelled "Show password".
            await page.GetByLabel("Password", new PageGetByLabelOptions { Exact = true }).FillAsync("P@ssw0rd123!");
            await SelectAsync(page, "Store", "E2E store");
            await SelectAsync(page, "Sales Rep role", "Sales Rep");
            await SelectAsync(page, "Organizations served as Sales Rep", "Fabrikam");
            await page.GetByLabel("First name").FillAsync("Sam");
            await page.GetByLabel("Last name").FillAsync("Seller");

            var created = page.WaitForResponseAsync(response => response.Url.EndsWith("/api/sales-rep") && response.Request.Method == "POST");
            await page.GetByTestId("save").ClickAsync();
            (await created).Status.Should().Be(200);
        });

        // The module wrote the account, the contact and the membership; the tests read them back from the harness.
        using var userManager = env.Context.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var account = await userManager.FindByEmailAsync(email);
        account.Should().NotBeNull();
        account.MemberId.Should().NotBeNullOrEmpty();

        var contact = await env.Context.GetRequiredService<IMemberService>().GetByIdAsync(account.MemberId) as Contact;
        contact.Should().NotBeNull();
        contact.FirstName.Should().Be("Sam");
        contact.LastName.Should().Be("Seller");

        var memberships = await env.Context.GetMembershipsAsync(account.Id);
        memberships.Should().ContainSingle(membership => membership.OrganizationId == SalesRepsEnvironment.FabrikamId);
    }

    [Fact(Timeout = 300_000, Skip = VCShellAppAvailability.SkipReason, SkipUnless = nameof(VCShellAppAvailability.IsAvailable), SkipType = typeof(VCShellAppAvailability))]
    public async Task Details_blade_blocks_a_rep_account()
    {
        await using var session = await env.OpenAsync(env.AdminUserId, ListRoute);

        await session.RunAsync(nameof(Details_blade_blocks_a_rep_account), async page =>
        {
            await page.GetByText(SalesRepsEnvironment.RitaEmail).ClickAsync();
            await Assertions.Expect(page.GetByLabel("Email (login)")).ToHaveValueAsync(SalesRepsEnvironment.RitaEmail);

            var blocked = page.WaitForResponseAsync(response => response.Url.EndsWith($"/api/sales-rep/{env.Rita.Id}/block") && response.Request.Method == "POST");
            await page.GetByTestId("block").ClickAsync();
            (await blocked).Status.Should().Be(204);
        });

        using var userManager = env.Context.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var account = await userManager.FindByIdAsync(env.Rita.UserId);
        (await userManager.IsLockedOutAsync(account)).Should().BeTrue();
    }

    // A VcSelect: its trigger is a combobox named by the label, its choices are options.
    private static async Task SelectAsync(IPage page, string label, string option)
    {
        await page.GetByRole(AriaRole.Combobox, new PageGetByRoleOptions { Name = label }).ClickAsync();
        await page.GetByRole(AriaRole.Option, new PageGetByRoleOptions { Name = option, Exact = true }).ClickAsync();
    }
}
