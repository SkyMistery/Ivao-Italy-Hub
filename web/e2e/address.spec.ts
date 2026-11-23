import { expect, test, type Page } from '@playwright/test';

import { staffBootstrap, stubTheAddressOfAPage, stubTheApiAsStaff, stubThePublishedPage } from './fixtures';
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

test('a title that gives no slug opens the field of the slug to a coordinator, who writes the address', async ({
  page,
}) => {
  // The maintainer's answer on #554, point 1: a title in another script proposes nothing, and a field nobody can see
  // would be a refusal nobody can answer.
  await stubTheApiAsStaff(page);
  await stubTheAddressOfAPage(page);
  const asked = slugsAskedFor(page);

  await page.goto('/staff/content/new?kind=Page&department=ED');
  await page.getByLabel(englishCommon.content.fields.parentId, { exact: true }).click();
  await page.getByRole('option', { name: '/about/team — Team' }).click();

  const slug = page.getByLabel(englishCommon.content.fields.slug, { exact: true });
  await expect(slug).toHaveCount(0);

  await writeTheTitle(page, 'Правила сайта');
  await expect(slug).toBeVisible();

  await slug.fill('site-rules');
  await expect.poll(() => asked.at(-1)).toBe('site-rules');

  // Open it stays: a Latin title written after does not hide the address typed in it.
  await writeTheTitle(page, 'Site rules');
  await expect(slug).toHaveValue('site-rules');
});

test('new from a template: a coordinator writes the title, and the slug is born from it', async ({
  page,
}) => {
  // The maintainer's answer on #554, point 3: «new from a template» follows design 13 §4.1 like the form.
  await stubTheApiAsStaff(page);
  await stubTheAddressOfAPage(page);
  const sent = bodiesSentFromATemplate(page);

  await page.goto('/staff/content?kind=Page&department=ED');
  await chooseInThePicker(page, '#templateId', 'Section page');
  await chooseInThePicker(page, '#newParent', '/about/team — Team');

  await expect(page.locator('#newSlug')).toHaveCount(0);
  await page.locator('#newTitle').fill('Our team, in 2027!');
  await page.getByRole('button', { name: englishCommon.content.create, exact: true }).click();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({
    slug: 'our-team-in-2027',
    title: { en: 'Our team, in 2027!' },
    parentId: 2,
  });
});

test('new from a template: a title that gives no slug opens the field there too', async ({ page }) => {
  await stubTheApiAsStaff(page);
  await stubTheAddressOfAPage(page);
  const sent = bodiesSentFromATemplate(page);

  await page.goto('/staff/content?kind=Page&department=ED');
  await chooseInThePicker(page, '#templateId', 'Section page');
  await chooseInThePicker(page, '#newParent', '/about/team — Team');

  await page.locator('#newTitle').fill('Правила');
  await expect(page.locator('#newSlug')).toBeVisible();
  await expect(page.getByRole('button', { name: englishCommon.content.create, exact: true })).toBeDisabled();

  await page.locator('#newSlug').fill('rules');
  await page.getByRole('button', { name: englishCommon.content.create, exact: true }).click();

  await expect.poll(() => sent.length).toBe(1);
  expect(sent[0]).toMatchObject({ slug: 'rules', title: { en: 'Правила' } });
});

test('new from a template: whoever approves keeps the field, proposed from the title', async ({ page }) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [...staffBootstrap.permissions, { name: 'Content.Approve', department: 'ED' }],
  });
  await stubTheAddressOfAPage(page);

  await page.goto('/staff/content?kind=Page&department=ED');
  await chooseInThePicker(page, '#templateId', 'Section page');
  await page.locator('#newTitle').fill('Our team');

  await expect(page.locator('#newSlug')).toHaveValue('our-team');
  await page.locator('#newSlug').fill('team');
  await page.locator('#newTitle').fill('Our whole team');
  await expect(page.locator('#newSlug')).toHaveValue('team');
});

/** A select of the template picker, opened by its control and answered by the option's name. */
async function chooseInThePicker(page: Page, control: string, option: string): Promise<void> {
  await page.locator(control).click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

/**
 * What «new from a template» sends, refused by the bench so that nothing navigates: the body is what this test is about,
 * and the refusal is the picker's own to say.
 */
function bodiesSentFromATemplate(page: Page): unknown[] {
  const sent: unknown[] = [];
  void page.route('**/api/content/from-template/*', (route) => {
    sent.push(route.request().postDataJSON());
    return route.fulfill({
      status: 400,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 400, title: 'Refused', errors: {} }),
    });
  });
  return sent;
}

/**
 * The English title of the form. English is the fixture division's main language, so its tab is the one open: no click,
 * which would have to tell the title's tabs from those of the sharing title below it.
 */
async function writeTheTitle(page: Page, title: string): Promise<void> {
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
