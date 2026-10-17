import type { i18n as I18n } from 'i18next';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { resolve } from 'node:path';

import { localesStamp } from '../../localesStamp';
import { createI18n, LOCALES_STAMP, SHIPPED_LANGUAGES } from './i18n';

/**
 * What the SPA asks `/locales/` for when a browser speaks a regional tag. Before 28 September 2026
 * every load from `en-GB` or `it-IT` fetched `/locales/en-GB/*.json` first — a 404 per namespace —
 * and only then the language that has files.
 */
describe('createI18n', () => {
  const requested: string[] = [];

  beforeEach(() => {
    requested.length = 0;
    vi.stubGlobal(
      'fetch',
      vi.fn((url: string) => {
        requested.push(url);
        return Promise.resolve(
          new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }),
        );
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function withBrowserLanguages(languages: string[]) {
    vi.spyOn(window.navigator, 'languages', 'get').mockReturnValue(languages);
    vi.spyOn(window.navigator, 'language', 'get').mockReturnValue(languages[0] ?? '');
  }

  async function started(languages: readonly string[]): Promise<I18n> {
    const i18n = createI18n(['common', 'errors'], languages);
    await new Promise<void>((resolve) => {
      if (i18n.isInitialized) resolve();
      else i18n.on('initialized', () => resolve());
    });
    await vi.waitFor(() => expect(requested.length).toBeGreaterThan(0));
    return i18n;
  }

  it('reads a regional tag as its language and asks only for files that exist', async () => {
    withBrowserLanguages(['xx-YY']);

    const i18n = await started(['en', 'xx']);

    expect(i18n.language).toBe('xx');
    expect(requested.every((url) => /^\/locales\/(xx|en)\//.test(url))).toBe(true);
    expect(requested).not.toContainEqual(expect.stringContaining('xx-YY'));
  });

  it("follows the reader's first choice, not an exact match further down their list", async () => {
    withBrowserLanguages(['en-GB', 'xx']);

    const i18n = await started(['en', 'xx']);

    expect(i18n.language).toBe('en');
    expect(requested.every((url) => url.startsWith('/locales/en/'))).toBe(true);
  });

  it('falls back to English, without asking for a language that has no files', async () => {
    withBrowserLanguages(['de-DE', 'de']);

    const i18n = await started(['en', 'xx']);

    expect(i18n.language).toBe('en');
    expect(requested.every((url) => url.startsWith('/locales/en/'))).toBe(true);
  });

  it('knows the languages of the build from the directories under locales/', () => {
    expect(SHIPPED_LANGUAGES).toContain('en');
    expect(SHIPPED_LANGUAGES.every((language) => !language.includes('-'))).toBe(true);
  });

  // Before 17 October 2026 the address was the same in every release, and a browser that had cached
  // the old common.json showed the keys of a new page raw after a delivery.
  it('asks for every file with the fingerprint of the language files of the build', async () => {
    withBrowserLanguages(['en']);

    await started(['en', 'xx']);

    expect(LOCALES_STAMP).toMatch(/^[0-9a-f]{12}$/);
    expect(LOCALES_STAMP).toBe(localesStamp(resolve(process.cwd(), '../locales')));
    expect(requested.length).toBeGreaterThan(0);
    expect(requested.every((url) => url.endsWith(`.json?v=${LOCALES_STAMP}`))).toBe(true);
  });
});
