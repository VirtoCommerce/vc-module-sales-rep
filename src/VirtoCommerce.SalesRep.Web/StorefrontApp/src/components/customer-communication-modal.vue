<template>
  <VcModal
    ref="modalComponent"
    :title="t('sales_rep.communication.title')"
    class="customer-communication-modal"
    is-mobile-fullscreen
    dividers
  >
    <p class="customer-communication-modal__subtitle">
      {{
        locked
          ? t("sales_rep.communication.subtitle", { organization: organizationName })
          : t("sales_rep.communication.subtitle_customers")
      }}
    </p>

    <form class="customer-communication-modal__form" @submit.prevent>
      <!-- Opened from a customer's own page, the popup stays on that customer: shown, never changed. -->
      <fieldset v-if="locked">
        <VcLabel class="customer-communication-modal__customers-label">
          {{ t("sales_rep.communication.customers_label") }}
        </VcLabel>

        <WishlistSharingRecipients :recipients="selectedCustomers" readonly />
      </fieldset>

      <div v-else class="customer-communication-modal__customers">
        <CustomerPicker
          :model-value="selectedCustomers"
          test-id-dropdown="customer-communication-customers"
          :label="t('sales_rep.communication.customers_label')"
          :placeholder="t('sales_rep.communication.customers_placeholder')"
          :disabled="loading || optionsLoading"
          :items="pickerItems"
          :error="failed || !!errors.customers"
          :message="failed ? t('sales_rep.communication.customers_error') : errors.customers"
          @change="customers = $event"
        />

        <WishlistSharingRecipients
          v-if="selectedCustomers.length"
          :recipients="selectedCustomers"
          :disabled="loading"
          @remove="deselect"
          @clear="customers = []"
        />
      </div>

      <VcInput
        v-model="title"
        :label="t('sales_rep.communication.title_label')"
        :placeholder="t('common.placeholders.enter_value')"
        :disabled="loading"
        :maxlength="128"
        :message="errors.title"
        :error="!!errors.title"
      />

      <VcTextarea
        v-model="message"
        :label="t('sales_rep.communication.message_label')"
        :placeholder="t('common.placeholders.enter_value')"
        :disabled="loading"
        :max-length="1000"
        :message="errors.message"
        :error="!!errors.message"
        rows="4"
        required
      />

      <fieldset>
        <VcLabel required class="customer-communication-modal__channels-label">
          {{ t("sales_rep.communication.channels_label") }}
        </VcLabel>

        <div class="customer-communication-modal__channels">
          <VcCheckbox v-model="sendEmail" :disabled="loading">{{
            t("sales_rep.communication.email_label")
          }}</VcCheckbox>

          <VcCheckbox v-model="sendPush" :disabled="loading">{{ t("sales_rep.communication.push_label") }}</VcCheckbox>
        </div>
      </fieldset>
    </form>

    <template #actions="{ close }">
      <VcButton :disabled="loading" color="secondary" variant="outline" @click="close">
        {{ t("sales_rep.communication.cancel_button") }}
      </VcButton>

      <VcButton :disabled="!meta.valid || !channelSelected" :loading="loading" @click="send">
        {{ t("sales_rep.communication.send_button") }}
      </VcButton>
    </template>
  </VcModal>
</template>

<script setup lang="ts">
import { toTypedSchema } from "@vee-validate/yup";
import { useField, useForm } from "vee-validate";
import { computed, ref, useTemplateRef } from "vue";
import { useI18n } from "vue-i18n";
import { string } from "yup";
import { useNotifications, VcModal } from "@vc-frontend/core";
import { useSalesRepCommunication } from "../composables/useSalesRepCommunication";
import { useSalesRepCustomerOptions } from "../composables/useSalesRepCustomerOptions";
import { mergeCustomerOptions } from "../utils";
import CustomerPicker from "./customer-picker.vue";
import WishlistSharingRecipients from "./wishlist-sharing-recipients.vue";
import type { OrganizationRowType } from "../types";

interface IProps {
  /** The customer the popup opens for; none when the customer list's own Send message button opens it. */
  organizationId?: string;
  /** That customer's name, shown until the customer options load, or when they do not list it. */
  organizationName?: string;
  /** Keeps the popup on that customer: it cannot be removed, and no other customer can be added. */
  locked?: boolean;
}

const props = defineProps<IProps>();

const { t, te } = useI18n();
const notifications = useNotifications();
const { sendCommunication, loading } = useSalesRepCommunication();
const { options, loading: optionsLoading, failed, findOption } = useSalesRepCustomerOptions();

const modalComponent = useTemplateRef<InstanceType<typeof VcModal>>("modalComponent");

// The caller knows only the name; the location and the logo come with the options, when they list the customer.
const openedFor: OrganizationRowType[] = props.organizationId
  ? [
      {
        organizationId: props.organizationId,
        organizationName: props.organizationName || props.organizationId,
        location: "",
        imageUrl: "",
      },
    ]
  : [];

const sendEmail = ref(false);
const sendPush = ref(false);
const channelSelected = computed(() => sendEmail.value || sendPush.value);

const { errors, meta, handleSubmit } = useForm({
  initialValues: { customers: openedFor, title: "", message: "" },
});

const { value: customers } = useField<OrganizationRowType[]>(
  "customers",
  (value: OrganizationRowType[]) => value.length > 0 || t("sales_rep.communication.customers_required"),
);
const { value: title } = useField<string>("title", toTypedSchema(string().trim().max(128)));
const { value: message } = useField<string>("message", toTypedSchema(string().trim().required().max(1000)));

const selectedCustomers = computed(() =>
  customers.value.map((customer) => findOption(customer.organizationId) ?? customer),
);

const pickerItems = computed(() => mergeCustomerOptions(options.value, openedFor));

function deselect(organizationId: string): void {
  customers.value = customers.value.filter((customer) => customer.organizationId !== organizationId);
}

const send = handleSubmit(async (data) => {
  // One send for the whole set: a member of several of these organizations still receives it once.
  const result = await sendCommunication({
    organizationIds: customers.value.map((customer) => customer.organizationId),
    sendEmail: sendEmail.value,
    sendPush: sendPush.value,
    title: data.title.trim() || undefined,
    message: data.message.trim(),
  });

  // Map the backend's stable warning codes to localized messages (unknown codes fall back to a generic note). One send
  // covers many organizations, so the backend may repeat a code once per organization.
  const warnings = [...new Set(result.warnings)].map((code) =>
    te(`sales_rep.communication.warnings.${code}`)
      ? t(`sales_rep.communication.warnings.${code}`)
      : t("sales_rep.communication.warnings.generic"),
  );

  if (result.succeeded) {
    modalComponent.value?.close();

    if (warnings.length > 0) {
      // Partial success — at least one channel delivered, but tell the rep what didn't.
      notifications.warning({
        text: `${t("sales_rep.communication.partial_success_text")} ${warnings.join(" ")}`,
        duration: 10000,
        single: true,
      });
    } else {
      notifications.success({
        text: t("sales_rep.communication.success_text"),
        duration: 10000,
        single: true,
      });
    }
  } else {
    // Nothing was sent — surface the specific reason(s) when we have them, else a generic error.
    notifications.error({
      text: warnings.length > 0 ? warnings.join(" ") : t("sales_rep.communication.error_text"),
      duration: 10000,
      single: true,
    });
  }
});
</script>

<style lang="scss">
.customer-communication-modal {
  &__form {
    @apply space-y-4;
  }

  &__subtitle {
    @apply mb-4 text-sm text-neutral-500 [word-break:break-word];
  }

  &__customers {
    @apply space-y-4;
  }

  &__customers-label,
  &__channels-label {
    @apply mb-2;
  }

  &__channels {
    @apply flex flex-col gap-2;
  }
}
</style>
