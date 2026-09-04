<template>
  <VcBlade
    :title="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.TITLE')"
    width="50%"
  >
    <VcContainer>
      <VcForm class="tw-flex tw-flex-col tw-gap-4 tw-p-3">
        <!-- Run settings -->
        <VcCard :header="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.BLOCKS.SETTINGS')">
          <div class="tw-flex tw-flex-col tw-gap-4 tw-p-4">
            <div class="tw-flex tw-flex-row tw-items-start tw-gap-4">
              <VcSelect
                v-model="storeId"
                class="tw-flex-1 tw-min-w-0"
                :label="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.FORM.STORE')"
                :options="storeOptions"
                option-value="id"
                option-label="name"
                :loading="loadingStores"
                searchable
              />
              <VcSwitch
                v-model="includeLiveData"
                class="tw-flex-1"
                :label="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.FORM.DEEP_CHECK')"
                :hint="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.FORM.DEEP_CHECK_HINT')"
              />
            </div>
            <div>
              <VcButton
                icon="lucide-play"
                :loading="runningDiagnostics"
                :disabled="runningDiagnostics"
                @click="onRun"
              >
                {{ $t("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.FORM.RUN") }}
              </VcButton>
            </div>
          </div>
        </VcCard>

        <!-- Checklist -->
        <VcCard
          v-if="hasRun"
          :header="$t('VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.BLOCKS.RESULTS')"
        >
          <div class="tw-flex tw-flex-col tw-p-2">
            <div
              v-if="!checks.length"
              class="tw-p-4 tw-text-center tw-text-[color:var(--neutrals-500)]"
            >
              {{ $t("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.EMPTY") }}
            </div>
            <div
              v-for="(check, index) in checks"
              :key="`${check.stage}_${index}`"
              class="tw-flex tw-flex-col tw-gap-2 tw-border-0 tw-border-b tw-border-solid tw-border-[color:var(--neutrals-200)] tw-p-3 last:tw-border-b-0"
            >
              <div class="tw-flex tw-flex-row tw-items-center tw-gap-3">
                <span
                  class="tw-inline-flex tw-min-w-[80px] tw-shrink-0 tw-items-center tw-justify-center tw-rounded-full tw-border tw-border-solid tw-px-3 tw-py-0.5 tw-text-xs tw-font-semibold"
                  :class="statusClasses(check.status)"
                >
                  {{ statusLabel(check.status) }}
                </span>
                <span class="tw-shrink-0 tw-font-medium">{{ stageLabel(check.stage) }}</span>
                <span class="tw-min-w-0 tw-grow tw-text-[color:var(--neutrals-600)]">{{ check.message }}</span>
                <VcButton
                  v-if="check.detail"
                  :icon="expanded[index] ? 'lucide-chevron-up' : 'lucide-chevron-down'"
                  text
                  class="tw-shrink-0"
                  @click="toggleDetail(index)"
                >
                  {{
                    expanded[index]
                      ? $t("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.DETAIL.HIDE")
                      : $t("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.DETAIL.SHOW")
                  }}
                </VcButton>
              </div>
              <pre
                v-if="check.detail && expanded[index]"
                class="tw-m-0 tw-whitespace-pre-wrap tw-break-words tw-rounded tw-bg-[color:var(--neutrals-100)] tw-p-3 tw-text-xs tw-text-[color:var(--neutrals-700)]"
                >{{ check.detail }}</pre
              >
            </div>
          </div>
        </VcCard>
      </VcForm>
    </VcContainer>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useAnalyticsDiagnostics, useStores } from "../composables";
import { VcBlade, VcContainer, VcForm, VcCard, VcSelect, VcSwitch, VcButton } from "@vc-shell/framework/ui";

defineBlade({
  url: "/analytics-diagnostics",
  name: "SalesRepAnalyticsDiagnostics",
  isWorkspace: true,
  menuItem: {
    title: "VC_SALES_REP.MENU.ANALYTICS_DIAGNOSTICS",
    icon: "lucide-activity",
    priority: 80,
    permissions: ["sales-rep:diagnostics"],
  },
});

const { t, te } = useI18n({ useScope: "global" });

const { checks, hasRun, runDiagnostics, runningDiagnostics } = useAnalyticsDiagnostics();
const { stores, loadStores, loadingStores } = useStores();

const storeOptions = computed(() => stores.value.map((x) => ({ id: x.id, name: x.name })));

const storeId = ref<string>();
const includeLiveData = ref(true);
const expanded = ref<Record<number, boolean>>({});

// Server statuses/stages are fixed tokens; unknown values (future stages) fall back to the raw token.
const neutralStatusClasses =
  "tw-border-[color:var(--neutrals-300)] tw-bg-[color:var(--neutrals-50)] tw-text-[color:var(--neutrals-600)]";

const statuses: Record<string, { key: string; classes: string }> = {
  Passed: {
    key: "PASSED",
    classes: "tw-border-[color:var(--success-300)] tw-bg-[color:var(--success-50)] tw-text-[color:var(--success-700)]",
  },
  Warning: {
    key: "WARNING",
    classes: "tw-border-[color:var(--warning-300)] tw-bg-[color:var(--warning-50)] tw-text-[color:var(--warning-700)]",
  },
  Failed: {
    key: "FAILED",
    classes: "tw-border-[color:var(--danger-300)] tw-bg-[color:var(--danger-50)] tw-text-[color:var(--danger-700)]",
  },
  Skipped: { key: "SKIPPED", classes: neutralStatusClasses },
};

const statusClasses = (status?: string) => statuses[status ?? ""]?.classes ?? neutralStatusClasses;

const stageKeys: Record<string, string> = {
  configuration: "CONFIGURATION",
  credentials: "CREDENTIALS",
  apiAccess: "API_ACCESS",
  customDimensions: "CUSTOM_DIMENSIONS",
  reportCompatibility: "REPORT_COMPATIBILITY",
  realtime: "REALTIME",
  processedData: "PROCESSED_DATA",
  featureQuery: "FEATURE_QUERY",
};

const localized = (prefix: string, key: string | undefined, token?: string) => {
  const fullKey = `${prefix}.${key ?? ""}`;
  return key && te(fullKey) ? t(fullKey) : (token ?? "");
};

const statusLabel = (status?: string) =>
  localized("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.STATUSES", statuses[status ?? ""]?.key, status);
const stageLabel = (stage?: string) =>
  localized("VC_SALES_REP.PAGES.ANALYTICS_DIAGNOSTICS.STAGES", stageKeys[stage ?? ""], stage);

const toggleDetail = (index: number) => {
  expanded.value[index] = !expanded.value[index];
};

const onRun = async () => {
  expanded.value = {};
  await runDiagnostics({ storeId: storeId.value, includeLiveData: includeLiveData.value });
};

onMounted(async () => {
  await loadStores();
  if (!storeId.value) {
    storeId.value = stores.value[0]?.id;
  }
});
</script>
