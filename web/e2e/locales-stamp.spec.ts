import { expect, test } from '@playwright/test';

import { stubTheApi } from './fixtures';
import { englishCommon } from './locales';

/**
 * The language files follow the release (note `2026-10-17-i-file-delle-lingue-seguono-la-release`): the built
 * bundle asks for every one of them with the fingerprint of the files after `?v=`, one fingerprint for all, and a
 * static server that knows nothing of it still hands out the file. Before 17 October 2026 the address was the same
 * in every release, and after the delivery of 0.8.0 a browser that had kept the old `common.json` showed the keys
 * of the new page raw.
 */
test('the built bundle asks for the language files with the fingerprint of the release', async ({ page }) => {
  await stubTheApi(page);

  const asked: string[] = [];
  page.on('response', (response) => {
    const url = new URL(response.url());
    if (url.pathname.startsWith('/locales/')) {
      expect(response.status(), url.pathname).toBe(200);
      asked.push(url.search);
    }
  });

  await page.goto('/');
  // The footer is built from the language files: drawn, they arrived.
  await expect(
    page.getByText(
      englishCommon.footer.rights
        .replace('{{year}}', String(new Date().getFullYear()))
        .replace('{{division}}', 'IVAO Example')
        .replace('{{build}}', '0.0.0-e2e · abc1234'),
    ),
  ).toBeVisible();

  expect(asked.length).toBeGreaterThan(0);
  expect(asked[0]).toMatch(/^\?v=[0-9a-f]{12}$/);
  expect(new Set(asked).size).toBe(1);
});
