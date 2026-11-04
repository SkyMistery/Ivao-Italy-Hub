import { readFile } from 'node:fs/promises';

import { expect, test } from '@playwright/test';

import { oneTemplate, staffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * «Download for Claude» (PR 3.0a; note 2026-10-30-una-pagina-scritta-con-claude §3, §4): one press in the toolbar of
 * the editor, and the browser saves a Markdown file composed at that moment. A unit test composes the instructions;
 * this one presses the button in a real browser and reads the file that arrives, because a download that composes
 * well and is never handed to the browser is the failure nobody would see in a test of the composer.
 */

const download = englishCommon.content.claude.download;

/** A page of the coordinator's own department, with one block: the row the editor opens. */
const aPage = {
  ...oneTemplate,
  id: 7,
  kind: 'Page',
  slug: 'welcome',
  path: 'welcome',
  ownerDepartment: 'ED',
  visibility: 'Public',
  isTemplate: false,
  title: { en: 'Welcome', it: 'Benvenuti' },
  rowVersion: '2026-11-04T10:00:00',
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
          { id: 'blk-hello', type: 'text', version: 1, props: { markdown: { en: 'Hello', it: 'Ciao' } } },
        ],
        sections: [],
      },
    ],
  },
};

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  // After the staff's own routes, so that it wins for this row (Playwright matches the last one first).
  await page.route('**/api/content/7', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
  );

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the editor of a page downloads the instructions, with the draft and the header to copy', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/7');

  const button = page.getByRole('button', { name: download.label });
  await expect(button).toBeVisible();

  const [file] = await Promise.all([page.waitForEvent('download'), button.click()]);
  expect(file.suggestedFilename()).toBe('welcome.claude.md');

  const path = await file.path();
  const markdown = await readFile(path, 'utf8');

  // The fixed part, the blocks with their sentence, the draft with its identifiers, and the header at the end.
  expect(markdown).toContain('# Writing a page of this site');
  expect(markdown).toContain('`text`, version 1');
  expect(markdown).toContain(englishCommon.blocks.text.about);
  expect(markdown).toContain('"id": "blk-hello"');

  const header = markdown.slice(markdown.lastIndexOf('## The header to copy'));
  expect(header).toContain('"format": "hub-content"');
  expect(header).toContain('"content": 7');
  expect(header).toContain('"rowVersion": "2026-11-04T10:00:00"');
  expect(header).toMatch(/"rules": "sha256:[0-9a-f]{64}"/);
  expect(header).toMatch(/"body": "sha256:[0-9a-f]{64}"/);
});

test('a template has no download: there is no page to write', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  // Row 5 of the fixture is a template.
  await page.goto('/staff/content/5');

  await expect(page.getByRole('button', { name: englishCommon.content.editor.saveDraft })).toBeVisible();
  await expect(page.getByRole('button', { name: download.label })).toHaveCount(0);
});

test('whoever may not edit the page has no download', async ({ page }) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: staffBootstrap.permissions.filter((permission) => permission.name !== 'Content.Edit'),
  });
  await page.route('**/api/content/7', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
  );

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/7');

  await expect(page.getByRole('heading', { name: englishCommon.content.editor.properties })).toBeVisible();
  await expect(page.getByRole('button', { name: download.label })).toHaveCount(0);
});
