import { expect, test } from '@playwright/test';

import { siteStaffBootstrap, stubTheApiAsStaff } from './fixtures';
import { englishCommon } from './locales';

/**
 * The back office is reachable: a list, and the form behind its buttons.
 *
 * This exists because of the second failure of the same family as the first. The detail routes were
 * children of the list routes, and no list rendered an `Outlet`, so clicking "new link" changed the
 * address and left the list on the screen — every form in the hub was unreachable in a browser
 * while 76 unit tests and 353 server tests stayed green. Nothing exercised how the routes compose,
 * exactly as nothing had exercised how the providers compose.
 *
 * So the assertions here are deliberately about **arriving somewhere**, not about what a screen
 * looks like: the address changed *and* the thing it promised is on the page. Either half alone is
 * what let this through.
 */

test.beforeEach(async ({ page }) => {
  await stubTheApiAsStaff(page);

  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('the links list opens for a coordinator of that department', async ({ page }) => {
  await page.goto('/staff/ed/links');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();

  // The row the API answered with: proof the list rendered its data and not just its frame.
  // By cell, because the URL of the same row also contains the word.
  await expect(page.getByRole('cell', { name: 'Discord', exact: true })).toBeVisible();
});

test('new link reaches the form, and not just the address bar', async ({ page }) => {
  await page.goto('/staff/ed/links');
  await page.getByRole('link', { name: englishCommon.links.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/links\/new/);

  // The half that was missing. The address changed all along; what never happened was the form
  // appearing, because the list route had no outlet to draw its child into.
  await expect(page.getByLabel(englishCommon.links.fields.url)).toBeVisible();

  // And the list is gone rather than sitting above the form. Asserted on the row, not on the word
  // "Links": that still appears in the breadcrumb of the form, which is correct.
  await expect(page.getByRole('cell', { name: 'Discord', exact: true })).toHaveCount(0);
});

test('edit reaches the form of that row', async ({ page }) => {
  await page.goto('/staff/ed/links');
  await page.getByRole('link', { name: englishCommon.common.edit }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/links\/7/);
  await expect(page.getByLabel(englishCommon.links.fields.url)).toBeVisible();
});

test('a department the member does not reach is a refusal, not an empty table', async ({ page }) => {
  await page.goto('/staff/fod/links');

  await expect(page).toHaveURL(/\/forbidden/);
});

test('the content sits beside the sidebar, not underneath it in a narrow column', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/ed/links');
  await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();

  const main = await page.locator('main').first().boundingBox();
  expect(main).not.toBeNull();

  // Geometry, because this is a fault no assertion about text can see. `Sidebar` brings its own
  // `SidebarProvider` and its own `SidebarContainer`, and `SidebarContainer` is not a two column
  // shell -- it *is* the `<aside>`, 288px wide. Wrapping our own around it put the sidebar and the
  // main region inside that aside, so every back office screen was drawn in a 255px column with
  // the rest of the window empty, and the collapse button appeared twice. Everything still said
  // the right words, in the right order, in the wrong place.
  expect(main!.x).toBeGreaterThan(200);
  expect(main!.width).toBeGreaterThan(600);

  // And exactly one way to collapse it, not two.
  await expect(page.getByText(/close sidebar/i)).toHaveCount(1);
});

test('a translated field is as wide as a plain one', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/ed/links/new');
  await expect(page.getByLabel(englishCommon.links.fields.url)).toBeVisible();

  // Geometry again, and again because nothing else can see it. Atmosphere's `Tabs` pins itself to
  // `w-[400px]`, and `LocaleFields` is built on it -- so the title of a link was drawn 400px wide
  // next to an address input the full width of the form. Every assertion about text passed.
  const localized = await page.locator('fieldset input').first().boundingBox();
  const plain = await page.locator('input[name="url"]').first().boundingBox();

  expect(localized).not.toBeNull();
  expect(plain).not.toBeNull();
  expect(localized!.width).toBeGreaterThan(plain!.width * 0.9);
});

test('the media library opens and offers the one control the form generator has no notion of', async ({
  page,
}) => {
  await page.goto('/staff/ed/media');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: englishCommon.media.title })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'banner.png', exact: true })).toBeVisible();

  // Uploading is not a field of any schema, so it is the one place in the back office with a
  // control written by hand. The button has to be visible and the input behind it must not be.
  await expect(page.getByRole('button', { name: englishCommon.media.upload })).toBeVisible();
  await expect(page.locator('input[type="file"]')).toBeHidden();
});

test('the preview of a file is a picture with a real size, inside the column it belongs to', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/ed/media/9');

  await expect(page.getByLabel(englishCommon.media.fields.category)).toBeVisible();

  // Two things no assertion about text can see, and the media library is nothing but these two.
  //
  // First: the bytes arrived. A file that did not — a wrong address, the route swallowed by the
  // SPA fallback, a visibility filter saying no — still leaves an <img> in the page carrying its
  // alternative text, which reads exactly right to every other kind of assertion.
  const picture = page.getByRole('img', { name: 'A runway at dawn' });
  await expect(picture).toBeVisible();
  expect(await picture.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);

  // Second: the box it is given is a real one, capped, and inside the column it belongs to. The
  // library holds anything from an icon to a photograph, so the preview must not be the file's own
  // size or this screen is a different shape for every row.
  const preview = await picture.boundingBox();
  const main = await page.locator('main').first().boundingBox();

  expect(preview).not.toBeNull();
  expect(main).not.toBeNull();

  expect(preview!.height).toBeGreaterThan(20);
  expect(preview!.height).toBeLessThanOrEqual(200);
  expect(preview!.x + preview!.width).toBeLessThanOrEqual(main!.x + main!.width);
});

test('the calendar vocabulary is a screen of the administration, with no department in it', async ({
  page,
}) => {
  // The second resource of the hub with no department at all — the permissions were the first —
  // and the first one a coordinator may read but not write. What a browser adds to the unit tests
  // is that the screen exists at the address the sidebar sends people to, and draws its rows.
  await page.goto('/staff/admin/calendar-kinds');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: englishCommon.calendarKinds.title })).toBeVisible();
  await expect(page.getByRole('cell', { name: 'Meeting', exact: true })).toBeVisible();

  // And it is offered where every back office screen is offered, rather than only by typing the
  // address. The palette reads `staffDestinations`, which the sidebar draws from too — asserting on
  // the sidebar itself would be asserting that Atmosphere's group happens to be open.
  await page.goto('/staff/ed/links');

  // ⚠️ Waited for: a key pressed before React has attached its listener is a key nobody hears, and
  // the wait that follows looks exactly like a broken shortcut (`search.spec.ts` says the same).
  await expect(page.getByRole('heading', { name: englishCommon.links.title })).toBeVisible();
  await page.keyboard.press('Control+k');

  const palette = page.getByRole('dialog');
  await expect(palette).toBeVisible();
  await expect(
    palette.getByText(`${englishCommon.admin.title} — ${englishCommon.calendarKinds.title}`),
  ).toBeVisible();
});

test('the order and the audience of a menu entry are changed from the table', async ({ page }) => {
  // Asked for by Carmine while running part 1 of the demo. What a browser adds to the unit tests is
  // the whole of it: the cell has to be a control, the save has to leave, and the value has to stay
  // — three things that are true in jsdom and mean nothing until a browser lays the table out.
  //
  // ⚠️ Signed in as staff of the department that **owns the site**: the menu is not a screen every
  // department has, and the ordinary fixture — a coordinator of events — is rightly answered "this
  // is not for you" there.
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await page.goto('/staff/wd/menu');

  const order = page.getByRole('spinbutton', { name: englishCommon.menu.fields.sort });
  await expect(order).toHaveValue('20');

  const written = page.waitForRequest(
    (request) => request.url().includes('/api/menu/3') && request.method() === 'PUT',
  );

  await order.fill('5');
  // The save is on leaving the field and not on every keystroke: twenty rows would be twenty
  // requests otherwise.
  await order.blur();

  const request = await written;
  expect(JSON.parse(request.postData() ?? '{}')).toMatchObject({ sort: 5, path: '/pilots' });

  // ⚠️ The whole row went back, not just the field: the list is handed a projection and the engine
  // writes with the full payload, so the cell reads the row before it writes it
  // (`decisions/2026-09-08-modificare-da-una-lista.md`).
  await expect(order).toHaveValue('5');

  // And the audience is a select in its cell, with the four the content screens use.
  const audience = page.getByRole('combobox', { name: englishCommon.menu.fields.visibility });
  await expect(audience).toBeVisible();
});

test('the address of a menu entry offers the addresses that exist, and stays open to be read', async ({
  page,
}) => {
  // ⚠️ The half jsdom cannot see, and it is the half that was broken: the list opened on focus and
  // closed on the very same click, because Radix dismisses a popover on a pointer event outside its
  // content — and the box is outside its content. A unit test passed throughout.
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await page.goto('/staff/wd/menu/3');

  const address = page.getByLabel(englishCommon.menu.fields.path, { exact: true });
  await address.click();

  // Grouped, and the screens of the application are a group of their own: they are routes and not
  // rows, so no department wrote them.
  await expect(page.getByText(englishCommon.menu.screensGroup)).toBeVisible();
  await expect(page.getByText('/calendar')).toBeVisible();

  // Choosing one writes the address, not the title.
  await page.getByText('/calendar').click();
  await expect(address).toHaveValue('/calendar');

  // The links of the library are a group too, and one of another department: whoever edits the menu
  // owns the site and reaches every department, so the list is the whole closed set and not a
  // department's corner of it.
  await address.click();
  await expect(page.getByText(englishCommon.menu.linksGroup)).toBeVisible();
  await expect(page.getByText('https://example.org/discord')).toBeVisible();

  // ⚠️ And it is closed. An address nobody wrote down is a way of searching this list, never a
  // value: it is gone the moment the field is left. The server refuses the same thing, on the
  // field, so this is the near half of one rule and not a rule of its own.
  await address.fill('https://somewhere.invented.example');
  await expect(page.getByText(englishCommon.form.suggest.emptyClosed)).toBeVisible();
  await page.getByLabel(englishCommon.menu.fields.sort, { exact: true }).click();
  await expect(address).toHaveValue('/calendar');
});

test('the gallery draws every kind of field the generator learned, and they are usable sizes', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/staff/admin/ui-kit');

  await expect(page.getByText('Something went wrong!')).toHaveCount(0);

  // A day and an instant are native inputs, so a browser brings the calendar and this hub does not
  // have to. What the hub owes is the second line, and it is the half a fixture in UTC could never
  // have shown: the sample holds noon UTC, and the division sits in Rome.
  await expect(page.locator('input[type="date"]')).toHaveCount(1);
  const instant = page.locator('input[type="datetime-local"]');
  await expect(instant).toHaveValue('2026-06-01T12:00');
  // Exact, and for a reason worth remembering: "2:00" is a substring of "12:00", so a loose match
  // here passed happily while the echo was showing UTC twice. Noon UTC on the first of June is two
  // in the afternoon in Rome.
  await expect(page.getByText(/Europe\/Rome/).first()).toHaveText('6/1/26, 2:00 PM Europe/Rome');

  // The icons are a closed set drawn as pictures, and the reason they are a grid and not a select
  // is that a name without its picture is unusable — so the picture has to have a size. jsdom
  // cannot see this at all: it does no layout.
  const icon = page.getByRole('radio', { name: 'plane', exact: true });
  await expect(icon).toBeVisible();

  const box = await icon.boundingBox();
  expect(box).not.toBeNull();
  expect(box!.width).toBeGreaterThanOrEqual(32);
  expect(box!.height).toBeGreaterThanOrEqual(32);

  // A translated object is tabs with real fields inside, not a JSON box.
  await expect(page.getByRole('tab', { name: /English/ })).not.toHaveCount(0);

  // And a list can be reordered, with the ends saying they have nowhere to go.
  await expect(page.getByRole('button', { name: 'Move down' })).toHaveCount(2);
  await expect(page.getByRole('button', { name: 'Move up' }).first()).toBeDisabled();
});

/**
 * The three screens G5 added, opened in a browser.
 *
 * They are the same list and the same form as the pages, mounted with a different `kind` — which is
 * exactly the kind of claim that is true in a unit test and false in a browser. §11 and §12 of
 * HANDOFF are both stories about composition being green everywhere except where it runs, and three
 * screens nobody had opened would have been the same bet again.
 */
test('the news, the documents and the vocabulary each open on their own address', async ({ page }) => {
  for (const [path, heading] of [
    ['/staff/ed/news', englishCommon.news.title],
    ['/staff/ed/documents', englishCommon.documents.title],
    ['/staff/ed/categories', englishCommon.categories.title],
  ] as const) {
    await page.goto(path);

    await expect(page.getByText('Something went wrong!')).toHaveCount(0);
    await expect(page.getByRole('heading', { name: heading, level: 1 })).toBeVisible();
  }
});

test('new news reaches the editor, and it is the editor of a news item', async ({ page }) => {
  await page.goto('/staff/ed/news');
  await page.getByRole('link', { name: englishCommon.news.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/news\/new/);

  // The metadata form is there — the half that was missing the day no form in the hub was
  // reachable — and it carries the field only a news item has, which is what says the `kind`
  // reached the schema rather than the screen simply being the page editor under another address.
  await expect(page.getByLabel(englishCommon.content.fields.slug)).toBeVisible();
  await expect(page.getByLabel(englishCommon.content.fields.pinned)).toBeVisible();
});

test('the documents list says which rows have a file, and which do not', async ({ page }) => {
  await page.goto('/staff/ed/documents');

  const withFile = page.getByRole('row', { name: /Joining procedure/ });
  const withoutFile = page.getByRole('row', { name: /Read in the browser/ });

  // A link to the file itself, in the row of the document that has one. Not a thumbnail: a
  // document's file is as often a PDF as a picture, and the row does not carry its type.
  await expect(withFile.getByRole('link', { name: englishCommon.list.file })).toHaveAttribute(
    'href',
    '/media/9/file',
  );

  // And nothing at all in the row of the one that is read in the browser. Both halves matter: a
  // column that drew something for every row would look right on a list where every row has a file.
  await expect(withoutFile.getByRole('link', { name: englishCommon.list.file })).toHaveCount(0);
});

test('the calendar shows a projected entry and does not offer to edit it', async ({ page }) => {
  await page.goto('/staff/ed/calendar');

  await expect(page.getByRole('heading', { name: englishCommon.calendar.title, level: 1 })).toBeVisible();

  const written = page.getByRole('row', { name: /Staff meeting/ });
  const projected = page.getByRole('row', { name: /Night flight/ });

  // Both are in the list: seeing what a module put in the calendar is the point of there being one.
  await expect(written).toBeVisible();
  await expect(projected).toBeVisible();

  // The one the staff wrote is theirs to change; the mirror says where it comes from instead of
  // offering a button that answers 403 (design M1 §4).
  await expect(written.getByRole('link', { name: englishCommon.common.edit })).toBeVisible();
  await expect(projected.getByRole('link', { name: englishCommon.common.edit })).toHaveCount(0);
  await expect(projected.getByText(englishCommon.calendar.projected).first()).toBeVisible();
});

test('new entry reaches the calendar form', async ({ page }) => {
  await page.goto('/staff/ed/calendar');
  await page.getByRole('link', { name: englishCommon.calendar.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/calendar\/new/);
  await expect(page.getByLabel(englishCommon.calendar.fields.kind, { exact: true })).toBeVisible();
});

test('new category reaches its form', async ({ page }) => {
  await page.goto('/staff/ed/categories');
  await page.getByRole('link', { name: englishCommon.categories.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/ed\/categories\/new/);
  await expect(page.getByLabel(englishCommon.categories.fields.key)).toBeVisible();
});

test('the templates of a department have a screen, a button, and a count behind them', async ({ page }) => {
  // ⚠️ The gap this closes, and it was there since G11a: a coordinator was told templates were
  // theirs to manage and there was nowhere to manage them. They were kept out of the content list
  // on purpose, offered by the picker only to make a page from, and the one way to open one was to
  // type its address.
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await page.goto('/staff/wd/templates');

  await expect(page.getByRole('heading', { name: englishCommon.templates.title })).toBeVisible();

  // The one column no other content list has: which kind this template makes. Scoped to the table,
  // because "Documents" is also a screen in the sidebar and "document" is a word in the description.
  const rows = page.getByRole('table');
  await expect(rows.getByText('Section page')).toBeVisible();
  await expect(rows.getByText(englishCommon.content.options.kind.Document, { exact: true })).toBeVisible();

  // Opening one says how many rows were made from it — the sentence that stops a careless edit.
  await page.getByRole('link', { name: englishCommon.common.edit }).first().click();
  await expect(page).toHaveURL(/\/staff\/wd\/templates\/5/);
  await expect(page.getByText('4 rows were made from this template.')).toBeVisible();
});

test('a new template is made from a button, and carries the kind that was chosen', async ({ page }) => {
  await stubTheApiAsStaff(page, siteStaffBootstrap);
  await page.goto('/staff/wd/templates');

  // The kind is chosen before the editor opens, because it decides which fields the form draws and
  // a form redrawing itself under the hands of whoever is filling it in would be worse.
  await page.getByRole('link', { name: englishCommon.templates.create }).first().click();

  await expect(page).toHaveURL(/\/staff\/wd\/templates\/new\?kind=Page/);
  await expect(page.getByLabel(englishCommon.content.fields.slug, { exact: true })).toBeVisible();
});

test('the templates screen is not for a coordinator who may not change one', async ({ page }) => {
  // Every staff member *reads* templates — that is what makes "new from a template" work across
  // departments — and only `Content.ManageTemplates` opens the screen that changes them. The
  // ordinary fixture is an events coordinator, who holds neither that nor the department.
  await stubTheApiAsStaff(page);
  await page.goto('/staff/ed/templates');

  await expect(page).toHaveURL(/\/forbidden/);
});
