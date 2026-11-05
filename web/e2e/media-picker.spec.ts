import { expect, test } from '@playwright/test';

import { staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The upload of a picker is offered to whoever the server would let upload: `POST /api/media` asks for
 * `Media.Edit` on the department, and without this the button sits at the top of the picker and answers
 * every file with a refusal. The new page's own picture is the picker this screen always draws.
 */

const picker = englishCommon.media.picker;

test('a coordinator who may upload is offered the upload at the top of the picker', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page.goto('/staff/content/new?kind=Page&department=ED');

  await expect(page.getByRole('searchbox', { name: picker.search }).first()).toBeAttached();
  await expect(page.getByRole('button', { name: picker.upload, exact: true }).first()).toBeAttached();
});

test('somebody without the permission of the library is not offered the upload', async ({ page }) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: staffBootstrap.permissions.filter((permission) => permission.name !== 'Media.Edit'),
  });
  await page.goto('/staff/content/new?kind=Page&department=ED');

  // The picker is there, and looks for a file like everybody's: only the upload is gone.
  await expect(page.getByRole('searchbox', { name: picker.search }).first()).toBeAttached();
  await expect(page.getByRole('button', { name: picker.upload, exact: true })).toHaveCount(0);
  await expect(page.getByText(picker.dropHint)).toHaveCount(0);
});
