import { readFile } from 'node:fs/promises';

import { expect, test, type Page } from '@playwright/test';

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

/**
 * What `GET /api/content/addresses` answers: a page of another department the coordinator may read, and a news item
 * (decisions/2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere.md).
 */
async function stubTheSiteAddresses(page: Page) {
  await page.route('**/api/content/addresses', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        {
          id: 20,
          kind: 'Page',
          url: '/about/team',
          title: { en: 'Team', it: 'Squadra' },
          ownerDepartment: 'WD',
          visibility: 'Public',
        },
        {
          id: 21,
          kind: 'News',
          url: '/news/launch',
          title: { en: 'Launch', it: 'Lancio' },
          ownerDepartment: 'WD',
          visibility: 'Staff',
        },
      ]),
    }),
  );
}

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  // After the staff's own routes, so that it wins for this row (Playwright matches the last one first).
  await page.route('**/api/content/7', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
  );

  await stubTheSiteAddresses(page);

  // No page of the style chosen, unless a test says otherwise (decisions/2026-11-04-la-pagina-dello-stile.md).
  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ stylePageId: null, stylePagePath: null }),
    }),
  );

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

/** The page of the division's style, as the public read of the site answers it. */
const theStylePage = {
  page: {
    ...aPage,
    id: 9,
    slug: 'style',
    path: 'about/style',
    visibility: 'Staff',
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 'sec-style',
          layout: 'stacked',
          background: 'none',
          padding: 'md',
          width: 'default',
          blocks: [
            {
              id: 'blk-tone',
              type: 'heading',
              version: 1,
              props: { level: 2, text: { en: 'Tone', it: 'Tono' } },
            },
            {
              id: 'blk-rule',
              type: 'text',
              version: 1,
              props: { markdown: { en: 'Write *short* sentences.', it: 'Frasi *brevi*.' } },
            },
          ],
          sections: [],
        },
      ],
    },
  },
  movedTo: null,
};

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

  // The site's addresses, of another department too, news included.
  expect(markdown).toContain('| `/about/team` | page | Team | Public |');
  expect(markdown).toContain('| `/news/launch` | news | Launch | Staff |');

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

test('without a page of the style the instructions have no part for it', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/7');

  const [file] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: download.label }).click(),
  ]);

  expect(await readFile(await file.path(), 'utf8')).not.toContain('## The style of the division');
});

test('the page of the style chosen in the settings goes at the end, read in the language on screen', async ({
  page,
}) => {
  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ stylePageId: 9, stylePagePath: '/about/style' }),
    }),
  );
  await page.route('**/api/content/public/page?**', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(theStylePage) }),
  );

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/7');

  const [file] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: download.label }).click(),
  ]);
  const markdown = await readFile(await file.path(), 'utf8');

  const style = markdown.slice(
    markdown.indexOf('## The style of the division'),
    markdown.indexOf('## The header to copy'),
  );
  expect(style).toContain('### Tone');
  expect(style).toContain('Write *short* sentences.');
  expect(style).not.toContain('Frasi');
});

test('whoever edits the page by a grant, without reading the links, still downloads', async ({ page }) => {
  // `Content.Edit` given by a grant does not bring `Links.View` with it (review of #449, finding 1): the links read is
  // not asked, and if it were it would be refused.
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: staffBootstrap.permissions.filter((permission) => !permission.name.startsWith('Links.')),
  });
  await page.route('**/api/content/7', (route) =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
  );
  await stubTheSiteAddresses(page);
  await page.route('**/api/content/settings', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ stylePageId: null, stylePagePath: null }),
    }),
  );
  const askedForLinks: string[] = [];
  await page.route('**/api/links**', (route) => {
    askedForLinks.push(route.request().url());
    return route.fulfill({ status: 403, contentType: 'application/json', body: '{}' });
  });

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto('/staff/content/7');

  const [file] = await Promise.all([
    page.waitForEvent('download'),
    page.getByRole('button', { name: download.label }).click(),
  ]);
  const markdown = await readFile(await file.path(), 'utf8');

  expect(markdown).toContain('"content": 7');
  expect(markdown).not.toContain('The links of the library');
  expect(askedForLinks).toEqual([]);
});
