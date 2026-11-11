using System.Threading.Tasks;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>The layout editing every hub page with stats and widgets offers, by role and label.</summary>
internal static class LayoutControls
{
    /// <summary>A toggle, shown once the layout has been read and pressed while editing.</summary>
    public static ILocator EditLayout(IPage page) => page.GetByRole(AriaRole.Button, new() { Name = "Edit layout", Exact = true });

    /// <summary>
    /// Hides a stat in edit mode the way a keyboard user does: Space picks it up and Down parks it among the hidden ones.
    /// </summary>
    public static async Task HideStatAsync(IPage page, string title)
    {
        await page.GetByRole(AriaRole.Button, new() { Name = $"Reorder {title}", Exact = true }).FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.Keyboard.PressAsync("ArrowDown");
        await Expect(page.GetByRole(AriaRole.Group, new() { Name = "Hidden stats", Exact = true })
            .GetByRole(AriaRole.Button, new() { Name = $"Reorder {title}", Exact = true })).ToBeVisibleAsync();
    }

    /// <summary>
    /// Saves the edited layout. Edit mode ends only once the backend has stored the document, a failed save keeps it,
    /// so the edit bar going away is the save's success; the Edit layout toggle stays on screen throughout.
    /// </summary>
    public static async Task SaveLayoutAsync(IPage page)
    {
        var save = page.GetByRole(AriaRole.Button, new() { Name = "Save layout", Exact = true });
        await save.ClickAsync();
        await Expect(save).ToHaveCountAsync(0, new() { Timeout = 30_000 });
    }
}
