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
import { parseDate } from "@internationalized/date";
import { computed, defineComponent, h, unref } from "vue";
import type { CalendarDate } from "@internationalized/date";
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
export const useBreadcrumbs = (sources: unknown) =>
  computed(() => (typeof sources === "function" ? (sources as () => unknown)() : unref(sources)));
export const usePageHead = () => {};
export const useNavigations = () => ({ mergeMenuSchema: () => {}, registerAccountSection: () => {} });
export const useExtensionRegistry = () => ({ register: () => {}, registerContribution: () => {} });
export const useWishlistSharingScopes = () => ({ registerSharingScope: () => {} });
export const registerCacheTypePolicies = () => {};
export const registerLocaleLoader = () => {};
export const getProductRoute = () => ({ name: "Product" });
export const EXTENSION_NAMES = { sharedList: { provenanceNote: "provenanceNote" } };
export const ROUTES = {
  ACCOUNT: { NAME: "Account", PATH: "/account" },
  COMPANY: { NAME: "Company", PATH: "/company" },
};
export const CORE_VERSION = "0.0.0-test";

/** The host's breakpoint scale (ui-kit/constants/tailwind.ts). */
export const BREAKPOINTS = { xs: "480px", sm: "640px", md: "768px", lg: "1024px", xl: "1280px", "2xl": "1500px" };

/** The host's `ContentType` enum (core/enums/content-type.enum.ts), as the object it compiles to. */
export const ContentType = {
  "image/jpeg": "image/jpeg",
  "image/png": "image/png",
  "application/pdf": "application/pdf",
  "application/msword": "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document": "application/msword",
  "application/vnd.ms-excel": "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": "application/vnd.ms-excel",
  "application/zip": "application/zip",
};

/** The host's ISO-date parsing and the date fields' display format (ui-kit/utilities/date.ts). */
export function tryParseDate(value: string | undefined): CalendarDate | undefined {
  try {
    return value ? parseDate(value.split("T")[0]) : undefined;
  } catch {
    return undefined;
  }
}

export function formatDateLocale(value: CalendarDate | null | undefined, locale: string): string {
  if (!value) {
    return "";
  }
  const date = new Date(Date.UTC(value.year, value.month - 1, value.day));
  return new Intl.DateTimeFormat(locale, { year: "numeric", month: "2-digit", day: "2-digit", timeZone: "UTC" }).format(
    date,
  );
}

/** The host's date-filter bounds (core/utilities/date): local midnight, and the last millisecond of that day. */
export function toStartDateFilterValue(dateOnly?: string): string | undefined {
  return dateOnly ? new Date(Date.parse(`${dateOnly}T00:00:00.000`)).toISOString() : undefined;
}

export function toEndDateFilterValue(dateOnly?: string): string | undefined {
  if (!dateOnly) {
    return undefined;
  }
  const date = new Date(Date.parse(`${dateOnly}T00:00:00.000`));
  date.setDate(date.getDate() + 1);
  return new Date(date.getTime() - 1).toISOString();
}

export function toLocalDateOnly(date: Date): string {
  return `${date.getFullYear()}-${`${date.getMonth() + 1}`.padStart(2, "0")}-${`${date.getDate()}`.padStart(2, "0")}`;
}

/** The host's file-size split (ui-kit/utilities/file-size.ts). */
export function getFileSize(bytes?: number): { value: number; unit: string } {
  const units = ["byte", "kilobyte", "megabyte", "gigabyte", "terabyte", "petabyte"];
  if (!bytes) {
    return { value: 0, unit: "byte" };
  }
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return { value: bytes / Math.pow(1024, i), unit: units[i] };
}

export const STATUS_ORDERS_FACET_NAME = "status";

/** The host's orders filter grammar (shared/account/composables/useUserOrdersFilter.ts). */
export function getOrdersFilterExpression(
  keyword: string,
  filterData: { statuses: string[]; customerNames?: string[]; startDate?: string; endDate?: string },
): string {
  const escape = (value: string) => value.replaceAll("\\", "\\\\").replaceAll('"', String.raw`\"`);
  let expression = keyword ? `${keyword} ` : "";
  if (filterData.statuses.length) {
    expression += `status:${filterData.statuses.map((status) => `"${escape(status)}"`).join(",")} `;
  }
  if (filterData.customerNames?.length) {
    expression += `customername:${filterData.customerNames.map((name) => `"${escape(name)}"`).join(",")} `;
  }
  const start = toStartDateFilterValue(filterData.startDate);
  const end = toEndDateFilterValue(filterData.endDate);
  if (start || end) {
    expression += `createddate:[${start ? `"${start}" ` : ""}TO${end ? ` "${end}"` : ""}]`;
  }
  return expression.trim();
}

/** The order view-model's shape, empty: specs that read it mock it with the values they assert. */
export const useOrderView = () => ({
  allItemsAreDigital: computed(() => false),
  giftItems: computed(() => []),
  orderItems: computed(() => []),
  mainCurrencyOrderItems: computed(() => []),
  otherCurrencyOrderItemGroups: computed(() => []),
  orderItemsGroupedByVendor: computed(() => ({})),
  deliveryAddress: computed(() => undefined),
  pickupLocation: computed(() => undefined),
  billingAddress: computed(() => undefined),
  shipment: computed(() => undefined),
  payment: computed(() => undefined),
  isCancelled: computed(() => false),
  shipmentMethodName: computed(() => undefined),
  paymentMethodName: computed(() => undefined),
  shipToTitle: computed(() => undefined),
});

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

/**
 * Mirrors the real button's markup — a `<button>` carrying the variant/color/size classes, `disabled`
 * while disabled or loading, and its default slot — since specs find buttons by text and read the
 * classes and state off the element.
 */
export const VcButton = defineComponent({
  name: "VcButton",
  props: {
    color: { type: String, default: "primary" },
    variant: { type: String, default: "solid" },
    size: { type: String, default: "md" },
    type: { type: String, default: "button" },
    disabled: { type: Boolean, default: false },
    loading: { type: Boolean, default: false },
    title: { type: String, default: undefined },
    ariaLabel: { type: String, default: undefined },
  },
  emits: ["click"],
  setup(props, { emit, slots }) {
    const enabled = computed(() => !props.disabled && !props.loading);
    return () =>
      h(
        "button",
        {
          type: props.type,
          disabled: !enabled.value,
          title: props.title,
          "aria-label": props.ariaLabel || props.title,
          "aria-busy": props.loading || undefined,
          class: [
            "vc-button",
            `vc-button--size--${props.size}`,
            `vc-button--color--${props.color}`,
            `vc-button--${props.variant}--${props.color}`,
            { "vc-button--disabled": !enabled.value, "vc-button--loading": props.loading },
          ],
          onClick: (event: MouseEvent) => enabled.value && emit("click", event),
        },
        [
          slots.prepend?.(),
          slots.default ? h("span", { class: "vc-button__slot" }, slots.default()) : null,
          slots.append?.(),
        ],
      );
  },
});

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

/** Renders `text` where the real one does, unless the default slot replaces it. */
export const VcEmptyView = defineComponent({
  name: "VcEmptyView",
  props: { text: { type: String, default: "" } },
  setup:
    (props, { slots }) =>
    () =>
      h("div", { class: "vc-empty-view" }, [
        slots.icon?.(),
        slots.default?.() ?? (props.text ? h("div", { class: "vc-empty-view__text" }, props.text) : null),
        slots.button ? h("div", { class: "vc-empty-view__buttons" }, slots.button()) : null,
      ]),
});

/**
 * Mirrors the real switch's markup — a hidden radio plus the visible button that carries the pressed
 * state — since specs read the selection off `aria-pressed` and the `--checked` class.
 */
export const VcTabSwitch = defineComponent({
  name: "VcTabSwitch",
  props: {
    modelValue: { type: [String, Number, Boolean], default: undefined },
    value: { type: [String, Number, Boolean], required: true },
    label: { type: String, default: "" },
    ariaLabel: { type: String, default: "" },
    name: { type: String, default: undefined },
    disabled: { type: Boolean, default: false },
  },
  emits: ["update:modelValue", "change", "input"],
  setup(props, { emit, slots }) {
    const checked = computed(() => props.modelValue === props.value);
    const onChange = () => emit("change", props.value);
    return () =>
      h("label", { class: ["vc-tab-switch", { "vc-tab-switch--checked": checked.value }] }, [
        h("input", {
          type: "radio",
          class: "vc-tab-switch__input",
          name: props.name,
          value: props.value,
          checked: checked.value,
          disabled: props.disabled,
          onChange,
          onInput: () => emit("input", props.value),
        }),
        h(
          "button",
          {
            class: "vc-tab-switch__button",
            type: "button",
            tabindex: 0,
            "aria-label": props.ariaLabel || props.label,
            "aria-pressed": checked.value,
            onClick: onChange,
          },
          [
            slots.icon?.({ checked: checked.value, value: props.value, label: props.label }),
            slots.default?.({ checked: checked.value, value: props.value, label: props.label }) ??
              (props.label ? h("span", { class: "vc-tab-switch__label" }, props.label) : null),
          ],
        ),
      ]);
  },
});

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
