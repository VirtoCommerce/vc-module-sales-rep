import { createSharedComposable, useNow } from "@vueuse/core";

// One minute clock for the whole hub: the compact activity rows age their "~N ago" on it, and Reporting moves its
// date windows on at midnight. A minute is the finest unit either prints.
export const useMinuteClock = createSharedComposable(() => useNow({ interval: 60_000 }));
