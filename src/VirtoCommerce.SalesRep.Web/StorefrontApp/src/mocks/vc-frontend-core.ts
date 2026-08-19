// Test-only resolution target for "@vc-frontend/core" (see the vitest.config alias).
//
// The real package ships types only — `exports["."]` has no runtime `import`/`main` condition,
// because the host injects the implementation at runtime through MF shared singletons. Vite's
// resolver throws on that package.json before vi.mock() gets a chance to substitute the module,
// so tests need a real, resolvable file to alias to.
//
// It carries WORKING DEFAULTS rather than being empty, because a facade mock is wholesale: a spec
// that returns only `{ globals }` also erases every other facade symbol its subject's module graph
// imports — `SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT` first, since every hub read goes through
// `useSalesRepHubQuery`. So specs spread this file instead of replacing it:
//
//   vi.mock("@vc-frontend/core", async (importOriginal) => ({
//     ...(await importOriginal<Record<string, unknown>>()),
//     globals: { storeId: "test-store", cultureName: "en-US" },
//   }));
import { defineComponent, h } from "vue";
import type { PropType } from "vue";

/** The context key the host's error link reads to skip its generic toast. */
export const SUPPRESS_ERROR_NOTIFICATIONS_CONTEXT = { suppressErrorNotifications: true };

export const Logger = { error: () => {}, warn: () => {}, info: () => {}, debug: () => {} };

export const globals = {
  storeId: "test-store",
  cultureName: "en-US",
  currencyCode: "USD",
  i18n: undefined,
  router: undefined,
};

export const useUser = () => ({ checkPermissions: () => true });
export const useModuleSettings = () => ({ isEnabled: () => true, getModuleSettings: () => undefined });
export const useModal = () => ({ openModal: () => {}, closeModal: () => {} });
export const useNotifications = () => ({ success: () => {}, error: () => {}, warning: () => {}, info: () => {} });
export const useBreadcrumbs = (items: unknown) => items;
export const usePageHead = () => {};
export const useNavigations = () => ({ mergeMenuSchema: () => {}, registerAccountSection: () => {} });
export const useExtensionRegistry = () => ({ register: () => {} });
export const useWishlistSharingScopes = () => ({ registerSharingScope: () => {} });
export const registerCacheTypePolicies = () => {};
export const registerLocaleLoader = () => {};
export const getProductRoute = () => ({ name: "Product" });
export const EXTENSION_NAMES = { sharedList: { provenanceNote: "provenanceNote" } };
export const CORE_VERSION = "0.0.0-test";

/**
 * Renders its default slot under the real component's root class — specs locate a kit component by
 * that class, so a classless wrapper reads as "the component never rendered".
 */
function passthrough(name: string, rootClass: string) {
  return defineComponent({
    name,
    setup:
      (_props, { slots }) =>
      () =>
        h("div", { class: rootClass }, slots.default?.()),
  });
}

export const VcButton = passthrough("VcButton", "vc-button");

/**
 * Mirrors the real widget's SLOT STRUCTURE and the two class names specs query, so a spec can keep
 * asserting on the module's own chrome where it actually renders — inside the widget's header and
 * body slots. A default-slot-only stub silently drops everything the module passes by name.
 */
export const VcWidget = defineComponent({
  name: "VcWidget",
  props: { title: { type: String, default: "" } },
  setup:
    (props, { slots }) =>
    () =>
      h("div", { class: "vc-widget" }, [
        h(
          "div",
          { class: "vc-widget__header-container" },
          slots["header-container"]?.() ??
            slots.header?.() ?? [
              slots.prepend?.(),
              h("div", { class: "vc-widget__title" }, slots.title?.() ?? props.title),
              slots.append?.(),
            ],
        ),
        slots["default-container"]?.() ?? slots.default?.(),
        slots["footer-container"]?.() ?? slots.footer?.(),
      ]),
});
export const VcWidgetSkeleton = passthrough("VcWidgetSkeleton", "vc-widget-skeleton");
export const VcModal = passthrough("VcModal", "vc-modal");

export const VcInput = defineComponent({
  name: "VcInput",
  props: { modelValue: { type: [String, Number] as PropType<string | number>, default: "" } },
  emits: ["update:modelValue"],
  setup:
    (props, { emit }) =>
    () =>
      h("input", {
        value: props.modelValue,
        onInput: (event: Event) => emit("update:modelValue", (event.target as HTMLInputElement).value),
      }),
});

export const VcCheckbox = defineComponent({
  name: "VcCheckbox",
  props: { modelValue: { type: Boolean, default: false } },
  emits: ["update:modelValue"],
  setup:
    (props, { emit }) =>
    () =>
      h("input", {
        type: "checkbox",
        checked: props.modelValue,
        onChange: (event: Event) => emit("update:modelValue", (event.target as HTMLInputElement).checked),
      }),
});

export const OrderStatus = defineComponent({
  name: "OrderStatus",
  props: { status: { type: String, default: "" }, displayValue: { type: String, default: "" } },
  setup: (props) => () => h("span", props.displayValue || props.status),
});
