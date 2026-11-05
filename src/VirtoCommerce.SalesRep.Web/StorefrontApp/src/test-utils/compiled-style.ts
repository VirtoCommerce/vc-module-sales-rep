import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import postcss from "postcss";
import { compileString } from "sass";
import { parse } from "vue/compiler-sfc";
import type { AcceptedPlugin } from "postcss";

/**
 * A component's compiled `<style>` rules, for layout tests: jsdom has no layout and Vitest skips SFC styles.
 * Runs Sass and this plugin's PostCSS config directly, because Vite's preprocessCSS needs esbuild, which fails in jsdom.
 */
export interface CompiledRuleType {
  selector: string;
  /** The `min-width` of the enclosing media query, in px; 0 outside one. */
  minWidth: number;
  declarations: Record<string, string>;
}

// The PostCSS plugins the build runs, from the plugin's own config.
const require = createRequire(import.meta.url);
const POSTCSS_CONFIG = require("../../postcss.config.cjs") as { plugins: Record<string, object> };

function postcssPlugins(): AcceptedPlugin[] {
  return Object.entries(POSTCSS_CONFIG.plugins).map(([name, options]) => {
    const plugin = require(name) as (options: object) => AcceptedPlugin;
    return plugin(options);
  });
}

// Paths, not URL objects: jsdom's global URL is not accepted by node:url.
const SRC = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** @param component the .vue file, relative to src/ */
export async function compiledStyleRules(component: string): Promise<CompiledRuleType[]> {
  const path = resolve(SRC, component);
  const { descriptor } = parse(readFileSync(path, "utf8"), { filename: path });
  const rules: CompiledRuleType[] = [];

  for (const style of descriptor.styles) {
    const source = style.lang === "scss" ? compileString(style.content).css : style.content;
    const { css: code } = await postcss(postcssPlugins()).process(source, { from: path });
    postcss.parse(code).walkRules((rule) => {
      const media = rule.parent?.type === "atrule" ? (rule.parent as postcss.AtRule) : undefined;
      const minWidth = Number(/min-width:\s*(\d+)px/.exec(media?.params ?? "")?.[1] ?? 0);
      const declarations: Record<string, string> = {};
      rule.walkDecls((declaration) => {
        declarations[declaration.prop] = declaration.value;
      });
      rule.selectors.forEach((selector) => rules.push({ selector: selector.trim(), minWidth, declarations }));
    });
  }

  return rules;
}

/** A property's value for these classes at a viewport: single-class rules tie on specificity, so the last one wins. */
export function cascadedValue(
  rules: CompiledRuleType[],
  classes: string[],
  property: string,
  viewport: number,
): string | undefined {
  return rules
    .filter((rule) => rule.minWidth <= viewport && property in rule.declarations)
    .filter((rule) => classes.some((name) => rule.selector === `.${name}`))
    .at(-1)?.declarations[property];
}

/** A CSS length in px: rem against the 16 px root, a percentage against `basis`. */
export function toPx(value: string | undefined, basis = 0): number | undefined {
  if (!value) {
    return undefined;
  }
  const [, amount, unit] = /^(-?[\d.]+)(px|rem|%)?$/.exec(value.trim()) ?? [];
  if (amount === undefined) {
    return undefined;
  }
  const number = Number(amount);
  if (unit === "rem") {
    return number * 16;
  }
  if (unit === "%") {
    return (number / 100) * basis;
  }
  return number;
}
