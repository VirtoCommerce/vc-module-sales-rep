import { mount } from "@vue/test-utils";
import { describe, expect, it, vi } from "vitest";
import SalesRepTaskAction from "./sales-rep-task-action.vue";
import type { SalesRepTaskStatusType, SalesRepTaskType } from "../types/tasks";

// Keys, not prose: the assertions are about which action rendered, and the params it composed.
vi.mock("vue-i18n", () => ({
  useI18n: () => ({
    t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${JSON.stringify(params)}` : key),
  }),
}));

function makeTask(status: SalesRepTaskStatusType): SalesRepTaskType {
  return {
    id: "task-1",
    name: "Call ACME",
    description: "",
    type: "Finance",
    priority: "Normal",
    dueDate: "2026-10-15T00:00:00Z",
    isActive: status === "upcoming" || status === "overdue",
    completed: status === "completed" ? true : undefined,
    status,
  };
}

function createWrapper(status: SalesRepTaskStatusType, disabled = false) {
  return mount(SalesRepTaskAction, {
    props: { task: makeTask(status), disabled },
    global: {
      stubs: {
        VcButton: {
          props: ["disabled"],
          emits: ["click"],
          template: '<button :disabled="disabled" @click="$emit(\'click\')"><slot name="prepend" /><slot /></button>',
        },
        VcIcon: { props: ["name", "color"], template: '<i :data-name="name" :data-color="color" />' },
      },
    },
  });
}

describe("SalesRepTaskAction", () => {
  it.each(["upcoming", "overdue"] as const)("offers to complete an %s task", (status) => {
    const wrapper = createWrapper(status);

    expect(wrapper.text()).toBe("sales_rep.tasks.table.mark_complete");
    expect(wrapper.find("i").attributes()).toMatchObject({ "data-name": "circle-check-big", "data-color": "success" });
  });

  it("offers to reopen a completed task", () => {
    const wrapper = createWrapper("completed");

    expect(wrapper.text()).toBe("sales_rep.tasks.table.reopen");
    expect(wrapper.find("i").attributes()).toMatchObject({ "data-name": "rotate-ccw", "data-color": "info" });
  });

  // Closed in the admin app without being done: neither completing nor reopening describes undoing that.
  it("offers nothing on a canceled task", () => {
    const wrapper = createWrapper("canceled");

    expect(wrapper.find("button").exists()).toBe(false);
  });

  // The visible label repeats on every row, so the name adds the task — led by the label (WCAG 2.5.3).
  it("names the task after the visible label", () => {
    const wrapper = createWrapper("completed");

    expect(wrapper.find("button").attributes("aria-label")).toBe(
      `sales_rep.tasks.table.action_aria ${JSON.stringify({ action: "sales_rep.tasks.table.reopen", name: "Call ACME" })}`,
    );
  });

  it("emits toggle on click", async () => {
    const wrapper = createWrapper("upcoming");

    await wrapper.find("button").trigger("click");

    expect(wrapper.emitted("toggle")).toHaveLength(1);
  });

  it("holds still while a write is in flight", () => {
    const wrapper = createWrapper("upcoming", true);

    expect(wrapper.find("button").attributes("disabled")).toBeDefined();
    // No colour of its own, so the glyph greys with the disabled button rather than staying lit.
    expect(wrapper.find("i").attributes("data-color")).toBeUndefined();
  });
});

// The Tasks table's Actions column is too narrow for the label (VCST-6157): the glyph alone, the label its tooltip.
describe("SalesRepTaskAction icon only", () => {
  const IconButtonStub = {
    props: { disabled: Boolean, icon: [Boolean, String] },
    emits: ["click"],
    template:
      '<button :data-icon="String(icon)" :disabled="disabled" @click="$emit(\'click\')"><slot name="prepend" /><slot /></button>',
  };

  function createIconWrapper(iconOnly: boolean) {
    return mount(SalesRepTaskAction, {
      props: { task: makeTask("upcoming"), iconOnly },
      global: {
        stubs: {
          VcButton: IconButtonStub,
          VcIcon: { props: ["name", "color"], template: '<i :data-name="name" :data-color="color" />' },
        },
      },
    });
  }

  it("draws the glyph alone, with the label as its tooltip", () => {
    const button = createIconWrapper(true).get("button");

    expect(button.attributes("data-icon")).toBe("true");
    expect(button.text()).toBe("");
    expect(button.attributes("title")).toBe("sales_rep.tasks.table.mark_complete");
    expect(button.find("i").attributes("data-name")).toBe("circle-check-big");
  });

  // The accessible name does not depend on what is drawn.
  it("keeps the accessible name either way", () => {
    expect(createIconWrapper(true).get("button").attributes("aria-label")).toBe(
      createIconWrapper(false).get("button").attributes("aria-label"),
    );
  });

  it("keeps the label, and no tooltip, by default", () => {
    const button = createIconWrapper(false).get("button");

    expect(button.attributes("data-icon")).toBe("false");
    expect(button.text()).toBe("sales_rep.tasks.table.mark_complete");
    expect(button.attributes("title")).toBeUndefined();
  });
});
