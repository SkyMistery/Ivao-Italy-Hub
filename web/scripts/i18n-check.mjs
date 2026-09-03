#!/usr/bin/env node
// Two checks, both of which fail the build.
//
//  1. The language files of the division are interchangeable: every language carries exactly the
//     same keys, otherwise a fork ends up with an untranslated screen.
//  2. Every key the code asks for by name exists in every language. A missing one does not throw
//     at runtime — i18next shows the key itself — so nothing but this would catch it.
//
// Only keys written as literals can be checked. A key built at runtime (`${labels}.fields.${path}`
// in the form generator, `visibility.${value}` in a badge) is deliberately out of reach: what keeps
// those honest is the test that renders the component and reads the text back.
import { readdir, readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

const localesDir = fileURLToPath(new URL('../../locales', import.meta.url));
const sourceDir = fileURLToPath(new URL('../src', import.meta.url));

/** `t('some.key'` and `i18n.t("some.key"`, and nothing that is not a plain string. */
const KEY_PATTERN = /\bt\(\s*(['"])([\w.-]+)\1/g;

/** A test may declare keys of its own; the language files of the division do not answer for them. */
const isTest = (file) => /\.test\.tsx?$/.test(file);

function flatten(value, prefix = '') {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return [prefix];
  }
  return Object.entries(value).flatMap(([key, child]) => flatten(child, prefix ? `${prefix}.${key}` : key));
}

async function listFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true, recursive: true });
  return entries
    .filter((entry) => entry.isFile())
    .map((entry) => `${entry.parentPath.replaceAll('\\', '/')}/${entry.name}`);
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

// --- 1. the languages carry the same keys ----------------------------------------------------

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

// --- 2. the keys the code asks for exist -------------------------------------------------------

/** All keys of a language, across namespaces: the application falls back between them. */
const keysByLocale = new Map();
for (const locale of locales) {
  const files = await readdir(new URL(`${locale}/`, `file://${localesDir}/`));
  const keys = new Set();
  for (const namespace of files) {
    for (const key of await readNamespace(locale, namespace)) keys.add(key);
  }
  keysByLocale.set(locale, keys);
}

/**
 * A plural key is stored with its suffix (`list.total_one`, `list.total_other`) and asked for
 * without one, so a key counts as present when any of its plural forms is.
 */
function has(keys, key) {
  if (keys.has(key)) return true;
  for (const candidate of keys) {
    if (candidate.startsWith(`${key}_`)) return true;
  }
  return false;
}

const sources = (await listFiles(sourceDir)).filter(
  (file) => /\.tsx?$/.test(file) && !isTest(file) && !file.endsWith('routeTree.gen.ts'),
);

let used = 0;
for (const file of sources) {
  const content = await readFile(file, 'utf8');
  for (const [, , key] of content.matchAll(KEY_PATTERN)) {
    used += 1;
    for (const locale of locales) {
      if (!has(keysByLocale.get(locale), key)) {
        const where = file.slice(sourceDir.length + 1);
        problems.push(`${where} uses "${key}", which ${locale} does not have`);
      }
    }
  }
}

if (problems.length > 0) {
  console.error(`i18n check failed (${problems.length} problem(s)):`);
  for (const problem of problems) console.error(`  - ${problem}`);
  process.exit(1);
}

console.log(
  `i18n check passed: ${locales.join(', ')} share ${namespaces.length} namespace(s), ` +
    `and all ${used} literal key(s) used in src/ exist in each.`,
);
