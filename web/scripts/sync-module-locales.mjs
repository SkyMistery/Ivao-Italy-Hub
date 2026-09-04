#!/usr/bin/env node
// Copies `web/src/modules/<key>/locales/<lng>/<ns>.json` into `locales/<lng>/<ns>.json`.
//
// A module keeps its own words next to its own code — that is the boundary design M0 §6.5 draws —
// but there is exactly one set of language files at the root of the repository, and it is read by
// three different things: the SPA over HTTP, the backend through `LocaleCatalog`, and `i18n:check`.
// This script is what makes those two facts true at the same time.
//
// The copies are committed, and CI runs this script and then `git diff --exit-code`, the same way
// it does for the generated API client. Two reasons, both learned the hard way by somebody else:
// a `dotnet run` with no pnpm in sight still has to find every language file, and a publish must
// not depend on which of two MSBuild targets happened to run first.
import { readdir, readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const modulesDir = fileURLToPath(new URL('../src/modules', import.meta.url));
const localesDir = fileURLToPath(new URL('../../locales', import.meta.url));

/** Written into every copy, so nobody edits the copy instead of the original. */
const SOURCE_KEY = '_source';

async function directories(path) {
  try {
    const entries = await readdir(path, { withFileTypes: true });
    return entries.filter((entry) => entry.isDirectory()).map((entry) => entry.name);
  } catch {
    return [];
  }
}

const divisionLocales = await directories(localesDir);
const written = [];
const problems = [];

for (const moduleKey of await directories(modulesDir)) {
  const moduleLocales = `${modulesDir}/${moduleKey}/locales`;
  const languages = await directories(moduleLocales);

  if (languages.length === 0) {
    continue;
  }

  // A module that speaks fewer languages than the division would leave a screen half translated,
  // and the check that would normally catch it cannot: it compares the files that exist.
  for (const language of divisionLocales) {
    if (!languages.includes(language)) {
      problems.push(`module "${moduleKey}" has no ${language}/ under locales/`);
    }
  }

  for (const language of languages) {
    if (!divisionLocales.includes(language)) {
      // Not a problem: a module may well ship a language this division does not publish in.
      continue;
    }

    for (const file of await readdir(`${moduleLocales}/${language}`)) {
      if (!file.endsWith('.json')) continue;

      const source = await readFile(`${moduleLocales}/${language}/${file}`, 'utf8');
      const parsed = JSON.parse(source);
      const content = `${JSON.stringify({ [SOURCE_KEY]: `web/src/modules/${moduleKey}/locales`, ...parsed }, null, 2)}\n`;

      await writeFile(`${localesDir}/${language}/${file}`, content, 'utf8');
      written.push(`${language}/${file}`);
    }
  }
}

if (problems.length > 0) {
  console.error(`module locales are not in step (${problems.length} problem(s)):`);
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}

console.log(
  written.length === 0
    ? 'module locales: no module ships language files.'
    : `module locales: copied ${written.length} file(s) into locales/ (${[...new Set(written)].join(', ')}).`,
);
