# Migration Report: 1.1.87 → 2.0.6

Generated: 2026-05-29

## ✅ Completed by AI (2026-05-29)

All AI-assisted manual topics were migrated and verified. **`vue-tsc --noEmit` passes (0 errors)** and **`yarn build` succeeds**.

- ✅ **nswag-class-to-interface** — `news-article-details.vue`: `new NewsArticleLocalizedContent()` / `new SeoInfo()` clone-then-mutate → object literals with `as` assertions.
- ✅ **use-blade-form** — `news-article-details.vue`: replaced `useForm` (vee-validate) + `onBeforeClose` + modification tracking with a single `useBladeForm({ data, closeConfirmMessage, canSaveOverride })`; `onBeforeClose` deleted.
- ✅ **vctable-audit** — 5 list pages: `<VcTable>` → `<VcDataTable>` with `<VcColumn v-for>`, `useTableSort`→`useDataTableSort`, selection via `v-model:selection`, `@item-click`→`@row-click`.
- ✅ **use-data-table-pagination-audit** — 5 list pages: removed manual `onPaginationClick`, wired `useDataTablePagination` + `@pagination-click="pagination.goToPage"`.
- ✅ **icon-audit** — `material-*` icons replaced with lucide equivalents across details + 4 list pages.
- ✅ **manual-migration-audit** — `useNewsArticleListUI/index.ts`: `closeBlade()`→`closeChildren()`, `openBlade({ blade:{name} })`→`openBlade({ name })`.

### Not actionable / false positives

- **remove-release-config** — informational; the CLI already removed the deprecated config, script and `scripts/` dir.
- **menu-group-config** — false positive: the flagged `group: ""` is an editor-toolbar-button field, not a blade menu property. `defineBlade` has no `group`. No change needed.

### Pre-existing lint errors — fixed

- ✅ 4 `vue/no-side-effects-in-computed-properties` errors in `news-article-details.vue` (pre-existing, surfaced after the eslint cache reset). Refactored: `selectedLocalizedContent` / `selectedSeo` are now pure `find` computeds with a fallback; array initialization and `push` of the per-locale entry moved into a `watch([newsArticle, currentLocale], …, { immediate: true })`. Behavior preserved. **ESLint: 0 errors.**

### Additional follow-up applied (post-migration)

- ESLint migrated to flat config (`eslint.config.mjs`, modeled on `vendor-portal`); legacy `.eslintrc.js`/`.eslintignore` removed; obsolete eslint-8-era devDependencies pruned. Prettier run across the project.

## Automated Changes (46 files)

- ✅ **define-app-module** — 1 file(s)
- ✅ **use-blade-migration** — 3 file(s)
- ✅ **icon-replace** — 3 file(s)
- ✅ **remove-global-components** — 7 file(s)
- ✅ **vc-blade-loading-prop** — 6 file(s)
- ✅ **remove-app-module-options** — 1 file(s)
- ✅ **define-options-to-blade** — 6 file(s)
- ✅ **remove-pathmatch-route** — 1 file(s)
- ✅ **blade-props-simplification** — 6 file(s)
- ✅ **define-expose-to-children** — 6 file(s)
- ✅ **remove-expose-title** — 6 file(s)

## Manual Migration Required

### remove-release-config

- Removed @vc-shell/release-config from devDependencies
- Removed "release" script from package.json
- Deleted scripts/release.ts
- Deleted empty scripts/ directory

### use-blade-migration

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: Complex onBeforeClose callback with multiple returns, manual review needed

### icon-replace

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/composables/useNewsArticleListUI/index.ts: Replaced 3 icon(s) with lucide equivalents
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: Replaced 4 icon(s) with lucide equivalents
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-published.vue: Replaced 1 icon(s) with lucide equivalents

### menu-group-config

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: Found deprecated menu properties: group (string). Migrate to groupConfig: { id, title, icon, priority, permissions }.

### Manual Migration Audit Findings

The audit found patterns that are not safely auto-rewritable (e.g., `useExternalWidgets`, `moment`, `useFunctions`, direct `closeBlade()`). These require targeted manual refactors before final type-check/build.

**Affected files:**

- `src/modules/vc-news/composables/useNewsArticleListUI/index.ts`

```ts
// useFunctions() removed:
// OLD:
const { debounce } = useFunctions();
const debounced = debounce(search, 300);

// NEW:
import { useDebounceFn } from "@vueuse/core";
const debounced = useDebounceFn(search, 300);
```

> See: [migration/03-moment-to-datefns.md](migration/03-moment-to-datefns.md)

### vctable-audit

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-all.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-archived.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-drafts.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-published.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-scheduled.vue: Uses <VcTable> — must be migrated to <VcDataTable>. See migration guide: VcTable → VcDataTable.

### NSwag DTO Class → Interface Migration

API client DTOs changed from classes (with `new DtoClass()`) to interfaces (with `{} as DtoClass`). The migrator handles simple cases automatically. Clone-then-mutate patterns (`new X(); x.field = value;`) require manual rewrite.

**Affected files:**

- `src/modules/vc-news/pages/news-article-details.vue`

```ts
// Clone-then-mutate (manual migration):
// OLD:
const criteria = new SearchCriteria();
criteria.take = 20;
criteria.sort = "name:ASC";

// NEW:
const criteria = { take: 20, sort: "name:ASC" } as SearchCriteria;
```

> See: [migration/nswag-class-to-interface.md](migration/nswag-class-to-interface.md)

### Form Management with useBladeForm()

`useForm()` (vee-validate) + manual `onBeforeClose()` + `modified` tracking are replaced by a single `useBladeForm()` composable. Remove all three and replace with one call. `useBladeForm` handles close confirmation, modification tracking, and form validation automatically.

```ts
// OLD:
import { useForm } from "vee-validate";
const { meta } = useForm({ validateOnMount: false });
const isModified = computed(() => meta.value.dirty);
onBeforeClose(async () => {
  if (isModified.value) {
    return !(await showConfirmation(t("CLOSE_CONFIRMATION")));
  }
});

// NEW:
import { useBladeForm } from "@vc-shell/framework";
const form = useBladeForm({
  data: item, // your reactive data ref
  closeConfirmMessage: computed(() => t("CLOSE_CONFIRMATION")),
});
// form.canSave, form.isModified, form.setBaseline(), form.markReady(), form.revert()
// onBeforeClose is handled automatically — DELETE it
```

> See: [migration/37-use-blade-form.md](migration/37-use-blade-form.md)

### use-data-table-pagination-audit

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-all.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-archived.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-drafts.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-published.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-scheduled.vue: Uses manual onPaginationClick — delete it and bind @pagination-click="pagination.goToPage". See migration guide: useDataTablePagination.

### icon-audit

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: [Material] material-undo → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: [Material] material-archive → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-details.vue: [Material] material-unarchive → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-all.vue: [Material] material-unknown_document → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-archived.vue: [Material] material-archive → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-drafts.vue: [Material] material-edit_document → replace with lucide- equivalent
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/modules/vc-news/pages/news-article-list-scheduled.vue: [Material] material-calendar_month → replace with lucide- equivalent

## Dependencies Updated

- @vc-shell/config-generator: ^1.1.87 → ^2.0.6
- @vc-shell/framework: ^1.1.87 → ^2.0.6
- @vc-shell/api-client-generator: ^1.1.87 → ^2.0.6
- @vc-shell/ts-config: ^1.1.87 → ^2.0.6
- vue: ^3.5.13 → ^3.5.30
- vue-router: ^4.2.5 → ^5.0.3
- @commitlint/cli: ^18.4.3 → ^20.4.1
- @commitlint/config-conventional: ^18.4.3 → ^20.4.1
- @vue/eslint-config-prettier: ^9.0.0 → ^10.2.0
- @vue/eslint-config-typescript: ^12.0.0 → ^14.6.0
- conventional-changelog-cli: ^4.1.0 → ^5.0.0
- eslint: ^8.56.0 → ^9.35.0
- eslint-plugin-vue: ^9.19.2 → ^10.4.0
- vite-plugin-checker: ^0.9.1 → ^0.13.0
- vue-tsc: ^2.2.10 → ^3.2.5

## Not Covered by Migrator

_These migration guides may be relevant — check manually:_

- **29-vc-table-to-data-table** — Old VcTable → VcDataTable migration
  Check: `grep -rn "VcTable\b" src/`

<details>
<summary>Transform Log (10 entries)</summary>

- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/tsconfig.json: added @vc-shell/framework/globals to compilerOptions.types
- /Users/symbot/DEV/vc-module-news/src/VirtoCommerce.News.Web/App/src/shims-vue.d.ts: standard boilerplate — deleted
- Registry: 108 DTO classes, 108 interface→class mappings, package: api
- Found 24 consumer files to scan.
- src/modules/vc-news/composables/useCustomers/index.ts: modified
- src/modules/vc-news/composables/useNewsArticleDetails/index.ts: modified
- src/modules/vc-news/composables/useNewsArticleList/index.ts: modified
- src/modules/vc-news/composables/useStore/index.ts: modified
- src/modules/vc-news/pages/news-article-details.vue: modified
- Done. 5 file(s) modified out of 24 scanned.

</details>
