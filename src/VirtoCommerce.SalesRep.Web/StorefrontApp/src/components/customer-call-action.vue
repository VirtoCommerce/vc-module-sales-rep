<template>
  <!-- One design-system tooltip over every state; the trigger slot binds its hover/focus listeners on a wrapper, so it
       also shows over a disabled control, which fires no events of its own. -->
  <VcTooltip class="customer-call-action" :disabled="menuOpened" placement="top" lazy>
    <template #trigger>
      <VcPopover
        v-if="phones.length > 1"
        placement="bottom-end"
        role="dialog"
        :aria-label="t('sales_rep.communication.call.choose_number')"
        lazy
        @toggle="menuOpened = $event"
      >
        <template #default="{ triggerProps }">
          <VcButton v-bind="{ ...buttonProps, ...triggerProps }" :aria-label="t('sales_rep.communication.call.action')">
            <template v-if="label" #default>{{ label }}</template>
          </VcButton>
        </template>

        <template #content="{ close }">
          <ul class="customer-call-action__menu">
            <li v-for="phone in phones" :key="phone.href">
              <VcMenuItem :external-link="phone.href" size="sm" nowrap @click="close()">
                <template v-if="phone.name">
                  <span class="customer-call-action__name">{{ phone.name }}</span>
                  ·
                </template>

                <span class="customer-call-action__number">{{ phone.number }}</span>
              </VcMenuItem>
            </li>
          </ul>
        </template>
      </VcPopover>

      <VcButton
        v-else-if="phones.length === 1"
        v-bind="buttonProps"
        :external-link="phones[0].href"
        :aria-label="t('sales_rep.communication.call.action_with_number', { number: phones[0].number })"
      >
        <template v-if="label" #default>{{ label }}</template>
      </VcButton>

      <!-- Disabled. A disabled button cannot take focus, so the wrapper does, and carries the reason. -->
      <span
        v-else
        class="customer-call-action__disabled"
        role="button"
        tabindex="0"
        aria-disabled="true"
        :aria-label="`${t('sales_rep.communication.call.action')}, ${tooltip}`"
      >
        <VcButton v-bind="buttonProps" disabled tabindex="-1" aria-hidden="true">
          <template v-if="label" #default>{{ label }}</template>
        </VcButton>
      </span>
    </template>

    <template #content>{{ tooltip }}</template>
  </VcTooltip>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { CustomerPhoneType } from "../phones";

interface IProps {
  phones: CustomerPhoneType[];
  /** `icon` for the My customers table, `button` (with a label) for its mobile cards. */
  variant?: "icon" | "button";
}

const props = withDefaults(defineProps<IProps>(), {
  variant: "icon",
});

const { t } = useI18n();

const menuOpened = ref(false);

const tooltip = computed(() =>
  props.phones.length ? t("sales_rep.communication.call.action") : t("sales_rep.communication.call.no_phone"),
);

const label = computed(() => (props.variant === "button" ? t("sales_rep.communication.call.action") : undefined));

const buttonProps = computed(() =>
  props.variant === "button"
    ? { size: "sm" as const, variant: "outline" as const, prependIcon: "phone" }
    : { icon: "phone", iconSize: "1.25rem", variant: "ghost" as const, color: "neutral" as const },
);
</script>

<style lang="scss">
// @apply: module is self-contained as an MF remote (no global utility layer).
.customer-call-action {
  @apply inline-flex;

  &__disabled {
    @apply inline-flex rounded;

    // The wrapper owns hover and focus; the disabled button inside would swallow the pointer otherwise.
    & > * {
      @apply pointer-events-none;
    }
  }

  &__menu {
    @apply flex min-w-56 flex-col py-1;
  }

  &__name {
    @apply font-medium text-neutral-900;
  }

  &__number {
    @apply text-neutral-600 [font-variant-numeric:tabular-nums];
  }
}
</style>
