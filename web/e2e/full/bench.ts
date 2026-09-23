import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import {
  expect,
  type APIRequestContext,
  type APIResponse,
  type BrowserContext,
  type Locator,
  type Page,
} from '@playwright/test';

import { englishCommon } from '../locales';

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
 * Adds a block of that kind to whatever is selected, from the bar of components on the left.
 *
 * ⚠️ Scoped to the drawer the block lives in, and it has to be: the same word labels the block
 * already in the page over in the outline, so "click the button that says Heading" is two buttons
 * and not one. The drawer is named by the subgroup its blocks declare in code.
 */
export async function addBlock(page: Page, drawer: string, block: string): Promise<void> {
  await page.getByLabel(drawer).getByRole('button', { name: block, exact: true }).click();
}

/**
 * Swaps the middle column for the outline, which is where a section is chosen without a mouse.
 *
 * The editor opens on the page itself since 10 September 2026, so a suite that wants the outline
 * asks for it.
 */
export async function openOutline(page: Page, label: string): Promise<void> {
  await page.getByRole('button', { name: label, exact: true }).click();
}

/**
 * Picks a section in the outline, which is what tells the palette where a component would land.
 */
export async function selectSection(page: Page, name: string): Promise<void> {
  await page.getByRole('button', { name, exact: true }).first().click();
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

/**
 * The metadata of the page, which is the first form on the editor screen.
 *
 * ⚠️ Since 9 September 2026 it lives in the **panel on the right**, as the properties of the page —
 * a section and a block are edited in the same place — and its `Save draft` is in the toolbar at the
 * top, outside the form, submitting it by `form=`. So `saveDraft(page)` and not
 * `metadata(page).getByRole('button', …)`, which used to be the same thing and is not any more.
 */
export function metadata(page: Page): Locator {
  return page.locator('form').first();
}

/** The button that saves the row, wherever it is drawn. */
export function saveDraft(page: Page, label: string): Locator {
  return page.getByRole('button', { name: label, exact: true });
}

/** The properties of whatever is selected, which is the last one. */
export function properties(page: Page): Locator {
  return page.locator('form').last();
}

/**
 * A row of content made, read or changed through the API rather than through the screens.
 *
 * Setting a scene through the screens is the right thing when the screens are what is being tested;
 * it is the wrong thing when they are only the way in. The one spec that uses these is about what
 * the editor *says* once a template has moved on, and driving four forms to get there would be four
 * more ways for it to fail for a reason that is not the one it asks about.
 */
export interface ContentRow {
  readonly id: number;
  readonly rowVersion: string;
  readonly slug: string;
  readonly kind: string;
  readonly ownerDepartment: string;
  readonly visibility: string;
  readonly isTemplate: boolean;
  readonly title: Record<string, string>;
  readonly summary: Record<string, string> | null;
  readonly seo: unknown;
  readonly body: { schemaVersion: number; sections: unknown[] };
  readonly schemaVersion: number;
  readonly collections: readonly string[];
  readonly coverMediaId: number | null;
  readonly pinned: boolean;
  readonly sort: number;
  readonly fileMediaId: number | null;
}

/** What a write looks like: the row as it came back, minus what only the server decides. */
function writeOf(row: ContentRow): Record<string, unknown> {
  return {
    kind: row.kind,
    slug: row.slug,
    ownerDepartment: row.ownerDepartment,
    visibility: row.visibility,
    isTemplate: row.isTemplate,
    title: row.title,
    summary: row.summary,
    seo: row.seo,
    body: row.body,
    schemaVersion: row.schemaVersion,
    collections: row.collections,
    coverMediaId: row.coverMediaId,
    pinned: row.pinned,
    sort: row.sort,
    fileMediaId: row.fileMediaId,
    rowVersion: row.rowVersion,
  };
}

/**
 * What the server demands on anything that changes state (`HubPipeline`): a cross site form can
 * post with the cookie attached, but it cannot set a header. The typed client sends it on every
 * call, so a helper here that forgot it is answered 403 — which is the product working, and is how
 * this line came to be written.
 */
const asTheClientDoes = { 'X-Requested-With': 'hub' };

/** The body of a call that must have worked, with the server's own words when it did not. */
async function answered(call: Promise<APIResponse>): Promise<ContentRow> {
  const response = await call;
  expect(response.status(), await response.text()).toBeLessThan(300);
  return (await response.json()) as ContentRow;
}

export function createContent(
  context: BrowserContext,
  row: Partial<ContentRow> & Pick<ContentRow, 'slug' | 'title' | 'body'>,
): Promise<ContentRow> {
  return answered(
    context.request.post('/api/content', {
      headers: asTheClientDoes,
      data: {
        kind: 'Page',
        ownerDepartment: department.toUpperCase(),
        visibility: 'Staff',
        isTemplate: false,
        summary: null,
        seo: null,
        schemaVersion: 1,
        collections: [],
        coverMediaId: null,
        pinned: false,
        sort: 0,
        fileMediaId: null,
        // The one a new row carries. An empty string is answered 400, which is how M1 found that
        // every create in the back office had been failing since M0 (handoff section 13).
        rowVersion: '0001-01-01T00:00:00',
        ...row,
      },
    }),
  );
}

export function readContent(context: BrowserContext, id: number): Promise<ContentRow> {
  return answered(context.request.get(`/api/content/${id}`));
}

/** The row back as it is, with whatever this caller changed on top. */
export function writeContent(
  context: BrowserContext,
  row: ContentRow,
  changes: Partial<ContentRow>,
): Promise<ContentRow> {
  return answered(
    context.request.put(`/api/content/${row.id}`, {
      headers: asTheClientDoes,
      data: { ...writeOf(row), ...changes },
    }),
  );
}

export function pageFromTemplate(
  context: BrowserContext,
  templateId: number,
  slug: string,
): Promise<ContentRow> {
  return answered(
    context.request.post(`/api/content/from-template/${templateId}`, {
      headers: asTheClientDoes,
      data: { ownerDepartment: department.toUpperCase(), slug },
    }),
  );
}

/**
 * A row a test made, taken back. ⚠️ The bench database is not thrown away between runs, and a
 * template left behind is a row in the template picker of every run after: after enough local runs
 * "Section page" had fallen off the picker's first page of a hundred, and the round could not
 * start. A row a test writes is a row that test takes back, as `menu.spec.ts` learnt first.
 */
export async function deleteContent(context: BrowserContext, id: number): Promise<void> {
  const response = await context.request.delete(`/api/content/${id}`, { headers: asTheClientDoes });
  expect(response.status(), await response.text()).toBeLessThan(300);
}

export function publishContent(context: BrowserContext, id: number): Promise<ContentRow> {
  // The changelog goes in even though it is null: the endpoint takes a body, and a POST with no
  // body at all is not routed to it — it comes back a bare 404, which reads like a missing row.
  return answered(
    context.request.post(`/api/content/${id}/publish`, {
      headers: asTheClientDoes,
      data: { changelog: null },
    }),
  );
}

/**
 * A picture put in a department's library through the API, as the library screen puts it there. The bytes are the
 * caller's: the library answers the file already there for the same bytes, so a run that wants its own file makes them
 * its own.
 */
export async function uploadMedia(
  context: BrowserContext,
  ownerDepartment: string,
  name: string,
  bytes: Buffer,
): Promise<number> {
  const response = await context.request.post('/api/media', {
    headers: asTheClientDoes,
    multipart: { file: { name, mimeType: 'image/png', buffer: bytes }, ownerDepartment },
  });
  expect(response.status(), await response.text()).toBeLessThan(300);
  return ((await response.json()) as { id: number }).id;
}

/** A file a test put in the library, taken back — for the same reason as `deleteContent`. */
export async function deleteMedia(context: BrowserContext, id: number): Promise<void> {
  const response = await context.request.delete(`/api/media/${id}`, { headers: asTheClientDoes });
  expect(response.status(), await response.text()).toBeLessThan(300);
}

/**
 * Airports of the bench: the snapshot of the world the fixtures of IVAO give it at its first start (T1). A leg needs
 * both of its airports there, and a tour of a kind with legs is ready only with one (M2, T7a).
 */
export const benchAirports = { rome: 'LIRF', milan: 'LIMC', bari: 'LIBD' } as const;

/** A leg at the end of a tour, through the editor's own call: for a spec whose subject is not the legs. */
export async function addLeg(
  context: BrowserContext,
  tourId: number,
  departureIcao: string = benchAirports.rome,
  arrivalIcao: string = benchAirports.milan,
): Promise<void> {
  const response = await context.request.post(`/api/flightops/tours/${tourId}/legs`, {
    headers: asTheClientDoes,
    data: {
      departureIcao,
      arrivalIcao,
      callsigns: null,
      flightNumbers: null,
      aircraft: null,
      releaseAt: null,
      changeReason: null,
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(response.status(), await response.text()).toBeLessThan(300);
}

/** The words of the tours the moves below press, read from the file the module ships. */
const tourWords = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../../locales/en/flightops.json', import.meta.url)), 'utf8'),
) as {
  tours: {
    fields: { title: string; summary: string; kind: string };
    options: { kind: { Free: string } };
    actions: { ready: string };
  };
};

function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/**
 * A Free tour released two days ago with one leg, Rome to Milan, made ready through the editor's own screens — the start
 * of every round about a pilot's report (T11b, T13b). Answers the tour's identifier.
 */
export async function releasedTourWithOneLeg(
  page: Page,
  context: BrowserContext,
  { name, slug }: { name: { en: string; it: string }; slug: string },
): Promise<number> {
  const now = Date.now();
  await page.goto('/staff/tours/new');
  await choose(page, tourWords.tours.fields.kind, tourWords.tours.options.kind.Free);
  await writeInBothLanguages(page.locator('form'), tourWords.tours.fields.title, 'title', name);
  await page.locator('[id="slug"]').fill(slug);
  await writeInBothLanguages(page.locator('form'), tourWords.tours.fields.summary, 'summary', {
    en: 'One leg to report.',
    it: 'Una leg da riportare.',
  });
  await page.locator('[id="releaseAt"]').fill(wallClock(new Date(now - 48 * 3600 * 1000)));
  await page.locator('[id="closeAt"]').fill(wallClock(new Date(now + 60 * 24 * 3600 * 1000)));
  await page.locator('[id="dailyLegLimit"]').fill('5');
  await whileWaitingFor(page, 'POST', '/api/flightops/tours', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/tours\/\d+$/);

  const tourId = Number(/\/staff\/tours\/(\d+)/.exec(page.url())![1]);
  await addLeg(context, tourId, benchAirports.rome, benchAirports.milan);

  await page.goto(`/staff/tours/${tourId}`);
  await whileWaitingFor(page, 'POST', `/api/flightops/tours/${tourId}/status`, async () => {
    await page.getByRole('button', { name: tourWords.tours.actions.ready }).click();
  });

  return tourId;
}

/**
 * The tours a run left behind, recognised by the start of their address: deleted, or — the server refuses to delete a
 * tour with reports (`tourHasReports`, design M2 §1.2.2) — hidden, which puts them on no public page and in no other
 * spec's way. One already hidden is left as it is.
 */
export async function removeBenchTours(context: BrowserContext, slugPrefix: string): Promise<void> {
  const response = await context.request.get(`/api/flightops/tours?pageSize=100&q=${slugPrefix}`);
  expect(response.status()).toBe(200);

  const found = ((await response.json()) as { items: { id: number; isHidden: boolean }[] }).items;
  for (const tour of found.filter((row) => !row.isHidden)) {
    const removed = await context.request.delete(`/api/flightops/tours/${tour.id}`, {
      headers: asTheClientDoes,
    });
    if (removed.status() < 300) {
      continue;
    }

    expect(await removed.text()).toContain('flightops:errors.tourHasReports');
    const hidden = await context.request.post(`/api/flightops/tours/${tour.id}/status`, {
      headers: asTheClientDoes,
      data: { action: 'Hide' },
    });
    expect(hidden.status(), await hidden.text()).toBeLessThan(300);
  }
}

/** Where the bench's mail lands: Mailpit, a service of the CI and a container on a developer's machine (T13b). */
export const mailpit = process.env.E2E_MAILPIT_URL ?? 'http://127.0.0.1:8025';

/** The mail Mailpit holds for this address about this run, read whole; null while it has not arrived. */
export async function mailFor(
  request: APIRequestContext,
  address: string,
  about: string,
): Promise<{ Subject: string; Text: string } | null> {
  const list = await request.get(`${mailpit}/api/v1/messages?limit=100`);
  expect(list.status(), `Mailpit at ${mailpit}`).toBe(200);

  const messages = (
    (await list.json()) as { messages: { ID: string; Subject: string; To: { Address: string }[] }[] }
  ).messages;
  const found = messages.find(
    (message) =>
      message.Subject.includes(about) && message.To.some((recipient) => recipient.Address === address),
  );
  if (found === undefined) {
    return null;
  }

  const read = await request.get(`${mailpit}/api/v1/message/${found.ID}`);
  return (await read.json()) as { Subject: string; Text: string };
}
