import { expect, test, type Page } from '@playwright/test';

import { anonymousBootstrap, oneTemplate, staffBootstrap, stubTheApi, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * «Open the preview» (design of phase 3, §4.7): the saved draft in a tab of its own, in the frame of the public site, at the
 * width of the window. A unit test says the tab is opened on the press and sent on after the save; what only a browser can
 * say is that a real popup opens, that it waits for the save, that it lands on a page drawn in the site's frame under the real
 * policy of the site (`Cross-Origin-Opener-Policy`, the CSP), and that its links go nowhere.
 */

const editor = englishCommon.content.editor;
const words = englishCommon.content.draftPreview;

interface Written {
  rowVersion: string;
  title: Record<string, string>;
}

/** A page of the coordinator's own department, with a link in it. */
const aPage = {
  ...oneTemplate,
  id: 7,
  kind: 'Page',
  slug: 'welcome',
  path: 'welcome',
  ownerDepartment: 'ED',
  visibility: 'Public',
  isTemplate: false,
  status: 'Draft',
  publishedAt: null,
  publishedVersionId: null,
  title: { en: 'Welcome', it: 'Benvenuti' },
  rowVersion: '2026-11-07T10:00:00',
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
            id: 'blk-hello',
            type: 'text',
            version: 1,
            props: {
              markdown: { en: 'Hello, [the team](/about/team).', it: 'Ciao, [la squadra](/about/team).' },
            },
          },
        ],
        sections: [],
      },
    ],
  },
};

/**
 * The row as the server holds it, for the page and every tab it opens: a save is answered with what it wrote. `hold` keeps a
 * save waiting until the test lets it go.
 */
async function stubTheRow(page: Page, hold?: Promise<void>) {
  const writes: Written[] = [];
  const reads: string[] = [];
  const row = { current: aPage as Record<string, unknown> };

  await page.context().route('**/api/content/7', async (route) => {
    if (route.request().method() === 'PUT') {
      const written = route.request().postDataJSON() as Written;
      writes.push(written);
      await hold;
      row.current = { ...row.current, ...written, rowVersion: `2026-11-07T10:00:0${writes.length}` };
    } else {
      reads.push(route.request().frame().page().url());
    }

    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(row.current) });
  });

  return { writes, reads, row };
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the draft on screen is saved first, then opens in a new tab in the language of the editor', async ({
  page,
}) => {
  await stubTheApiAsStaff(page);

  let release: () => void = () => {};
  const held = new Promise<void>((resolve) => (release = resolve));
  const { writes } = await stubTheRow(page, held);

  await page.goto('/staff/content/7');
  await page.locator('input[name="title.en"]').fill('Written by hand');
  await expect(page.getByText(editor.autosave.unsaved)).toBeVisible();

  // The editor's preview drawn in Italian: the tab opens in it.
  await page
    .getByRole('group', { name: editor.previewLanguage })
    .getByRole('button', { name: 'IT', exact: true })
    .click();

  const opening = page.waitForEvent('popup');
  await page.getByRole('button', { name: editor.openPreview.label }).click();
  const tab = await opening;
  await stubTheApiAsStaff(tab);

  // ⚠️ Open, and empty, for as long as the save is on its way: it is sent to the draft only once the draft is stored.
  await expect.poll(() => writes.length).toBe(1);
  expect(writes[0]?.title.en).toBe('Written by hand');
  expect(tab.url()).toBe('about:blank');

  release();

  await expect(tab).toHaveURL(/\/staff\/content\/7\/preview\?lang=it$/);
  await expect(tab.getByText(words.notice)).toBeVisible();
  await expect(tab.getByText('Ciao,')).toBeVisible();
  // The frame of the site, around it.
  await expect(tab.getByRole('contentinfo')).toBeVisible();
  // What was saved, and not the row as it was opened: the title of the tab is the one just written, read in Italian.
  await expect(tab).toHaveTitle(/^Preview: Benvenuti/);

  await page.screenshot({ path: test.info().outputPath('editor.png') });
  await tab.screenshot({ path: test.info().outputPath('preview.png'), fullPage: true });
});

test('a draft that could not be saved opens nothing', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page
    .context()
    .route('**/api/content/7', (route) =>
      route.request().method() === 'PUT'
        ? route.fulfill({ status: 500, contentType: 'application/problem+json', body: '{"status":500}' })
        : route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(aPage) }),
    );

  await page.goto('/staff/content/7');
  await page.locator('input[name="title.en"]').fill('Not to be kept');

  const opened: Page[] = [];
  page.on('popup', (popup) => opened.push(popup));
  await page.getByRole('button', { name: editor.openPreview.label }).click();

  await expect(page.getByText(editor.openPreview.notSaved)).toBeVisible();
  await expect.poll(() => opened.every((popup) => popup.isClosed())).toBe(true);
});

test('no link of the page or of its frame is followed', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await stubTheRow(page);

  await page.goto('/staff/content/7/preview?lang=en');
  const here = page.url();

  await page.getByRole('link', { name: 'the team' }).click();
  await page.getByRole('link', { name: 'the team' }).click({ button: 'middle' });
  await page.getByRole('contentinfo').getByRole('link', { name: 'News' }).click();
  await page.getByRole('banner').getByRole('link', { name: englishCommon.nav.staff }).click();

  expect(page.url()).toBe(here);
  expect(page.context().pages()).toHaveLength(1);
  await expect(page.getByText(words.notice)).toBeVisible();
});

test('the draft is read again on «refresh», and when the tab comes back into focus', async ({ page }) => {
  await stubTheApiAsStaff(page);
  const { reads, row } = await stubTheRow(page);

  await page.goto('/staff/content/7/preview?lang=en');
  await expect(page.getByText('Hello,')).toBeVisible();

  row.current = {
    ...aPage,
    rowVersion: '2026-11-07T10:05:00',
    body: bodySaying('Saved in the editor meanwhile'),
  };
  await page.getByRole('button', { name: words.refresh }).click();
  await expect(page.getByText('Saved in the editor meanwhile')).toBeVisible();

  row.current = { ...aPage, rowVersion: '2026-11-07T10:06:00', body: bodySaying('Saved while away') };
  const before = reads.length;
  // What a browser does when its tab is chosen again.
  await page.evaluate(() => document.dispatchEvent(new Event('visibilitychange', { bubbles: true })));
  await expect(page.getByText('Saved while away')).toBeVisible();
  expect(reads.length).toBeGreaterThan(before);
});

test('a row this member may not read is refused inside the frame of the site', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await page
    .context()
    .route('**/api/content/8', (route) =>
      route.fulfill({ status: 403, contentType: 'application/problem+json', body: '{"status":403}' }),
    );
  await page
    .context()
    .route('**/api/content/9', (route) =>
      route.fulfill({ status: 404, contentType: 'application/problem+json', body: '{"status":404}' }),
    );

  await page.goto('/staff/content/8/preview');
  await expect(page.getByText(englishCommon.forbidden.title)).toBeVisible();
  await expect(page.getByRole('contentinfo')).toBeVisible();
  await expect(page.getByText(words.notice)).toHaveCount(0);

  await page.goto('/staff/content/9/preview');
  await expect(page.getByText(englishCommon.notFound.title)).toBeVisible();
});

test('a member who is not staff is sent to «not for you»', async ({ page }) => {
  await stubTheApi(page);
  await page.route('**/api/me', (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        ...anonymousBootstrap,
        user: { ...staffBootstrap.user, isStaff: false, isSuperadmin: false, positions: [] },
        permissions: [],
      }),
    }),
  );

  await page.goto('/staff/content/7/preview');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByText(englishCommon.forbidden.title)).toBeVisible();
});

function bodySaying(words: string) {
  return {
    schemaVersion: 1,
    sections: [
      {
        ...aPage.body.sections[0],
        blocks: [{ ...aPage.body.sections[0]!.blocks[0], props: { markdown: { en: words, it: words } } }],
      },
    ],
  };
}
