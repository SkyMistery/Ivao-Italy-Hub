import { existsSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

import js from '@eslint/js';
import importX, { createNodeResolver } from 'eslint-plugin-import-x';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import globals from 'globals';
import tseslint from 'typescript-eslint';

const projectRoot = import.meta.dirname;
const modulesRoot = join(projectRoot, 'src', 'modules');

/**
 * Directories that hold core code: none of them may depend on a module (design M0 section 6.5).
 * `app/` is the one exception, and only for `src/modules/index.ts`: the design puts the loader of
 * the manifests there, so it has to be able to read the list. It still may not reach inside a
 * module, which is what the second set of zones below says.
 */
const coreDirectories = ['blocks', 'features', 'routes', 'shared'];

const moduleKeys = existsSync(modulesRoot)
  ? readdirSync(modulesRoot, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
  : [];

/**
 * One zone per ordered pair of modules, derived from the folders that exist: adding a module under
 * `src/modules/` extends the rule on its own, no list to maintain.
 */
const crossModuleZones = moduleKeys.flatMap((from) =>
  moduleKeys
    .filter((target) => target !== from)
    .map((target) => ({
      target: `./src/modules/${target}`,
      from: `./src/modules/${from}`,
      message: 'A module never imports from another module: go through the core instead.',
    })),
);

const coreZones = coreDirectories.map((directory) => ({
  target: `./src/${directory}`,
  from: './src/modules',
  message: 'The core must not depend on a module. Modules contribute through their manifest.',
}));

/**
 * `app/` may read `src/modules/index.ts`, the list of manifests, and nothing else under it: the
 * insides of a module are the module's own business, composed through the manifest.
 */
const loaderZones = moduleKeys.map((key) => ({
  target: './src/app',
  from: `./src/modules/${key}`,
  message: "Read the manifest list in src/modules/index.ts, never a module's own files.",
}));

/**
 * The three project rules of design M0 sections 6.5 and 7.1 are enforced here, not by review:
 *  - no hand written `fetch` outside `src/shared/api`;
 *  - no inline `<svg>` outside `src/shared/icons` and `src/blocks`;
 *  - no import from the core into `modules/`, and none between two modules.
 */
export default tseslint.config(
  {
    ignores: ['dist/**', 'node_modules/**', 'src/routeTree.gen.ts', 'src/shared/api/schema.d.ts'],
  },
  js.configs.recommended,
  tseslint.configs.recommendedTypeChecked,
  {
    languageOptions: {
      globals: { ...globals.browser },
      parserOptions: {
        projectService: true,
        tsconfigRootDir: projectRoot,
      },
    },
    settings: {
      'import-x/resolver-next': [
        createNodeResolver({
          extensions: ['.ts', '.tsx', '.js', '.jsx', '.json'],
          tsconfig: { configFile: join(projectRoot, 'tsconfig.json') },
        }),
      ],
    },
    plugins: {
      'import-x': importX,
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],
      '@typescript-eslint/consistent-type-imports': 'error',
      'no-restricted-globals': [
        'error',
        {
          name: 'fetch',
          message: 'Use the generated client in src/shared/api instead of calling fetch directly.',
        },
      ],
      'no-restricted-syntax': [
        'error',
        {
          selector: 'JSXOpeningElement[name.name="svg"]',
          message:
            'Icons live in src/shared/icons; blocks may draw their own. Never inline an SVG in a screen.',
        },
      ],
      'import-x/no-restricted-paths': [
        'error',
        {
          basePath: projectRoot,
          zones: [...coreZones, ...loaderZones, ...crossModuleZones],
        },
      ],
    },
  },
  {
    // A TanStack route file exports both its Route and its component: that is the shape the
    // generator expects, so fast refresh does not get a say.
    //
    // A guard also stops a navigation by throwing the result of `redirect()`, which is a `Response`
    // and not an `Error`. That is how the router is meant to be used (design M0 section 7.3), and
    // it is allowed here rather than everywhere: throwing a non error anywhere else is still a bug.
    files: ['src/routes/**/*.tsx'],
    rules: {
      'react-refresh/only-export-components': 'off',
      '@typescript-eslint/only-throw-error': ['error', { allow: [{ from: 'lib', name: 'Response' }] }],
    },
  },
  {
    files: ['src/shared/api/**/*.{ts,tsx}'],
    rules: { 'no-restricted-globals': 'off' },
  },
  {
    files: ['src/shared/icons/**/*.tsx', 'src/blocks/**/*.tsx'],
    rules: { 'no-restricted-syntax': 'off' },
  },
  {
    files: ['**/*.js', '**/*.mjs'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    files: ['scripts/**/*.mjs', 'vite.config.ts', 'eslint.config.js'],
    languageOptions: { globals: { ...globals.node } },
  },
);
