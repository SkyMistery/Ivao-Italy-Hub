import { expect, type BrowserContext, type Locator, type Page } from '@playwright/test';

/**
 * The moves the round is made of, written once. Everything here is about *driving* the application
 * — signing in, choosing in a select, writing a translated field — and nothing here asserts what
 * the product should do: that belongs in the spec, where it can be read.
 */

export const benchUrl = process.env.E2E_URL ?? 'http://127.0.0.1:5080';

/** The department the bench works in. See `scripts/e2e-server.mjs` for why it is the web one. */
export const department = 'wd';

/**
 * Becomes the bench's member of staff. The cookie this writes is the one a real IVAO login writes,
 * so everything after it — the security stamp, the permission claims, the department policy — is
 * the real thing (design M1 §11.1).
 */
export async function signIn(context: BrowserContext): Promise<void> {
  const response = await context.request.post('/e2e/signin');
  expect(response.status(), await response.text()).toBe(200);
}

/**
 * This division publishes in Italian and English and defaults to Italian, so the suite says which
 * one it is reading in rather than asserting against whichever the server prefers today.
 */
export async function readInEnglish(context: BrowserContext): Promise<void> {
  await context.addCookies([{ name: 'hub.lang', value: 'en', url: benchUrl }]);
}

/**
 * The block a label belongs to. `SchemaForm` draws a label and its control as siblings inside one
 * element, which is what makes "the select next to *this* label" expressible at all: several
 * selects on one screen have the same options, and several translated fields have the same tabs.
 */
export function fieldOf(scope: Page | Locator, label: string): Locator {
  return scope.getByText(label, { exact: true }).locator('..');
}

/**
 * Picks a value in one of Atmosphere's selects, which is a button and a list and not a `<select>`.
 * The page is passed separately from the scope because the list of options is rendered in a portal
 * at the end of the document: it is never inside the field it belongs to.
 */
export async function choose(
  page: Page,
  label: string,
  option: string,
  scope: Page | Locator = page,
): Promise<void> {
  await fieldOf(scope, label).getByRole('combobox').click();
  await page.getByRole('option', { name: option, exact: true }).click();
}

/**
 * Writes a translated field in every language of the division. One tab per language, and only the
 * open one exists in the DOM — so this is a click and a fill per language, which is also exactly
 * what a coordinator does.
 */
export async function writeInBothLanguages(
  form: Locator,
  label: string,
  path: string,
  values: { en: string; it: string },
): Promise<void> {
  const field = form.locator('fieldset').filter({ hasText: label });

  for (const [locale, language, text] of [
    ['it', 'Italian', values.it],
    ['en', 'English', values.en],
  ] as const) {
    await field.getByRole('tab', { name: language }).click();
    await form.locator(`[id="${path}.${locale}"]`).fill(text);
  }
}

/**
 * Adds a block of that kind to the first section that accepts one.
 *
 * Scoped to the palette on purpose: the same words label the blocks already in the page over in
 * the outline, so "click the button that says Heading" is three buttons and not one.
 */
export async function addBlock(page: Page, paletteLabel: string, block: string): Promise<void> {
  const palette = page.getByText(paletteLabel, { exact: true }).first().locator('..');
  await palette.getByRole('button', { name: block, exact: true }).click();
}

/**
 * Runs an action and waits for the call it sets off to come back, asserting the status.
 *
 * Without this the suite races the server: clicking "publish" and then opening the public page in
 * another context passed on a warm machine and failed on a cold one, which is the shape of a test
 * that reports the speed of the runner rather than the behaviour of the product. Waiting on the
 * response also turns a refusal — a publish rejected for a missing language, say — into a failure
 * that says so, instead of a mysterious empty page three steps later.
 */
export async function whileWaitingFor(
  page: Page,
  method: string,
  urlPart: string,
  action: () => Promise<void>,
): Promise<void> {
  const response = page.waitForResponse(
    (candidate) => candidate.request().method() === method && candidate.url().includes(urlPart),
  );

  await action();
  expect((await response).status(), `${method} ${urlPart}`).toBeLessThan(300);
}

/**
 * Selects a block in the outline, so that its properties are the ones on the right.
 *
 * Scoped to the list item, because the palette that adds blocks uses the very same words: "click
 * the button that says Callout" is two buttons, and only one of them selects anything.
 */
export async function selectBlock(page: Page, label: string): Promise<void> {
  await page
    .locator('li')
    .filter({ hasText: label })
    .getByRole('button', { name: label, exact: true })
    .click();
}

/** The metadata of the page, which is the first form on the editor screen. */
export function metadata(page: Page): Locator {
  return page.locator('form').first();
}

/** The properties of whatever is selected, which is the last one. */
export function properties(page: Page): Locator {
  return page.locator('form').last();
}
