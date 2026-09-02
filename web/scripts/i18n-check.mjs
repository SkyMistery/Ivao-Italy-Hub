#!/usr/bin/env node
// Fails when the language files of the division are not interchangeable: every language must
// carry exactly the same keys, otherwise a fork ends up with an untranslated screen.
// F6 extends this script to the keys actually used by the code.
import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const localesDir = fileURLToPath(new URL('../../locales', import.meta.url));

function flatten(value, prefix = '') {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return [prefix];
  }
  return Object.entries(value).flatMap(([key, child]) => flatten(child, prefix ? `${prefix}.${key}` : key));
}

async function readNamespace(locale, namespace) {
  const content = await readFile(new URL(`${locale}/${namespace}`, `file://${localesDir}/`), 'utf8');
  return new Set(flatten(JSON.parse(content)));
}

const locales = (await readdir(localesDir, { withFileTypes: true }))
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name)
  .sort();

if (locales.length === 0) {
  console.error('No language directory found under locales/.');
  process.exit(1);
}

const [reference, ...others] = locales;
const namespaces = (await readdir(new URL(`${reference}/`, `file://${localesDir}/`))).sort();
const problems = [];

for (const locale of others) {
  const files = new Set(await readdir(new URL(`${locale}/`, `file://${localesDir}/`)));
  for (const namespace of namespaces) {
    if (!files.has(namespace)) {
      problems.push(`${locale}/${namespace} is missing`);
      continue;
    }
    const expected = await readNamespace(reference, namespace);
    const actual = await readNamespace(locale, namespace);
    for (const key of expected) {
      if (!actual.has(key)) problems.push(`${locale}/${namespace}: missing key "${key}"`);
    }
    for (const key of actual) {
      if (!expected.has(key)) problems.push(`${locale}/${namespace}: extra key "${key}"`);
    }
  }
  for (const namespace of files) {
    if (!namespaces.includes(namespace))
      problems.push(`${locale}/${namespace} has no counterpart in ${reference}/`);
  }
}

if (problems.length > 0) {
  console.error(`i18n check failed (${problems.length} problem(s)):`);
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}

console.log(`i18n check passed: ${locales.join(', ')} share ${namespaces.length} namespace(s).`);
