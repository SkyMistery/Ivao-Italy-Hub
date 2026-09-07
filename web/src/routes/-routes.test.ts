import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

/**
 * What a back office screen may read its row from.
 *
 * ⚠️ Not the loader. A loader runs on navigation and never again: the screens used to hold the row
 * `Route.useLoaderData()` returned, so after one save they still carried the `rowVersion` from when
 * the page opened, and the **second** save of a page load was answered 409 — "somebody else changed
 * this in the meantime", blaming somebody who did not exist. Every detail screen in the back office
 * had it, because the recipe was copied faithfully eleven times (design M0 §7.3, found in G12 by
 * recopying a page by hand).
 *
 * Two things hid it. The end-to-end round reloads the page before editing again, for a reason its
 * comment explains as timing; and `Route.useLoaderData()` types as `never` in these files, which is
 * assignable to everything, so TypeScript was checking nothing at all there.
 *
 * The loader stays: it is the **preload**, and `ensureQueryData` is what makes the screen render
 * with data already in hand. What the screen *reads* is the query the loader filled.
 */

// ⚠️ The file is named with a leading `-` because it lives under `routes/`: the TanStack Router
// plugin scans that tree and warns about every file that does not export a `Route`, and the dash is
// the prefix it is configured to ignore.
const staff = resolve(process.cwd(), 'src/routes/_staff');

const files = readdirSync(staff).filter((name) => name.endsWith('.tsx'));

test('the back office has route files to check', () => {
  // The guard that stops the assertion below from passing because a path went stale.
  expect(files.length).toBeGreaterThan(10);
});

/** The file with its comments taken out, because half of them talk about the very thing forbidden. */
function code(name: string): string {
  return readFileSync(resolve(staff, name), 'utf8')
    .replace(/\/\*[\s\S]*?\*\//g, ' ')
    .replace(/\/\/.*$/gm, ' ');
}

test('no back office screen reads its row from the loader', () => {
  const offenders = files.filter((name) => code(name).includes('useLoaderData'));

  expect(offenders).toEqual([]);
});

test('and the check reads the code rather than the prose about it', () => {
  // Written because the first version of the test above failed on all eleven files at once: every
  // one of them carries a comment explaining why the loader is not read, and the word was enough.
  expect(files.some((name) => readFileSync(resolve(staff, name), 'utf8').includes('useLoaderData'))).toBe(
    true,
  );
});
