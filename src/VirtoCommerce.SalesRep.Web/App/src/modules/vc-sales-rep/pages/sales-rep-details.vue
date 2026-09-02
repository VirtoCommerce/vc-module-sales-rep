<template>
  <VcBlade
    :loading="loading"
    :title="title"
    :toolbar-items="bladeToolbar"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
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
              <Field
                v-slot="{ errors, errorMessage, handleChange }"
                :model-value="salesRep.password"
                name="password"
                :rules="passwordRules"
                class="tw-flex-1"
              >
                <VcInput
                  v-model="salesRep.password"
                  type="password"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.PASSWORD')"
                  :placeholder="isNew ? '' : $t('VC_SALES_REP.PAGES.DETAILS.FORM.PASSWORD_KEEP')"
                  :error="errors.length > 0"
                  :error-message="errorMessage"
                  @update:model-value="handleChange"
                />
              </Field>
            </div>
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcSelect
                v-if="isNew"
                v-model="salesRep.storeId"
                class="tw-flex-1 tw-min-w-0"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.STORE')"
                :options="storeOptions"
                option-value="id"
                option-label="name"
                required
              />
              <VcSelect
                v-model="salesRep.roleId"
                class="tw-flex-1 tw-min-w-0"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ROLE')"
                :hint="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ROLE_HINT')"
                :options="roleOptions"
                option-value="id"
                option-label="title"
                required
              />
            </div>
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

        <!-- Profile -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DETAILS.BLOCKS.PROFILE')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <!-- First/last name are required: the storefront X-API exposes contact.firstName/lastName/fullName as
                 non-null GraphQL fields, so a nameless rep can't even sign in to the storefront (VCST-5759). -->
            <div class="tw-flex tw-flex-row tw-gap-4">
              <Field
                v-slot="{ errors, errorMessage, handleChange }"
                :model-value="salesRep.firstName"
                name="firstName"
                rules="required"
              >
                <VcInput
                  v-model="salesRep.firstName"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.FIRST_NAME')"
                  :max-length="128"
                  required
                  :error="errors.length > 0"
                  :error-message="errorMessage"
                  @update:model-value="handleChange"
                />
              </Field>
              <Field
                v-slot="{ errors, errorMessage, handleChange }"
                :model-value="salesRep.lastName"
                name="lastName"
                rules="required"
              >
                <VcInput
                  v-model="salesRep.lastName"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.LAST_NAME')"
                  :max-length="128"
                  required
                  :error="errors.length > 0"
                  :error-message="errorMessage"
                  @update:model-value="handleChange"
                />
              </Field>
              <VcInput
                v-model="salesRep.middleName"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.MIDDLE_NAME')"
                :max-length="128"
              />
            </div>
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcInput
                v-model="salesRep.birthDate"
                type="date"
                class="tw-w-1/3"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.BIRTH_DATE')"
              />
              <VcInput
                v-model="salesRep.salutation"
                class="tw-w-1/3"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.SALUTATION')"
                :max-length="64"
              />
            </div>
            <div class="tw-flex tw-flex-row tw-gap-4">
              <VcSelect
                v-model="salesRep.timeZone"
                class="tw-flex-1 tw-min-w-0"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.TIME_ZONE')"
                :options="timeZones"
                option-value="id"
                option-label="title"
                searchable
                clearable
              />
              <VcSelect
                v-model="salesRep.defaultLanguage"
                class="tw-flex-1 tw-min-w-0"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.LANGUAGE')"
                :options="languages"
                option-value="id"
                option-label="title"
                searchable
                clearable
              />
              <VcSelect
                v-model="salesRep.currencyCode"
                class="tw-flex-1 tw-min-w-0"
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
              style="--textarea-height: 60px"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ABOUT')"
            />
            <VcMultivalue
              v-model="additionalEmailItems"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDITIONAL_EMAILS')"
              :placeholder="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDITIONAL_EMAILS_PLACEHOLDER')"
              option-label="id"
            />
            <VcMultivalue
              v-model="phoneItems"
              :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.PHONES')"
              :placeholder="$t('VC_SALES_REP.PAGES.DETAILS.FORM.PHONES_PLACEHOLDER')"
              option-label="id"
            />
          </div>
        </VcCard>

        <!-- Addresses -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.DETAILS.BLOCKS.ADDRESSES')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div
              v-for="(address, index) in salesRep.addresses"
              :key="index"
              class="tw-flex tw-flex-col tw-gap-3 tw-rounded tw-border tw-border-solid tw-border-[color:var(--neutrals-200)] tw-p-3"
            >
              <div class="tw-flex tw-flex-row tw-items-center tw-justify-between">
                <span class="tw-font-medium">{{ $t("VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS") }} {{ index + 1 }}</span>
                <VcButton
                  icon="lucide-trash-2"
                  text
                  @click="removeAddress(index)"
                />
              </div>
              <div class="tw-flex tw-flex-row tw-gap-4">
                <VcSelect
                  v-model="address.addressType"
                  class="tw-flex-1 tw-min-w-0"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_TYPE')"
                  :options="addressTypeOptions"
                  option-value="id"
                  option-label="title"
                />
                <VcInput
                  v-model="address.firstName"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.FIRST_NAME')"
                />
                <VcInput
                  v-model="address.lastName"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.LAST_NAME')"
                />
              </div>
              <Field
                v-slot="{ errors, errorMessage, handleChange }"
                :model-value="address.line1"
                :name="`address_${index}_line1`"
                rules="required"
              >
                <VcInput
                  v-model="address.line1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_LINE1')"
                  required
                  :error="errors.length > 0"
                  :error-message="errorMessage"
                  @update:model-value="handleChange"
                />
              </Field>
              <VcInput
                v-model="address.line2"
                :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_LINE2')"
              />
              <div class="tw-flex tw-flex-row tw-gap-4">
                <Field
                  v-slot="{ errors, errorMessage, handleChange }"
                  :model-value="address.city"
                  :name="`address_${index}_city`"
                  rules="required"
                >
                  <VcInput
                    v-model="address.city"
                    class="tw-flex-1 tw-min-w-0"
                    :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_CITY')"
                    required
                    :error="errors.length > 0"
                    :error-message="errorMessage"
                    @update:model-value="handleChange"
                  />
                </Field>
                <VcInput
                  v-model="address.regionName"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_REGION')"
                />
                <Field
                  v-slot="{ errors, errorMessage, handleChange }"
                  :model-value="address.postalCode"
                  :name="`address_${index}_postalCode`"
                  rules="required"
                  class="tw-flex-1 tw-min-w-0"
                >
                  <VcInput
                    v-model="address.postalCode"
                    class="tw-flex-1 tw-min-w-0"
                    :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_POSTAL_CODE')"
                    required
                    :error="errors.length > 0"
                    :error-message="errorMessage"
                    @update:model-value="handleChange"
                  />
                </Field>
              </div>
              <Field
                v-slot="{ errors, handleChange }"
                :model-value="address.countryCode"
                :name="`address_${index}_country`"
                rules="required"
              >
                <VcSelect
                  :model-value="address.countryCode"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_COUNTRY_NAME')"
                  :options="countries"
                  option-value="id"
                  option-label="title"
                  searchable
                  required
                  :error="errors.length > 0"
                  @update:model-value="(code: string) => { setCountry(address, code); handleChange(code); }"
                />
              </Field>
              <div class="tw-flex tw-flex-row tw-gap-4">
                <VcInput
                  v-model="address.phone"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_PHONE')"
                />
                <VcInput
                  v-model="address.email"
                  class="tw-flex-1"
                  :label="$t('VC_SALES_REP.PAGES.DETAILS.FORM.ADDRESS_EMAIL')"
                />
              </div>
            </div>
            <div>
              <VcButton
                icon="lucide-plus"
                @click="addAddress"
              >
                {{ $t("VC_SALES_REP.PAGES.DETAILS.FORM.ADD_ADDRESS") }}
              </VcButton>
            </div>
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
  useRoles,
} from "../composables";
import { AddressType, CustomerAddress } from "../../../api_client/virtocommerce.salesrep";
import {
  VcBlade,
  VcContainer,
  VcForm,
  VcCard,
  VcInput,
  VcTextarea,
  VcSelect,
  VcMultivalue,
  VcButton,
} from "@vc-shell/framework/ui";

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
const { createSalesRepPermission, updateSalesRepPermission, accountCreatePermission, accountManagementPermission } =
  useSalesRepPermissions();
const { stores, loadStores, loadingStores } = useStores();
const { loadOrganizations } = useOrganizations();
const { timeZones, languages, currencies, countries, loadDictionaries, loadingDictionaries } = useDictionaries();
const { roles, loadRoles, loadingRoles } = useRoles();

const storeOptions = computed(() => stores.value.map((x) => ({ id: x.id, name: x.name })));
const roleOptions = computed(() => roles.value.map((x) => ({ id: x.id, title: x.name })));

// Minimum account password length. Kept in sync with the platform's default security policy; no client API
// exposes the configured value, so it is a constant here.
const PASSWORD_MIN_LENGTH = 8;
// Validate length only when a password is entered: a blank password keeps the current one on edit (and, on
// create, an account may be created without a password). vee-validate runs rules on empty values too, so the
// empty case is allowed explicitly.
const passwordRules = (value: unknown) => {
  const password = typeof value === "string" ? value : "";
  return !password || password.length >= PASSWORD_MIN_LENGTH
    ? true
    : t("VC_SALES_REP.PAGES.DETAILS.VALIDATION.PASSWORD_MIN", { min: PASSWORD_MIN_LENGTH });
};

// emails[0] is the account sign-in login (bound to the account — always kept, can't be removed here);
// emails[1..] are additional emails edited via the multi-value below. The service dedups the combined list.
const primaryEmail = computed<string | undefined>({
  get: () => salesRep.value.emails?.[0],
  set: (value) => {
    const rest = (salesRep.value.emails ?? []).slice(1);
    salesRep.value.emails = [value ?? "", ...rest];
  },
});

// VcMultivalue (free-form) models chips as { [optionLabel]: value }; we key on `id` (its default item
// shape). `id` is both the value and the displayed chip label.
const additionalEmailItems = computed({
  get: () => (salesRep.value.emails ?? []).slice(1).map((email) => ({ id: email })),
  set: (items: { id?: string }[]) => {
    const login = salesRep.value.emails?.[0] ?? "";
    const extra = (items ?? []).map((x) => x.id ?? "").filter(Boolean);
    salesRep.value.emails = [login, ...extra];
  },
});

const phoneItems = computed({
  get: () => (salesRep.value.phones ?? []).map((phone) => ({ id: phone })),
  set: (items: { id?: string }[]) => {
    salesRep.value.phones = (items ?? []).map((x) => x.id ?? "").filter(Boolean);
  },
});

const addressTypeOptions = Object.values(AddressType)
  .filter((value) => value !== AddressType.Undefined)
  .map((value) => ({ id: value, title: value }));

const addAddress = () => {
  if (!salesRep.value.addresses) {
    salesRep.value.addresses = [];
  }
  salesRep.value.addresses.push({ addressType: AddressType.BillingAndShipping } as CustomerAddress);
};

const removeAddress = (index: number) => {
  salesRep.value.addresses?.splice(index, 1);
};

// Country is picked from the platform countries catalog: store the ISO code and keep countryName in sync from the
// selected option. Filling countryName here means the backend never has to resolve it from the code on save
// (which is what threw a 500 when the code was empty/invalid).
const setCountry = (address: CustomerAddress, code?: string) => {
  address.countryCode = code ?? "";
  address.countryName = countries.value.find((c) => c.id === code)?.title ?? "";
};

const selectedOrganizations = computed({
  get: () => (salesRep.value.organizations ?? []).map((o) => ({ id: o.organizationId, name: o.organizationName })),
  set: (values: { id?: string; name?: string }[]) => {
    salesRep.value.organizations = (values ?? []).map((v) => ({
      organizationId: v.id,
      organizationName: v.name,
    }));
  },
});

const loading = useLoading(loadingOrSavingSalesRep, loadingStores, loadingRoles, loadingDictionaries);

// Saving creates/edits both the member and the login account, so it needs both permissions (matches the API).
const hasSaveAccess = computed(() =>
  isNew.value
    ? hasAccess(createSalesRepPermission) && hasAccess(accountCreatePermission)
    : hasAccess(updateSalesRepPermission) && hasAccess(accountManagementPermission),
);

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
    isVisible: hasSaveAccess.value,
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
          isVisible: hasAccess(accountManagementPermission) && salesRepCanBlock.value,
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
          isVisible: hasAccess(accountManagementPermission) && salesRepCanUnblock.value,
        },
      ]),
]);

onMounted(async () => {
  // These loaders are independent — run them concurrently to cut blade-open latency.
  await Promise.all([
    loadStores(),
    loadRoles(),
    loadDictionaries(),
    param.value ? loadSalesRep({ id: param.value }) : Promise.resolve(),
  ]);
  // Default the role on a new rep so the required dropdown is pre-filled.
  if (!param.value && !salesRep.value.roleId && roles.value.length) {
    salesRep.value.roleId = roles.value[0].id;
  }
  setBaseline();
});
</script>
