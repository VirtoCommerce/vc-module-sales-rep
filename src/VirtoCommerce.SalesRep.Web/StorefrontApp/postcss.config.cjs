const path = require("path");

// Same pipeline as the host, with Tailwind pinned to THIS plugin's config so it scans
// the plugin's own sources and generates the utilities its templates use.
module.exports = {
  plugins: {
    "postcss-import": {},
    "tailwindcss/nesting": {},
    tailwindcss: { config: path.resolve(__dirname, "tailwind.config.cjs") },
    autoprefixer: {},
  },
};
