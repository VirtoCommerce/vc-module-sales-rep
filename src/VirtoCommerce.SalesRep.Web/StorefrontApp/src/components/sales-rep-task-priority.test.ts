import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import SalesRepTaskPriority from "./sales-rep-task-priority.vue";

vi.mock("vue-i18n", () => ({ useI18n: () => ({ t: (key: string) => key }) }));

const dot = (priority: string) =>
  mount(SalesRepTaskPriority, { props: { priority } }).get(".sales-rep-task-priority__dot").classes();

describe("SalesRepTaskPriority", () => {
  it("names the priority", () => {
    expect(mount(SalesRepTaskPriority, { props: { priority: "Highest" } }).text()).toBe(
      "sales_rep.tasks.priority.highest",
    );
  });

  // The mockup's three colours for five priorities.
  it.each([
    ["Highest", "high"],
    ["High", "high"],
    ["Normal", "normal"],
    ["Low", "low"],
    ["Lowest", "low"],
  ])("colours %s as %s", (priority, level) => {
    expect(dot(priority)).toContain(`sales-rep-task-priority__dot--${level}`);
  });

  // The label carries the meaning; the dot only repeats it.
  it("hides the dot from assistive tech", () => {
    const wrapper = mount(SalesRepTaskPriority, { props: { priority: "High" } });

    expect(wrapper.get(".sales-rep-task-priority__dot").attributes("aria-hidden")).toBe("true");
  });
});
