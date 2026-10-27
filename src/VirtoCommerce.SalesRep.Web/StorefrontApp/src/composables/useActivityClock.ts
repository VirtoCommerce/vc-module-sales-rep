import { createSharedComposable, useNow } from "@vueuse/core";

// One clock for every compact activity row, so "~N ago" ages while the page stays open. A minute is the finest
// unit a row prints.
export const useActivityClock = createSharedComposable(() => useNow({ interval: 60_000 }));
