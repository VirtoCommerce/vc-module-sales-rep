import { beforeEach, describe, expect, it, vi } from "vitest";
import { MIN_ROWS } from "@vc-frontend/core";
import {
  CUSTOMER_PROFILE_LAYOUT_SCOPE,
  DASHBOARD_LAYOUT_SCOPE,
  DOCUMENTS_BLOCK_ID,
  DOCUMENTS_DEFAULT_ROWS,
  DOCUMENTS_MAX_ROWS,
  TASKS_BLOCK_ID,
  TASKS_DEFAULT_ROWS,
  TASKS_MAX_ROWS,
} from "../constants";
import { customerProfileBlocks, dashboardBlocks, registerHubBlocks } from "./blocks";
import { customerActivityBlock } from "./customer-activity-block";
import { documentsBlock } from "./documents-block";
import { tasksBlock } from "./tasks-block";
import type { BlockType } from "@vc-frontend/core";

// Block ids handed to the host's registry, per scope.
const registered = vi.hoisted(() => new Map<string, string[]>());

vi.mock("@vc-frontend/core", async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  registerBlock: (scope: string, block: { id: string }) => {
    registered.set(scope, [...(registered.get(scope) ?? []), block.id]);
  },
}));

// Every block a scope can hold: its own table plus the blocks init() adds while their feature is on.
const surfaces: [string, readonly BlockType[]][] = [
  [DASHBOARD_LAYOUT_SCOPE, [...dashboardBlocks, tasksBlock, documentsBlock]],
  [CUSTOMER_PROFILE_LAYOUT_SCOPE, [...customerProfileBlocks, customerActivityBlock]],
];

beforeEach(() => {
  registered.clear();
});

// Regions and components are the facade's types to enforce; what they cannot see is which table lands in which
// scope and whether two blocks of one scope share an id.
describe("hub blocks", () => {
  it("registers each table into its own scope, in table order", () => {
    registerHubBlocks();

    expect(registered.get(DASHBOARD_LAYOUT_SCOPE)).toEqual(dashboardBlocks.map((block) => block.id));
    expect(registered.get(CUSTOMER_PROFILE_LAYOUT_SCOPE)).toEqual(customerProfileBlocks.map((block) => block.id));
  });

  // The host keeps the first block of an id and only logs the second, so a repeat would silently drop a widget.
  it.each(surfaces)("%s: has unique block ids, the conditional blocks included", (_scope, blocks) => {
    const ids = blocks.map((block) => block.id);

    expect(new Set(ids).size).toBe(ids.length);
  });
});

// Registered by init() only while their feature is on (documents:read, the tasks module), so their shape is
// pinned on the exported definitions.
describe("conditional blocks", () => {
  it("puts the documents widget in the right rail with its row cap", () => {
    expect(documentsBlock).toMatchObject({
      id: DOCUMENTS_BLOCK_ID,
      region: "mainRight",
      settings: [{ kind: "maxRows", default: DOCUMENTS_DEFAULT_ROWS, min: MIN_ROWS, max: DOCUMENTS_MAX_ROWS }],
    });
  });

  it("puts the tasks widget in the right rail with its row cap", () => {
    expect(tasksBlock).toMatchObject({
      id: TASKS_BLOCK_ID,
      region: "mainRight",
      settings: [{ kind: "maxRows", default: TASKS_DEFAULT_ROWS, min: MIN_ROWS, max: TASKS_MAX_ROWS }],
    });
  });
});
