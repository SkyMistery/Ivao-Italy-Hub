import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

/**
 * The English language file of the division, read from disk.
 *
 * Read rather than imported because Playwright's ESM loader wants an import attribute for JSON that
 * the rest of the toolchain does not; and read rather than retyped because a test carrying its own
 * copy of a sentence passes while the screen shows a raw key. It is the same file the browser
 * fetches from `/locales/en/common.json`.
 */
interface CommonStrings {
  readonly home: { readonly heading: string };
  readonly footer: { readonly version: string };
  readonly theme: { readonly toggle: string };
  readonly auth: { readonly login: string };
}

export const englishCommon = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
) as CommonStrings;
