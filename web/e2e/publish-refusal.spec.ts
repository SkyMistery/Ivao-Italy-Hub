import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import { oneTemplate, staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * A publication the server refuses (#539). Found on the bench while checking #537: a Video from a host the hub does not
 * frame, the line written, «Publish» pressed in the dialog, a 400 — and the writer was told only that the page "cannot be
 * published yet", while the reason was in the list above the form, which may well be off the screen. The refusal is now
 * said where the writer is looking, in the server's own words, as every write without a form of its own says it
 * (`useRefusalNotice`): the dialog closes and the toast says why.
 */

function language(file: string) {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL(`../../locales/en/${file}`, import.meta.url)), 'utf8'),
  ) as {
    common: { notDone: string };
    toast: { region: string };
    errors: { content: { addressNotFramed: string; review: { inReview: string } } };
  };
}

const common = language('common.json');
const serverWords = language('errors.json');

const aPage = {
  ...oneTemplate,
  id: 7,
  kind: 'Page',
  slug: 'welcome',
  path: 'welcome',
  ownerDepartment: 'ED',
  visibility: 'Public',
  isTemplate: false,
  publishedAt: null,
  draftFromFile: false,
  title: { en: 'Welcome', it: 'Benvenuti' },
  rowVersion: '2026-11-23T10:00:00',
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 'sec-intro',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [
          {
            id: 'blk-video',
            type: 'video',
            version: 1,
            props: { url: 'https://evil.example/video.mp4', aspect: '16x9' },
          },
        ],
        sections: [],
      },
    ],
  },
};

/** The page, whoever may approve it, and a publication the server answers with `refusal`. */
async function stubAPublicationRefused(
  page: Page,
  refusal: Record<string, string[]>,
  problems: Record<string, string[]>,
) {
  // Whoever may approve publishes straight away: the dialog of the line is the toolbar's.
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [
      ...staffBootstrap.permissions,
      ...staffBootstrap.permissions
        .filter((permission) => permission.name === 'Content.Edit')
        .map((permission) => ({ ...permission, name: 'Content.Approve' })),
    ],
  });
  await page.route('**/api/content/7', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
  );
  await page.route('**/api/content/7/publish-problems', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ errors: problems, localized: {} }),
    }),
  );
  await page.route('**/api/content/7/review', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        firstPublication: true,
        sections: [],
        blocks: { added: 0, changed: 0, removed: 0 },
        fields: [],
      }),
    }),
  );

  const publications: unknown[] = [];
  await page.route('**/api/content/7/publish', async (route) => {
    publications.push(route.request().postDataJSON());
    // What `CrudProblems.Validation` answers.
    await route.fulfill({
      status: 400,
      contentType: 'application/problem+json',
      body: JSON.stringify({
        title: 'One or more validation errors occurred.',
        status: 400,
        errors: refusal,
        localized: {},
      }),
    });
  });

  return publications;
}

async function publish(page: Page) {
  await page.goto('/staff/content/7');
  await page.getByRole('button', { name: englishCommon.content.editor.publish, exact: true }).click();
  const dialog = page.getByRole('alertdialog');
  await dialog.getByLabel(englishCommon.content.editor.publishDialog.changelog).fill('A film');
  await dialog
    .getByRole('button', { name: englishCommon.content.editor.publishDialog.confirm, exact: true })
    .click();
  return dialog;
}

test('a refused publication closes the dialog and says why, in the server’s words', async ({ page }) => {
  const refusal = { 'body.sections[0].blocks[0].props.url': ['errors.content.addressNotFramed'] };
  const publications = await stubAPublicationRefused(page, refusal, refusal);

  const dialog = await publish(page);

  await expect.poll(() => publications.length).toBe(1);
  await expect(dialog).toHaveCount(0);
  const toasts = page.getByRole('region', { name: common.toast.region });
  await expect(toasts).toContainText(common.common.notDone);
  await expect(toasts).toContainText(serverWords.errors.content.addressNotFramed);
  await page.screenshot({ path: test.info().outputPath('refused.png') });
});

test('a refusal the list of what is missing does not name is said too', async ({ page }) => {
  // A page somebody else marked ready in the meantime: nothing is missing, and the server still says no.
  const publications = await stubAPublicationRefused(
    page,
    { status: ['errors.content.review.inReview'] },
    {},
  );

  const dialog = await publish(page);

  await expect.poll(() => publications.length).toBe(1);
  await expect(dialog).toHaveCount(0);
  await expect(page.getByRole('region', { name: common.toast.region })).toContainText(
    serverWords.errors.content.review.inReview,
  );
});
