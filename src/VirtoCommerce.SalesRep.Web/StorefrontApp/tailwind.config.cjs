const path = require("path");
const hostPreset = require("@vc-frontend/core/tailwind-preset");

// The HOST's design system (colors via CSS custom properties, spacing, breakpoints).
// Content scanning covers this plugin's sources so `@apply` in <style scoped> blocks
// resolves against the host's tokens. Styling is done per-component via scoped styles
// (see src/pages/*.vue), so this plugin emits NO global utility layer — there is nothing
// to leak into host pages, and no `important` scope is needed.
const preset = hostPreset.default ?? hostPreset;

module.exports = {
  ...preset,
  content: [path.resolve(__dirname, "index.html"), path.resolve(__dirname, "src/**/*.{vue,js,ts}")],
};
