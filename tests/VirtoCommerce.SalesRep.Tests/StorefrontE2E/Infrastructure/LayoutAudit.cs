using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The layout checks of QA's overflow audit (ai-tools <c>scripts/lib/measure-layout.ts</c>, rule BL-UI-004), run at
/// each of QA's widths: the page never scrolls sideways, and no text is cut off by <c>overflow: hidden</c> without an
/// ellipsis. Both compare an element with its own box, never with a pixel value, so they hold on any machine that renders
/// the same storefront build in the same font.
/// </summary>
internal static class LayoutAudit
{
    /// <summary>
    /// QA's audit widths (ai-tools <c>scripts/lib/design-tokens.generated.ts</c>, <c>AUDIT_VIEWPORTS_PX</c>): each ui-kit
    /// breakpoint (480, 640, 768, 1024, 1280, 1500), the pixel below it, the middles of the ranges between them, and the
    /// device widths 375 and 1920.
    /// </summary>
    public static IReadOnlyList<int> Widths { get; } = [375, 479, 480, 560, 639, 640, 704, 767, 768, 896, 1023, 1024, 1152, 1279, 1280, 1390, 1499, 1500, 1920];

    private const int Height = 900;

    // Screenshots of the first widths that failed, enough to see the pattern without one per width.
    private const int Screenshots = 3;

    /// <summary>
    /// Makes the page measurable: no transitions or animations, so every box is where it ends up, and the storefront's
    /// own font loaded. A fallback font changes the width of every text, and the fallbacks differ by OS. Call it once the
    /// page shows its content, since the font starts loading only when text needs it.
    /// </summary>
    public static async Task PrepareAsync(IPage page)
    {
        await page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });
        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = "*, *::before, *::after { transition: none !important; animation: none !important; }" });

        var font = await page.EvaluateAsync<JsonElement>(FontScript);
        if (!font.GetProperty("loaded").GetBoolean())
        {
            throw new InvalidOperationException($"The storefront's font \"{font.GetProperty("family").GetString()}\" did not load, so text widths would be the OS fallback's.");
        }
    }

    /// <summary>
    /// Checks the page at each of QA's widths and returns what broke, one line per problem, starting with the width.
    /// The sideways scroll is checked on the whole document; clipped text only inside <paramref name="scopes"/> (the
    /// page's root, an open popup), since the theme's header and menus are not this module's. <paramref name="restoreAsync"/>
    /// runs after each resize, for a popup a resize may close. The first widths that fail leave a full-page screenshot,
    /// named after <paramref name="name"/> and the width, in the diagnostics folder.
    /// </summary>
    public static async Task<IReadOnlyList<string>> SweepAsync(IPage page, string name, IReadOnlyList<string> scopes, Func<Task> restoreAsync = null)
    {
        var problems = new List<string>();
        var screenshots = 0;

        foreach (var width in Widths)
        {
            await page.SetViewportSizeAsync(width, Height);

            if (restoreAsync != null)
            {
                await restoreAsync();
            }

            await page.EvaluateAsync(SettleScript);

            var found = Describe(await page.EvaluateAsync<JsonElement>(AuditScript, scopes));
            if (found.Count == 0)
            {
                continue;
            }

            problems.AddRange(found.Select(x => $"{width} px: {x}"));

            if (screenshots++ < Screenshots)
            {
                Directory.CreateDirectory(StorefrontEnvironment.DiagnosticsDirectory);
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(StorefrontEnvironment.DiagnosticsDirectory, $"{name}-{width}px.png"), FullPage = true });
            }
        }

        return problems;
    }

    private static List<string> Describe(JsonElement audit)
    {
        var problems = new List<string>();

        var clientWidth = audit.GetProperty("clientWidth").GetInt32();
        var scrollWidth = audit.GetProperty("scrollWidth").GetInt32();
        if (scrollWidth > clientWidth + 1)
        {
            var widest = string.Join(", ", audit.GetProperty("overflowing").EnumerateArray()
                .Select(x => $"{x.GetProperty("element").GetString()} “{x.GetProperty("text").GetString()}” ends at {x.GetProperty("right").GetInt32()}"));
            problems.Add($"the page scrolls sideways, {scrollWidth} px of content in {clientWidth}: {widest}");
        }

        problems.AddRange(audit.GetProperty("clipped").EnumerateArray().Select(x =>
            $"{x.GetProperty("element").GetString()} cuts its text off without an ellipsis, {x.GetProperty("content").GetInt32()} px of " +
            $"{x.GetProperty("axis").GetString()} in {x.GetProperty("box").GetInt32()}: “{x.GetProperty("text").GetString()}”"));

        return problems;
    }

    // The storefront's own font: the first family of the body's font stack. check() is true for a family that has no
    // @font-face (a system font) and for one that loaded; false for one still loading or that failed.
    private const string FontScript = """
        async () => {
          await document.fonts.ready;
          const family = getComputedStyle(document.body).fontFamily.split(',')[0].trim().replace(/^["']|["']$/g, '');
          return { family, loaded: document.fonts.check(`16px "${family}"`) };
        }
        """;

    // A resize re-lays the page at once, but some components follow it a frame or more later (the ones that watch the
    // breakpoints, a dropdown that repositions). Waits until the page's size holds still for three frames, and at least
    // 200 ms, then for any font the new layout asked for.
    private const string SettleScript = """
        async () => {
          const frame = () => new Promise(resolve => requestAnimationFrame(() => resolve()));
          const size = () => {
            const doc = document.documentElement;
            return `${doc.clientWidth} ${doc.scrollWidth} ${doc.scrollHeight}`;
          };
          const start = performance.now();
          let last = '';
          let stable = 0;
          for (let i = 0; i < 120 && (stable < 3 || performance.now() - start < 200); i++) {
            await frame();
            const now = size();
            stable = now === last ? stable + 1 : 0;
            last = now;
          }
          await document.fonts.ready;
        }
        """;

    // BL-UI-004. The sideways scroll is measured against the document's client width, which leaves out a vertical
    // scrollbar; when the page scrolls, it names the innermost HTML elements that reach furthest right. Clipped text: an
    // element whose overflow is hidden or clip in an axis its content outgrows, unless the clip shows an ellipsis
    // (text-overflow or line-clamp), the element is a screen reader's 1 px box, or it holds no text.
    private const string AuditScript = """
        scopes => {
          const doc = document.documentElement;
          const describe = el => el.tagName.toLowerCase()
            + (typeof el.className === 'string' && el.className.trim() ? '.' + el.className.trim().split(/\s+/).slice(0, 2).join('.') : '');
          const result = { clientWidth: doc.clientWidth, scrollWidth: doc.scrollWidth, overflowing: [], clipped: [] };

          if (doc.scrollWidth > doc.clientWidth + 1) {
            // How far an element reaches: its box, or its content where that spills out of the box (text that
            // does not wrap overflows a box that itself fits), unless the element clips or scrolls its own content.
            // HTML elements only: the parts of an icon's drawing say nothing about which control it is.
            const reach = el => {
              const box = el.getBoundingClientRect();
              const spills = getComputedStyle(el).overflowX === 'visible';
              return spills ? Math.max(box.right, box.left + el.scrollWidth) : box.right;
            };
            // What an ancestor clips, or what is fixed to the viewport, cannot make the page scroll.
            const contained = el => {
              if (getComputedStyle(el).position === 'fixed') {
                return true;
              }
              for (let parent = el.parentElement; parent && parent !== document.body; parent = parent.parentElement) {
                if (['hidden', 'clip'].includes(getComputedStyle(parent).overflowX) && parent.getBoundingClientRect().right <= doc.clientWidth + 1) {
                  return true;
                }
              }
              return false;
            };
            // An element reaching past the page's own scroll width is clipped by something, so it is not the cause either.
            const out = [...document.body.querySelectorAll('*')]
              .filter(el => el instanceof HTMLElement && reach(el) > doc.clientWidth + 1 && reach(el) <= doc.scrollWidth + 1 && !contained(el));
            // Each culprit with the nearest element of this module's plugin, which says whose layout it is.
            const owner = el => el.parentElement?.closest('[class*="sales-rep-"]');
            result.overflowing = out
              .filter(el => !out.some(other => other !== el && el.contains(other)))
              .sort((a, b) => reach(b) - reach(a))
              .slice(0, 3)
              .map(el => ({
                element: describe(el) + (owner(el) ? ' in ' + describe(owner(el)) : ''),
                text: (el.innerText || '').trim().slice(0, 40),
                right: Math.round(reach(el)),
              }));
          }

          const seen = new Set();
          for (const root of scopes.flatMap(scope => [...document.querySelectorAll(scope)])) {
            for (const el of [root, ...root.querySelectorAll('*')]) {
              if (seen.has(el)) {
                continue;
              }
              seen.add(el);

              const style = getComputedStyle(el);
              const clipsX = ['hidden', 'clip'].includes(style.overflowX) && el.scrollWidth > el.clientWidth + 1;
              const clipsY = ['hidden', 'clip'].includes(style.overflowY) && el.scrollHeight > el.clientHeight + 1;
              if (!clipsX && !clipsY) {
                continue;
              }

              const box = el.getBoundingClientRect();
              const ellipsis = style.textOverflow === 'ellipsis' || style.getPropertyValue('-webkit-line-clamp') !== 'none';
              const text = (el.innerText || '').trim();
              if (box.width <= 1 || box.height <= 1 || ellipsis || !text) {
                continue;
              }

              result.clipped.push({
                element: describe(el),
                axis: clipsX ? 'width' : 'height',
                content: clipsX ? el.scrollWidth : el.scrollHeight,
                box: clipsX ? el.clientWidth : el.clientHeight,
                text: text.slice(0, 60),
              });
              if (result.clipped.length >= 20) {
                return result;
              }
            }
          }

          return result;
        }
        """;
}
