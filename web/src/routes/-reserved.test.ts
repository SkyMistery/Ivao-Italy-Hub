import { readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

import { BACKEND_PATHS } from '../../backendPaths';

/**
 * The first segments no page may take (note 2026-09-13-contenuti-centralizzati, 3.7): every address
 * the application answers for itself. The server holds the list — it is the one that refuses a page
 * called `news` — and this reads it, and compares it with what the front end actually answers for:
 * the routes under `_public`, `_member` and `_staff`, and `BACKEND_PATHS`. A route added on either
 * side and forgotten in the list fails here, and not on the day a coordinator names a page after it
 * and the page cannot be reached.
 */

const repository = resolve(process.cwd(), '..');

function reservedOnTheServer(): string[] {
  const source = readFileSync(resolve(repository, 'src/IvaoHub.Core/Content/ContentAddresses.cs'), 'utf8');
  const block = /ReservedSegments\s*=\s*\[([\s\S]*?)\];/.exec(source)?.[1];
  expect(block, 'ContentAddresses.ReservedSegments is no longer a literal list').toBeDefined();
  return [...block!.matchAll(/"([^"]+)"/g)].map((match) => match[1]!);
}

/** `news.$slug.tsx` → `news`; `index.tsx`, `$.tsx` and a layout's own file answer for no segment. */
function firstSegmentsOfRoutes(): string[] {
  const segments = new Set<string>();

  for (const layout of ['_public', '_member', '_staff']) {
    for (const file of readdirSync(resolve(process.cwd(), 'src/routes', layout))) {
      const first = file.replace(/\.tsx$/, '').split('.')[0]!;
      if (first !== 'index' && !first.startsWith('$') && !first.startsWith('-')) {
        segments.add(first);
      }
    }
  }

  return [...segments];
}

test('every address the application answers for is one no page may take', () => {
  const reserved = reservedOnTheServer();

  const answered = [
    ...firstSegmentsOfRoutes(),
    ...BACKEND_PATHS.map((path) => path.replace(/^\//, '').split('/')[0]!),
  ];

  expect(answered.length).toBeGreaterThan(10);
  expect(answered.filter((segment) => !reserved.includes(segment))).toEqual([]);
});
