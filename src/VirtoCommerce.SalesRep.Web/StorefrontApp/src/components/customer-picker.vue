<template>
  <VcSelect
    ref="select"
    :model-value="modelValue"
    :test-id-dropdown="testIdDropdown"
    :label="label"
    :placeholder="placeholder"
    :disabled="disabled"
    :items="items"
    :error="error"
    :message="message"
    text-field="organizationName"
    value-field="organizationId"
    required
    multiple
    autocomplete
    clearable
    lazy
    @change="emit('change', $event)"
  >
    <template #item="{ item }">
      <span class="customer-picker__option">
        <WishlistSharingAvatar :organization-name="item.organizationName" :image-url="item.imageUrl" />

        <span class="customer-picker__option-meta">
          <span class="customer-picker__option-name">{{ item.organizationName }}</span>

          <span v-if="item.location" class="customer-picker__option-location">{{ item.location }}</span>
        </span>
      </span>
    </template>
  </VcSelect>
</template>

<script setup lang="ts">
import { useTemplateRef } from "vue";
import WishlistSharingAvatar from "./wishlist-sharing-avatar.vue";
import type { OrganizationRowType } from "../types";

interface IProps {
  /** The picked customers, as whole rows: `VcSelect` keeps items, not ids, in multiple mode. */
  modelValue: OrganizationRowType[];
  items: OrganizationRowType[];
  label: string;
  placeholder: string;
  message?: string;
  error?: boolean;
  disabled?: boolean;
  testIdDropdown?: string;
}

defineProps<IProps>();

const emit = defineEmits<{ change: [customers: OrganizationRowType[]] }>();

const select = useTemplateRef<{ $el: HTMLElement }>("select");

// `VcSelect` has no focus method, and its input opens the list on focus, so this focuses the select's container: the
// element a dialog focuses when it opens on this field.
function focus(): void {
  select.value?.$el?.querySelector<HTMLElement>(".vc-select__container")?.focus();
}

defineExpose({ focus });
</script>

<style lang="scss">
// The rep's customers to pick from, shared by the list Share flow and the Send a message popup.
.customer-picker {
  &__option {
    @apply flex min-w-0 grow items-center gap-3;
  }

  &__option-meta {
    @apply flex min-w-0 grow flex-col;
  }

  &__option-name {
    // The leading is spelled out so the two lines total the avatar's 32 and the row stays balanced; the menu item
    // hands down 14/14.
    @apply truncate text-sm/[18px] font-bold text-neutral-950;
  }

  &__option-location {
    // `font-normal` against the kit: `VcMenuItem` bolds the whole row of a selected option, and the second line is
    // Regular in every state.
    @apply truncate text-xs font-normal text-neutral-600;
  }
}
</style>
