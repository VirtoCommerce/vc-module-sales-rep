import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { mount } from "@vue/test-utils";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { parse } from "vue/compiler-sfc";
import SalesRepTaskFilters from "../components/sales-rep-task-filters.vue";
import { emptyTaskFilters } from "../tasks";
import { cascadedValue, compiledStyleRules, toPx } from "../test-utils/compiled-style";
import type { CompiledRuleType } from "../test-utils/compiled-style";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

const TOOLBAR = "sales-rep-tasks-page__toolbar";
const SEARCH = "sales-rep-tasks-page__search-input";

// The narrowest the search box may get beside the filters.
const SEARCH_MIN_WIDTH = 240;

// The content column's width at each viewport, measured on the stand; the last two are boundary cases around a fit.
const CONTENT = [
  { viewport: 640, content: 577 },
  { viewport: 678, content: 615 },
  { viewport: 700, content: 637 },
  { viewport: 767, content: 704 },
  { viewport: 768, content: 485 },
  { viewport: 898, content: 615 },
  { viewport: 900, content: 617 },
  { viewport: 1024, content: 725 },
  { viewport: 1280, content: 941 },
  { viewport: 1500, content: 867 },
  { viewport: 1500, content: 868 },
];

// The classes of the toolbar's ancestors in the page template, nearest first.
function toolbarAncestors(): string[][] {
  const file = resolve(dirname(fileURLToPath(import.meta.url)), "sales-rep-tasks-page.vue");
  const template = document.createElement("template");
  template.innerHTML = parse(readFileSync(file, "utf8")).descriptor.template?.content ?? "";

  const ancestors: string[][] = [];
  let element = template.content.querySelector(`.${TOOLBAR}`)?.parentElement;
  while (element) {
    ancestors.push([...element.classList]);
    element = element.parentElement;
  }
  return ancestors;
}

/**
 * Lays out the toolbar at a viewport from the compiled CSS of the page and the filters: whether search and filters
 * share a line, how wide the search is then, and how many selects sit on each line of the filters.
 */
function toolbarLayout(page: CompiledRuleType[], filters: CompiledRuleType[], viewport: number, content: number) {
  // Every element in the chain is a full-width block, so a query container there is as wide as the content column.
  const isContainer = (classes: string[]) => cascadedValue(page, classes, "container-type", viewport) === "inline-size";
  const toolbarContainer = toolbarAncestors().some(isContainer) ? content : undefined;
  const filtersContainer = isContainer([TOOLBAR]) ? content : toolbarContainer;

  const pageValue = (classes: string[], property: string) =>
    cascadedValue(page, classes, property, viewport, toolbarContainer);
  const filtersValue = (classes: string[], property: string) =>
    cascadedValue(filters, classes, property, viewport, filtersContainer);

  const wrapper = mount(SalesRepTaskFilters, {
    props: { modelValue: emptyTaskFilters(), statuses: [], customers: [] },
    global: { stubs: { VcSelect: { template: '<div class="select" />' } } },
  });
  const filtersRow = wrapper.classes();
  const widths = wrapper.findAll(".select").map((select) => toPx(filtersValue(select.classes(), "width")) ?? content);
  const filtersGap = toPx(filtersValue(filtersRow, "gap")) ?? 0;
  const filtersSideBySide = (filtersValue(filtersRow, "flex-direction") ?? "row") === "row";
  const filtersWidth = widths.reduce((sum, width) => sum + width, 0) + filtersGap * (widths.length - 1);

  // What the filters take side by side, read from a container wide enough for every query to apply, so the expected
  // layout does not depend on the page's queries resolving.
  const wideValue = (classes: string[], property: string) =>
    cascadedValue(filters, classes, property, viewport, Infinity);
  const wideWidths = wrapper.findAll(".select").map((select) => toPx(wideValue(select.classes(), "width")) ?? 0);
  const wideFiltersWidth =
    wideWidths.reduce((sum, width) => sum + width, 0) +
    (toPx(wideValue(filtersRow, "gap")) ?? 0) * (wideWidths.length - 1);

  const toolbarGap = toPx(pageValue([TOOLBAR], "gap")) ?? 0;
  const searchBeside = (pageValue([TOOLBAR], "flex-direction") ?? "row") === "row";
  // Beside the search the filters may take everything but the gap; below it, the whole line.
  const room = searchBeside ? content - toolbarGap : content;

  // Line by line, as flex lays the selects out: wrapped when allowed and the next one no longer fits.
  const lines: number[] = [];
  if (!filtersSideBySide) {
    widths.forEach(() => lines.push(1));
  } else if (filtersValue(filtersRow, "flex-wrap") === "wrap") {
    let used = Infinity;
    for (const width of widths) {
      if (used + filtersGap + width > room) {
        lines.push(1);
        used = width;
      } else {
        lines[lines.length - 1] += 1;
        used += filtersGap + width;
      }
    }
  } else {
    lines.push(widths.length);
  }

  return {
    searchBeside,
    searchWidth: searchBeside ? room - Math.min(filtersWidth, room) : content,
    filtersKeepWidths: filtersValue(filtersRow, "flex-shrink") === "0",
    filtersOverflow: lines.length === 1 && lines[0] > 1 && filtersWidth > room,
    lines,
    filtersFit: content >= wideFiltersWidth,
    fitsOnOneLine: content >= SEARCH_MIN_WIDTH + toolbarGap + wideFiltersWidth,
    searchGrows: pageValue([SEARCH], "flex-grow") === "1" && toPx(pageValue([SEARCH], "min-width")) === 0,
  };
}

describe("SalesRepTasksPage toolbar layout", () => {
  let page: CompiledRuleType[];
  let filters: CompiledRuleType[];

  beforeAll(async () => {
    [page, filters] = await Promise.all([
      compiledStyleRules("pages/sales-rep-tasks-page.vue"),
      compiledStyleRules("components/sales-rep-task-filters.vue"),
    ]);
  }, 30_000);

  it.each(CONTENT)(
    "sets the filters on one line where they fit and one per line where they do not, at $viewport px ($content px)",
    ({ viewport, content }) => {
      const { lines, filtersFit } = toolbarLayout(page, filters, viewport, content);

      expect(lines).toEqual(filtersFit ? [3] : [1, 1, 1]);
    },
  );

  it.each(CONTENT)("keeps the filters inside the content at $viewport px ($content px)", ({ viewport, content }) => {
    expect(toolbarLayout(page, filters, viewport, content).filtersOverflow).toBe(false);
  });

  it.each(CONTENT)(
    "puts the search beside the filters only when both fit, at $viewport px ($content px)",
    ({ viewport, content }) => {
      const layout = toolbarLayout(page, filters, viewport, content);

      expect(layout.searchBeside).toBe(layout.fitsOnOneLine);
      if (layout.searchBeside) {
        expect(layout.filtersKeepWidths).toBe(true);
        expect(layout.searchGrows).toBe(true);
        expect(layout.searchWidth).toBeGreaterThanOrEqual(SEARCH_MIN_WIDTH);
        expect(layout.lines).toEqual([3]);
      }
    },
  );
});
