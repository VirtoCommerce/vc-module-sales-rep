import { createSharedComposable, useNow } from "@vueuse/core";
import { computed } from "vue";
import { local } from "../utils";

/**
 * The start of the user's current day, as epoch ms. It changes once a day, so date windows built on it move on at
 * midnight while the page stays open, without re-running every minute in between.
 */
export const useToday = createSharedComposable(() => {
  const now = useNow({ interval: 60_000 });
  return computed(() => local(now.value.getFullYear(), now.value.getMonth(), now.value.getDate()));
});
