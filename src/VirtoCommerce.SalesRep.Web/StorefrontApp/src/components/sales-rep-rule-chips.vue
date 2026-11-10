<template>
  <div class="sales-rep-rule-chips">
    <!-- Baseline tab: active when no rule is chosen; clicking it clears the filter. Its value is a
         boolean so no non-empty rule name can collide with it (a rule named "" would still match,
         but every surface here already reads a falsy filter as the baseline).
         `Boolean(true)`, not `:value="true"`: the latter trips vue/prefer-true-attribute-shorthand,
         and the shorthand it asks for passes "" instead — same reason as variations.vue. -->
    <SalesRepRuleChip
      :value="Boolean(true)"
      :model-value="!modelValue"
      :label="allLabel ?? ''"
      :count="allCount"
      :hint="allHint"
      @change="modelValue = undefined"
    >
      <!-- Adornments belong to whoever knows what a tab means: the baseline arrives with no name. -->
      <template #append>
        <slot name="suffix" />
      </template>
    </SalesRepRuleChip>

    <SalesRepRuleChip
      v-for="rule in selectableRules"
      :key="rule.name"
      :value="rule.name"
      :model-value="modelValue"
      :label="rule.label"
      :count="rule.count"
      :hint="rule.hint"
      @change="modelValue = $event"
    >
      <template #append>
        <slot name="suffix" :rule-name="rule.name" />
      </template>
    </SalesRepRuleChip>
  </div>
</template>

<script setup lang="ts">
import { useSalesRepRuleSelection } from "../composables/useSalesRepRuleSelection";
import SalesRepRuleChip from "./sales-rep-rule-chip.vue";
import type { SalesRepRuleType } from "../types";

interface IProps {
  // The server-defined filter rules to offer as tabs.
  rules: SalesRepRuleType[];
  // Label for the synthetic baseline tab (the "All" / no-filter option).
  allLabel?: string;
  // Item count for the baseline tab; rendered as a highlighted counter when present (like `rule.count`).
  allCount?: number;
  // Hint for the baseline tab, shown on its hover and focus (like `rule.hint`).
  allHint?: string;
  // Whether `rules` is still being fetched — an in-flight refetch must not look like "the rule is gone".
  loading?: boolean;
}

const props = defineProps<IProps>();

defineSlots<{
  /** An adornment after a tab's label; the baseline tab has no rule name. */
  suffix?(props: { ruleName?: string }): unknown;
}>();

// undefined = baseline (first tab); single source of this convention across all rule-tab surfaces.
const modelValue = defineModel<string | undefined>();

// Data-derived vocabularies change with the scope (period, customer), so a selected rule can stop being offered — e.g.
// no order carries that status in the newly picked period. That tab then falls back to the baseline.
const { selectableRules } = useSalesRepRuleSelection(
  modelValue,
  () => props.rules,
  () => props.loading,
);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.sales-rep-rule-chips {
  @apply flex flex-wrap items-center gap-1;
}
</style>
