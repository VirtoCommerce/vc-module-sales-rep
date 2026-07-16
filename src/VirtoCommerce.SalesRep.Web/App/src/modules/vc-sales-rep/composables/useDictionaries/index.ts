import { computed, ref } from "vue";
import { useAsync, useApiClient } from "@vc-shell/framework";
import { SalesRepClient } from "../../../../api_client/virtocommerce.salesrep";

interface Option {
  id: string;
  title: string;
}

// Time zones stay client-side: the classic admin builds this list client-side too (moment.tz), there is no
// backend time-zone catalog, and the full tz database is not VirtoCommerce-configurable data.
function supportedTimeZones(): string[] {
  try {
    const intl = Intl as unknown as { supportedValuesOf?: (k: string) => string[] };
    return typeof intl.supportedValuesOf === "function" ? intl.supportedValuesOf("timeZone") : [];
  } catch {
    return [];
  }
}

// Currencies, languages and countries come from VirtoCommerce data (Core currency catalog, the configured
// "Languages" platform setting, and the platform countries list) via the SalesRep dictionaries endpoint —
// mirroring what the classic customer contact admin shows. Intl.DisplayNames is used only to prettify the
// display label; the stored value is always the code returned by the backend.
function displayName(code: string): string | undefined {
  try {
    return new Intl.DisplayNames(["en"], { type: "language" }).of(code);
  } catch {
    return undefined;
  }
}

export default () => {
  const { getApiClient } = useApiClient(SalesRepClient);

  const currencies = ref<Option[]>([]);
  const languages = ref<Option[]>([]);
  const countries = ref<Option[]>([]);

  const timeZones = computed<Option[]>(() => supportedTimeZones().map((tz) => ({ id: tz, title: tz })));

  const { loading: loadingDictionaries, action: loadDictionaries } = useAsync(async () => {
    const apiClient = await getApiClient();
    const result = await apiClient.getDictionaries();

    currencies.value = (result.currencies ?? [])
      .filter((c) => !!c.code)
      .map((c) => {
        const label = c.name && c.name !== c.code ? `${c.code} — ${c.name}` : (c.code as string);
        return { id: c.code as string, title: c.symbol ? `${label} (${c.symbol})` : label };
      });

    languages.value = (result.languages ?? [])
      .filter((code): code is string => !!code)
      .map((code) => {
        const name = displayName(code);
        return { id: code, title: name ? `${name} (${code})` : code };
      });

    countries.value = (result.countries ?? [])
      .filter((c) => !!c.id)
      .map((c) => ({ id: c.id as string, title: c.name || (c.id as string) }));
  });

  return {
    timeZones,
    currencies,
    languages,
    countries,
    loadDictionaries,
    loadingDictionaries,
  };
};
