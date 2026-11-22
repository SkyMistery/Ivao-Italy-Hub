import { expect, test, type Page } from '@playwright/test';

import { stubTheAddressOfAPage, stubTheApiAsStaff, stubThePublishedPage } from './fixtures';
import { englishCommon } from './locales';

/**
 * The address of a page in a browser (G18, note 2026-09-13-contenuti-centralizzati §3.7).
 *
 * Two of the three halves are the router's, and a unit test cannot see a router: a page two levels
 * down is reached at its whole address, and an address a page used to have takes the reader to
 * where it is now — in the address bar, not only on the screen. The third is the editor saying
 * where the page it is composing will be.
 */

const heading = {
  id: 's_main',
  layout: 'stacked',
  blocks: [
    {
      id: 'b_heading',
      type: 'heading',
      version: 1,
      props: { level: 1, text: { en: 'The team', it: 'La squadra' } },
    },
  ],
};

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a page two levels down is read at its whole address', async ({ page }) => {
  await stubThePublishedPage(page, 'about/team', { schemaVersion: 1, sections: [heading] });

  await page.goto('/about/team');

  await expect(page.getByRole('heading', { name: 'The team', level: 1 })).toBeVisible();
});

test('an address a page used to have takes the reader to where it is now', async ({ page }) => {
  await stubThePublishedPage(page, 'about/team', { schemaVersion: 1, sections: [heading] });
  await page.route(
    (url) =>
      url.pathname.endsWith('/api/content/public/page') && url.searchParams.get('path') === 'training/team',
    (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ page: null, movedTo: '/about/team' }),
      }),
  );

  await page.goto('/training/team');

  await expect(page).toHaveURL(/\/about\/team$/);
  await expect(page.getByRole('heading', { name: 'The team', level: 1 })).toBeVisible();
});

test('the editor of a page says where it will be, and a coordinator is asked to choose where', async ({
  page,
}) => {
  // The ordinary staff fixture is an events coordinator, who may not put a page at the top of the
  // site: the field has no "at the top" and asks for a page to go under.
  await stubTheApiAsStaff(page);
  await stubTheAddressOfAPage(page);

  await page.goto('/staff/content/new?kind=Page&department=ED');

  // The page first: the form reports its values once they are whole, and without a page to go under
  // they are not — so the address has nothing to say until one is chosen.
  await page.getByLabel(englishCommon.content.fields.parentId, { exact: true }).click();
  await expect(page.getByRole('option', { name: englishCommon.content.options.parentId.none })).toHaveCount(
    0,
  );
  await page.getByRole('option', { name: '/about/team — Team' }).click();

  // The address is born from the title (design 13 §4.1): a coordinator, who may not approve, is not shown the slug.
  const asked = slugsAskedFor(page);
  await expect(page.getByLabel(englishCommon.content.fields.slug, { exact: true })).toHaveCount(0);
  await writeTheTitle(page, 'Section page');

  const where = page.getByRole('status', { name: englishCommon.content.address.label });
  await expect(where).toContainText('/about/section-page');
  await expect(where).toContainText(englishCommon.content.address.state.Free);
  await expect.poll(() => asked.at(-1)).toBe('section-page');
});

test('a coordinator whose title makes a taken address is offered the free one, and takes it with a press', async ({
  page,
}) => {
  await stubTheApiAsStaff(page);
  await stubTheAddressOfAPage(page);

  // After the fixture's own answer, so that it wins (Playwright matches the last route first): the address of the title
  // is taken, and the one the server proposes is free.
  await page.route(
    (url) => url.pathname.endsWith('/api/content/address'),
    (route) => {
      const slug = new URL(route.request().url()).searchParams.get('slug');
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(
          slug === 'section-page'
            ? { path: '/about/section-page', state: 'Taken', suggestion: 'section-page-2' }
            : { path: `/about/${slug ?? ''}`, state: 'Free', suggestion: null },
        ),
      });
    },
  );

  await page.goto('/staff/content/new?kind=Page&department=ED');
  await page.getByLabel(englishCommon.content.fields.parentId, { exact: true }).click();
  await page.getByRole('option', { name: '/about/team — Team' }).click();
  await writeTheTitle(page, 'Section page');

  const where = page.getByRole('status', { name: englishCommon.content.address.label });
  await expect(where).toContainText(englishCommon.content.address.takenNew);

  await page
    .getByRole('button', {
      name: englishCommon.content.address.useSuggestion.replace('{{suggestion}}', 'section-page-2'),
    })
    .click();

  await expect(where).toContainText('/about/section-page-2');
  await expect(where).toContainText(englishCommon.content.address.state.Free);
});

/** The English title of the form, written in its own tab. */
async function writeTheTitle(page: Page, title: string): Promise<void> {
  const field = page
    .locator('#content-metadata fieldset')
    .filter({ hasText: englishCommon.content.fields.title });
  const english = field.getByRole('tab', { name: 'English' });
  if ((await english.count()) > 0) {
    await english.click();
  }
  await page.locator('[id="title.en"]').fill(title);
}

/** The slugs the form asks the server about, in order: what the address preview is composed from. */
function slugsAskedFor(page: Page): string[] {
  const asked: string[] = [];
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.pathname.endsWith('/api/content/address')) {
      asked.push(url.searchParams.get('slug') ?? '');
    }
  });
  return asked;
}
