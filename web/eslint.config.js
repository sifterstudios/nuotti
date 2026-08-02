// Flat config. ESLint 9 dropped .eslintrc.json, and CI invoked eslint through npx without it
// being a dependency, so it pulled whatever major was newest - eslint@10 - which then could not
// read the legacy config at all. The linter is a pinned devDependency now, so this file and the
// version that reads it move together.
import js from "@eslint/js";
import ts from "typescript-eslint";
import svelte from "eslint-plugin-svelte";
import svelteParser from "svelte-eslint-parser";
import globals from "globals";

export default ts.config(
  {
    // Generated and vendored output. Listed first so nothing below tries to parse it.
    ignores: ["build/", ".svelte-kit/", "node_modules/", "*.config.mjs"],
  },
  js.configs.recommended,
  ...ts.configs.recommended,
  ...svelte.configs["flat/recommended"],
  {
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: "module",
      globals: { ...globals.browser, ...globals.node },
    },
  },
  {
    files: ["**/*.svelte"],
    languageOptions: {
      parser: svelteParser,
      parserOptions: { parser: ts.parser },
    },
  },
  {
    // Carried over from .eslintrc.json, plus varsIgnorePattern: the old config set only
    // argsIgnorePattern, so a deliberately unused *variable* marked with the same underscore
    // convention still errored - e.g. `let { data: _data } = $props()`, where SvelteKit hands
    // the page a prop this route does not read.
    rules: {
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_", caughtErrorsIgnorePattern: "^_" },
      ],
      "@typescript-eslint/no-explicit-any": "warn",
      "no-console": "warn",
    },
  }
);
