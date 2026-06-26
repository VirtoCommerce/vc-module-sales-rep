import { computed } from "vue";

interface Option {
  id: string;
  title: string;
}

// Common cultures for a contact's preferred language (Intl has no enumerable locale list).
const LANGUAGE_CODES = [
  "en-US",
  "en-GB",
  "de-DE",
  "fr-FR",
  "es-ES",
  "it-IT",
  "pt-PT",
  "pt-BR",
  "nl-NL",
  "pl-PL",
  "ru-RU",
  "uk-UA",
  "cs-CZ",
  "sv-SE",
  "fi-FI",
  "da-DK",
  "nb-NO",
  "tr-TR",
  "ar-SA",
  "ja-JP",
  "ko-KR",
  "zh-CN",
];

function supportedValues(key: "timeZone" | "currency"): string[] {
  try {
    const intl = Intl as unknown as { supportedValuesOf?: (k: string) => string[] };
    return typeof intl.supportedValuesOf === "function" ? intl.supportedValuesOf(key) : [];
  } catch {
    return [];
  }
}

function displayName(type: "currency" | "language", code: string): string | undefined {
  try {
    return new Intl.DisplayNames(["en"], { type }).of(code);
  } catch {
    return undefined;
  }
}

export default () => {
  const timeZones = computed<Option[]>(() => supportedValues("timeZone").map((tz) => ({ id: tz, title: tz })));

  const currencies = computed<Option[]>(() =>
    supportedValues("currency").map((code) => {
      const name = displayName("currency", code);
      return { id: code, title: name && name !== code ? `${code} — ${name}` : code };
    }),
  );

  const languages = computed<Option[]>(() =>
    LANGUAGE_CODES.map((code) => {
      const name = displayName("language", code);
      return { id: code, title: name ? `${name} (${code})` : code };
    }),
  );

  return {
    timeZones,
    currencies,
    languages,
  };
};
