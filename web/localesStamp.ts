import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

/**
 * The fingerprint of the language files: what the SPA puts after `?v=` when it asks for
 * `/locales/{lng}/{ns}.json` (`app/i18n.ts`), so that a release that changes a word changes the
 * address the browser asks for.
 *
 * ⚠️ Without it a delivery left the new words unread (test.it.ivao.aero, 17 October 2026, 0.8.0):
 * the JS chunks change name with every build, the language files do not, and on that hosting the
 * web server hands them out by itself with a `Last-Modified` and no `Cache-Control` — so the
 * browser kept the old `common.json` by heuristic and the new page showed `connections.…` keys.
 * The hub cannot send a header on a file it never sees; a new address works whoever serves it
 * (note `2026-10-17-i-file-delle-lingue-seguono-la-release`).
 *
 * Every file under the directory counts, by its path and its bytes, so a word changed, a file added
 * and a file moved all change it, and an untouched set gives the same stamp on every machine.
 *
 * It lives in a file of its own because two things read it: `vite.config.ts` writes it into the
 * bundle, and `src/app/localesStamp.test.ts` checks it.
 */
export function localesStamp(directory: string): string {
  const hash = createHash('sha256');
  const files = readdirSync(directory, { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile())
    .map((entry) => join(entry.parentPath, entry.name))
    .map((file) => ({ file, path: relative(directory, file).split(sep).join('/') }))
    .sort((a, b) => (a.path < b.path ? -1 : a.path > b.path ? 1 : 0));

  for (const { file, path } of files) {
    hash.update(path).update('\0').update(readFileSync(file)).update('\0');
  }

  return hash.digest('hex').slice(0, 12);
}
