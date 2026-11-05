import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

import { aboutKeyOf } from '../shared/modules';

import { registry } from './registry';

/**
 * Every block says what it is for, in every language of the division (PR 3.0a,
 * `decisions/2026-10-30-una-pagina-scritta-con-claude.md` §3 and answer 1).
 *
 * The sentence is read in two places: the line at the top of the block's properties panel, and the
 * instructions «Download for Claude» composes, where it is how an assistant chooses a block for what a
 * page has to say. A block without it is a block the assistant can only guess at — so the sentence is
 * asked of **every** block the build registers, the modules' included, and not of the ones somebody
 * remembered.
 *
 * `pnpm i18n:check` cannot see these keys: they are built from the registration, which is the case it
 * says is out of its reach. Every language directory under `locales/` is read, as that script reads
 * them, so a fork that adds a language answers for it here too.
 */

/** Read from the working directory, which is `web/`: under jsdom `import.meta.url` is not a file. */
const LOCALES = resolve(process.cwd(), '..', 'locales');

const languages = readdirSync(LOCALES, { withFileTypes: true })
  .filter((entry) => entry.isDirectory())
  .map((entry) => entry.name);

function sentence(language: string, key: string): unknown {
  const [namespace, path] = key.includes(':') ? key.split(':', 2) : ['common', key];
  const catalogue: unknown = JSON.parse(
    readFileSync(resolve(LOCALES, language, `${namespace ?? 'common'}.json`), 'utf8'),
  );

  return (path ?? '')
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node !== null && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      catalogue,
    );
}

test('the languages of the division are read', () => {
  expect(languages.length).toBeGreaterThan(0);
});

test.each(registry.blocks.map((block) => [block.type, aboutKeyOf(block)] as const))(
  'the block %s says what it is for, in every language (%s)',
  (_type, key) => {
    for (const language of languages) {
      const value = sentence(language, key);
      expect(typeof value === 'string' && value.trim().length > 0, `${language}: ${key}`).toBe(true);
    }
  },
);
