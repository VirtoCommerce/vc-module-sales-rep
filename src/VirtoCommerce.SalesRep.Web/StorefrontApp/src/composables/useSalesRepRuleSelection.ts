import { computed, toValue, watch } from "vue";
import { selectableFilterRules } from "../utils";
import type { SalesRepRuleType } from "../types";
import type { MaybeRefOrGetter, Ref } from "vue";

/**
 * A rule picker's selection, for a chip row and a select alike. The backend's "All" passthrough rule (customer
 * segments carry one) is not offered: it would duplicate the picker's own no-rule option. A picked rule the loaded
 * rules do not offer falls back to none, rather than staying active where nothing shows it. While the rules load, the
 * pick stays: an in-flight refetch must not look like "the rule is gone".
 */
export function useSalesRepRuleSelection(
  selected: Ref<string | undefined>,
  rules: MaybeRefOrGetter<SalesRepRuleType[]>,
  loading: MaybeRefOrGetter<boolean | undefined>,
) {
  watch(
    () => [toValue(loading), toValue(rules)] as const,
    ([isLoading, offered]) => {
      if (isLoading || !selected.value) {
        return;
      }

      if (!offered.some((rule) => rule.name === selected.value)) {
        selected.value = undefined;
      }
    },
    { immediate: true },
  );

  const selectableRules = computed(() => selectableFilterRules(toValue(rules)));

  return { selectableRules };
}
