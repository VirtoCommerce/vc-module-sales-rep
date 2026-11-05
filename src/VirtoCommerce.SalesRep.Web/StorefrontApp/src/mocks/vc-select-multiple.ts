/* A minimal test double of the ui-kit's VcSelect in multiple mode, not a shippable component. Shared by the specs of
   the customer picker's callers (the list Share flow and the Send a message popup). */

import { defineComponent, h } from "vue";
import type { PropType } from "vue";

/* Multiple mode carries whole items, not ids — the real VcSelect pushes the item it was given. */
export const VcSelectMultiple = defineComponent({
  props: {
    modelValue: { type: Array as PropType<Record<string, string>[]>, default: () => [] },
    items: { type: Array as PropType<Record<string, string>[]>, default: () => [] },
    textField: { type: String, default: "" },
    valueField: { type: String, default: "" },
    message: { type: String, default: "" },
    error: { type: Boolean, default: false },
    required: { type: Boolean, default: false },
    disabled: { type: Boolean, default: false },
    multiple: { type: Boolean, default: false },
    clearable: { type: Boolean, default: false },
    testIdDropdown: { type: String, default: "" },
  },
  emits: ["update:modelValue", "change"],
  setup(props, { emit }) {
    const isPicked = (item: Record<string, string>) =>
      props.modelValue.some((picked) => picked[props.valueField] === item[props.valueField]);

    // The kit raises both events together on every path that changes the selection, its own clear button included.
    function commit(next: Record<string, string>[]) {
      emit("update:modelValue", next);
      emit("change", next);
    }

    function toggle(item: Record<string, string>) {
      commit(
        isPicked(item)
          ? props.modelValue.filter((picked) => picked[props.valueField] !== item[props.valueField])
          : [...props.modelValue, item],
      );
    }

    return () =>
      h(
        "div",
        {
          "data-test-id": props.testIdDropdown,
          "data-multiple": String(props.multiple),
          "aria-invalid": String(props.error),
          "aria-required": String(props.required),
          "aria-disabled": String(props.disabled),
        },
        [
          // The real select's focusable container, which `CustomerPicker.focus()` finds by its class.
          h("div", { class: "vc-select__container", tabindex: "-1" }),
          ...props.items.map((item) =>
            h(
              "button",
              {
                key: item[props.valueField],
                type: "button",
                "data-test-id": `customer-option-${item[props.valueField]}`,
                "aria-pressed": String(isPicked(item)),
                onClick: () => toggle(item),
              },
              item[props.textField],
            ),
          ),
          props.clearable
            ? h("button", { type: "button", "data-test-id": "customer-select-clear", onClick: () => commit([]) })
            : null,
          props.message ? h("span", { "data-test-id": "field-message" }, props.message) : null,
        ],
      );
  },
});
