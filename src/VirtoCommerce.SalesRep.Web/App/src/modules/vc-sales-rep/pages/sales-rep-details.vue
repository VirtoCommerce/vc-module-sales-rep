<template>
  <VcBlade
    :loading="loading"
    :title="title"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
        <!-- Profile -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DETAILS.BLOCKS.PROFILE')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcInput
                v-model="salesRep.firstName"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.FIRST_NAME')"
                :max-length="128"
              />
              <VcInput
                v-model="salesRep.lastName"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.LAST_NAME')"
                :max-length="128"
              />
              <VcInput
                v-model="salesRep.middleName"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.MIDDLE_NAME')"
                :max-length="128"
              />
            </div>
            <VcInput
              v-model="salesRep.birthDate"
              type="date"
              class="tw-w-1/3"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.BIRTH_DATE')"
            />
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcSelect
                v-model="salesRep.timeZone"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.TIME_ZONE')"
                :options="timeZones"
                option-value="id"
                option-label="title"
                searchable
                clearable
              />
              <VcSelect
                v-model="salesRep.defaultLanguage"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.LANGUAGE')"
                :options="languages"
                option-value="id"
                option-label="title"
                searchable
                clearable
              />
              <VcSelect
                v-model="salesRep.currencyCode"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.CURRENCY')"
                :options="currencies"
                option-value="id"
                option-label="title"
                searchable
                clearable
              />
            </div>
            <VcTextarea
              v-model="salesRep.about"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ABOUT')"
            />
          </div>
        </VcCard>

        <!-- Account -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DETAILS.BLOCKS.ACCOUNT')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div class="tw-flex tw-flex-row tw-gap-4">
              <Field
                v-slot="{ errors, errorMessage, handleChange }"
                :model-value="primaryEmail"
                name="email"
                rules="required"
              >
                <VcInput
                  v-model="primaryEmail"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.EMAIL')"
                  required
                  :error="errors.length > 0"
                  :error-message="errorMessage"
                  @update:model-value="handleChange"
                />
              </Field>

              <VcInput
                v-model="salesRep.password"
                class="tw-flex-1"
                type="password"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.PASSWORD')"
                :placeholder="isNew ? '' : $t('VC_SALES_REP.PAGES.DETAILS.FORM.PASSWORD_KEEP')"
              />
            </div>

            <VcSelect
              v-if="isNew"
              v-model="salesRep.storeId"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.STORE')"
              :options="storeOptions"
              option-value="id"
              option-label="name"
              required
            />
          </div>
        </VcCard>

        <!-- Served organizations -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DETAILS.BLOCKS.ORGANIZATIONS')">
          <div class="tw-flex tw-flex-col tw-gap-2 tw-p-4">
            <VcSelect
              v-model="selectedOrganizations"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ORGANIZATIONS')"
              :placeholder="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ORGANIZATIONS_PLACEHOLDER')"
              :options="loadOrganizations"
              option-value="id"
              option-label="name"
              multiple
              searchable
              :emit-value="false"
            />
            <span class="tw-text-xs tw-text-[color:var(--neutrals-500)]">
              {{ $t("VC_SALES_REP.PAGES.DETAILS.FORM.ORGANIZATIONS_HINT") }}
            </span>
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted } from "vue";
import { useI18n } from "vue-i18n";
import { Field } from "vee-validate";
import { IBladeToolbar, usePermissions, useBlade, useLoading, useBladeForm } from "@vc-shell/framework";
import {
  useSalesRepDetails,
  useSalesRepPermissions,
  useStores,
  useOrganizations,
  useDictionaries,
} from "../composables";
import { VcBlade, VcContainer, VcForm, VcCard, VcInput, VcTextarea, VcSelect } from "@vc-shell/framework/ui";

defineBlade({
  url: "/sales-rep-details",
  name: "SalesRepDetails",
});

const { t } = useI18n({ useScope: "global" });
const { param, callParent } = useBlade();
const { hasAccess } = usePermissions();

const isNew = computed(() => !param.value);

const {
  salesRep,
  loadSalesRep,
  saveSalesRep,
  resetSalesRep,
  salesRepIsDirty,
  blockSalesRep,
  unblockSalesRep,
  salesRepCanBlock,
  salesRepCanUnblock,
  loadingOrSavingSalesRep,
} = useSalesRepDetails();
const { createSalesRepPermission, updateSalesRepPermission } = useSalesRepPermissions();
const { stores, loadStores, loadingStores } = useStores();
const { loadOrganizations } = useOrganizations();
const { timeZones, languages, currencies } = useDictionaries();

const storeOptions = computed(() => stores.value.map((x) => ({ id: x.id, name: x.name })));

const primaryEmail = computed<string | undefined>({
  get: () => salesRep.value.emails?.[0],
  set: (value) => {
    salesRep.value.emails = value ? [value] : [];
  },
});

const selectedOrganizations = computed({
  get: () => (salesRep.value.organizations ?? []).map((o) => ({ id: o.organizationId, name: o.organizationName })),
  set: (values: { id?: string; name?: string }[]) => {
    salesRep.value.organizations = (values ?? []).map((v) => ({
      organizationId: v.id,
      organizationName: v.name,
    }));
  },
});

const loading = useLoading(loadingOrSavingSalesRep, loadingStores);

const savePermission = computed(() => (isNew.value ? createSalesRepPermission : updateSalesRepPermission));

const { canSave, setBaseline } = useBladeForm({
  data: salesRep,
  closeConfirmMessage: computed(() => t("VC_SALES_REP.PAGES.DETAILS.ALERTS.CLOSE_CONFIRMATION")),
  canSaveOverride: computed(() => !!salesRepIsDirty.value),
});

const title = computed(() =>
  isNew.value
    ? t("VC_SALES_REP.PAGES.DETAILS.TITLE_NEW")
    : salesRep.value.fullName || t("VC_SALES_REP.PAGES.DETAILS.TITLE"),
);

const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "save",
    icon: "lucide-save",
    title: t("VC_SALES_REP.PAGES.DETAILS.TOOLBAR.SAVE"),
    disabled: !canSave.value,
    clickHandler: async () => {
      await saveSalesRep();
      setBaseline();
      callParent("reload");
      if (salesRep.value.id) {
        callParent("reOpenDetailsBlade", salesRep.value.id);
      }
    },
    isVisible: hasAccess(savePermission.value),
  },
  ...(isNew.value
    ? []
    : [
        {
          id: "reset",
          icon: "lucide-undo-2",
          title: t("VC_SALES_REP.PAGES.DETAILS.TOOLBAR.RESET"),
          disabled: !salesRepIsDirty.value,
          clickHandler: async () => {
            resetSalesRep();
            setBaseline();
          },
        },
        {
          id: "block",
          icon: "lucide-lock",
          title: t("VC_SALES_REP.PAGES.DETAILS.TOOLBAR.BLOCK"),
          clickHandler: async () => {
            await blockSalesRep();
            callParent("reload");
            callParent("reOpenDetailsBlade", salesRep.value.id);
          },
          isVisible: hasAccess(updateSalesRepPermission) && salesRepCanBlock.value,
        },
        {
          id: "unblock",
          icon: "lucide-lock-open",
          title: t("VC_SALES_REP.PAGES.DETAILS.TOOLBAR.UNBLOCK"),
          clickHandler: async () => {
            await unblockSalesRep();
            callParent("reload");
            callParent("reOpenDetailsBlade", salesRep.value.id);
          },
          isVisible: hasAccess(updateSalesRepPermission) && salesRepCanUnblock.value,
        },
      ]),
]);

onMounted(async () => {
  await loadStores();
  if (param.value) {
    await loadSalesRep({ id: param.value });
  }
  setBaseline();
});
</script>
