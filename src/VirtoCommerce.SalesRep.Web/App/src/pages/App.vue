<template>
  <VcApp
    :is-ready="isReady"
    :logo="logoImage"
    title="Sales Reps"
    :version="version"
  >
  </VcApp>
</template>

<script lang="ts" setup>
import { useUser } from "@vc-shell/framework";
import { onMounted, ref } from "vue";
import logoImage from "/assets/logo.svg";

import { VcApp } from "@vc-shell/framework/ui";

const isReady = ref(false);
const version = import.meta.env.PACKAGE_VERSION;

const { isAuthenticated } = useUser();

onMounted(() => {
  isReady.value = isAuthenticated.value;
});
</script>

<style lang="scss">
@use "./../styles/index.scss";
</style>
