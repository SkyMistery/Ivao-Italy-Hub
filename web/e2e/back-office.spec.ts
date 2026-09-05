import { expect, test } from '@playwright/test';

import { stubTheApiAsStaff } from './fixtures';
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
