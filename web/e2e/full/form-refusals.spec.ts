import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import {
  expect,
  type APIRequestContext,
  type Browser,
  type BrowserContext,
  type Locator,
  type Page,
} from '@playwright/test';

import { englishCommon } from '../locales';

import {
  benchUrl,
  choose,
  deleteMedia,
  readInEnglish,
  test,
  uploadMedia,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The core's generated form against the real server, in the two ways it used to leave a person with nothing on screen.
 *
 * - **A refusal of the whole row** (#331). The server refuses a registration, or an availability, for an event that closed
 *   meanwhile under `id`, a name the form has no field for; the form handed it to a field nobody draws, and the button stopped
 *   spinning in silence. The bench's pilot opens the event while it takes them, the coordinator of the events cancels it, the
 *   pilot presses the button: the sentence above the form says they are closed.
 * - **A row that moves under the form** (#332, item 1). On a contact message of the back office a reply moves the status and the
 *   row's version; on a file of the library, archiving it does. The form kept the version it was mounted with, and the next save
 *   was answered «somebody else changed this». Now the form takes the new row: the status shows «Answered», and the save goes.
 *
 * The bench survives between runs: the events and the file of this spec are deleted after the test (`afterwards`), and an
 * interrupted run's events at the start; a contact message is never deleted, and each run leaves one, named after the run.
 */

const events = englishEvents();
const words = englishCommon as unknown as {
  departments: { FOD: string };
  common: { save: string };
  contacts: {
    title: string;
    fields: { status: string };
    options: { status: { Answered: string; Closed: string } };
    thread: { send: string; fields: { body: string } };
  };
  media: { fields: { alt: string; category: string }; archive: { action: string; notice: string } };
};
const conflict = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/errors.json', import.meta.url)), 'utf8'),
  ) as { errors: { conflict: { title: string } } }
).errors.conflict.title;

const asTheClientDoes = { 'X-Requested-With': 'hub' };
const blank = '0001-01-01T00:00:00';
// Not under `evt-test-e2e`, which `events-staff.spec.ts` deletes whole.
const stem = 'evt-e2e-refusal';
const stamp = Date.now().toString(36);
const title = { en: `Bench refusal ${stamp}`, it: `Il rifiuto del banco ${stamp}` };

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot' | null): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(who === null ? '/e2e/signin' : `/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/**
 * An event twenty days from now, published: in person with no question — a registration form with nothing but its button —, or
 * with an ATC roster and the bench's Rome tower open — its availabilities close three days before it, by the settings' default.
 */
async function publishedEvent(request: APIRequestContext, shape: 'inPerson' | 'roster'): Promise<number> {
  const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfe',
      publicSlots: false,
      privateSlots: false,
      wholeDivision: false,
      organizer: 'Division',
      externalUrl: null,
      title,
      slug: slugOf(shape),
      summary: title,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: null,
      startsAtUtc: `${day}T09:00:00Z`,
      endsAtUtc: `${day}T18:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
      ...(shape === 'inPerson' ? { inPerson: true, venue: title } : { hasRoster: true, shiftMinutes: null }),
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  if (shape === 'roster') {
    const position = await request.post('/api/events/atc/positions', {
      headers: asTheClientDoes,
      data: { eventId: id, callsign: 'LIRF_TWR', fromUtc: null, toUtc: null, note: null, rowVersion: blank },
    });
    expect(position.status(), await position.text()).toBe(201);
  }

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

function slugOf(shape: 'inPerson' | 'roster'): string {
  return `${stem}-${shape.toLowerCase()}-${stamp}`;
}

/** Presses the button and waits for the server's refusal of the whole row: a 400 under `id`, the one name the form has no field for. */
async function pressAndBeRefused(page: Page, button: Locator, urlPart: string): Promise<void> {
  const refused = page.waitForResponse(
    (response) => response.request().method() === 'PUT' && response.url().includes(urlPart),
  );
  await button.click();

  const answer = await refused;
  expect(answer.status()).toBe(400);
  expect(Object.keys(((await answer.json()) as { errors: Record<string, unknown> }).errors)).toEqual(['id']);
}

async function cancel(request: APIRequestContext, id: number): Promise<void> {
  const read = await request.get(`/api/events/events/${id}`);
  expect(read.status(), await read.text()).toBe(200);
  const { rowVersion } = (await read.json()) as { rowVersion: string };

  const cancelled = await request.post(`/api/events/events/${id}/cancel`, {
    headers: asTheClientDoes,
    data: { note: { en: 'Cancelled by the bench.', it: 'Annullato dal banco.' }, rowVersion },
  });
  expect(cancelled.status(), await cancelled.text()).toBe(200);
}

/** What a run of this spec left: its events, which nobody registered to nor gave an availability for. */
async function removeOurEvents(coordinator: APIRequestContext): Promise<void> {
  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('a registration refused because the event closed meanwhile is said above the form', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurEvents(coordinator.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const id = await publishedEvent(coordinator.request, 'inPerson');

  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/events/${slugOf('inPerson')}`);
  const registration = page.getByRole('region', { name: events.public.registration.title });
  const register = registration.getByRole('button', { name: events.public.registration.register });
  await expect(register).toBeVisible();

  // The page was drawn while the event took registrations; it does not any more.
  await cancel(coordinator.request, id);
  await pressAndBeRefused(page, register, '/api/events/mine/registrations/');

  await expect(registration.getByRole('alert')).toContainText(events.errors.registrationClosed);
  await expect(registration.getByText(events.public.registration.registered)).toHaveCount(0);
});

test('an availability refused because the event closed meanwhile is said above the form', async ({
  browser,
  afterwards,
}) => {
  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurEvents(coordinator.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const id = await publishedEvent(coordinator.request, 'roster');

  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/events/${slugOf('roster')}`);
  const atc = page.getByRole('region', { name: events.public.atc.title });
  const give = atc.getByRole('button', { name: events.public.atc.give });
  await expect(give).toBeVisible();

  // The page was drawn while the event took availabilities; it does not any more.
  await cancel(coordinator.request, id);
  await pressAndBeRefused(page, give, '/api/events/mine/availability/');

  await expect(atc.getByRole('alert')).toContainText(events.errors.availabilityClosed);
  await expect(atc.getByText(events.public.atc.given)).toHaveCount(0);
});

test('a reply moves the row under the status form, and closing the message afterwards saves', async ({
  browser,
}) => {
  const staff = await contextAs(browser, null);
  const pilot = await contextAs(browser, 'pilot');

  const sent = await pilot.request.post('/api/contacts', {
    headers: asTheClientDoes,
    data: { department: 'FOD', subject: `Bench row version ${stamp}`, body: 'Is the third leg still open?' },
  });
  expect(sent.status(), await sent.text()).toBe(200);
  const { id } = (await sent.json()) as { id: number };
  await pilot.close();

  const page = await staff.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/staff/fod/contacts/${id}`);
  await page.getByLabel(words.contacts.thread.fields.body, { exact: true }).fill(`It is. ${stamp}`);
  await whileWaitingFor(page, 'POST', `/api/contacts/${id}/replies`, async () => {
    await page.getByRole('button', { name: words.contacts.thread.send, exact: true }).click();
  });
  await expect(page.getByText(`It is. ${stamp}`)).toBeVisible();

  // The form below shows the row as the reply left it, and holds its new version.
  const status = page.locator('form').filter({ has: page.getByRole('button', { name: words.common.save }) });
  await expect(status.getByRole('combobox')).toContainText(words.contacts.options.status.Answered);

  await choose(page, words.contacts.fields.status, words.contacts.options.status.Closed, status);
  await whileWaitingFor(page, 'PUT', `/api/contacts/${id}`, async () => {
    await status.getByRole('button', { name: words.common.save, exact: true }).click();
  });

  // Saved: back to the department's list, and never «somebody else changed this».
  await expect(page).toHaveURL(/\/staff\/fod\/contacts(\?|$)/);
  await expect(page.getByText(conflict)).toHaveCount(0);

  await staff.close();
});

test('archiving a file moves the row under its form, and saving the metadata afterwards goes', async ({
  browser,
  afterwards,
}) => {
  const staff = await contextAs(browser, null);
  // An eight by eight PNG, and the run's stamp after its end: the library answers the same bytes with the same file.
  const picture = Buffer.concat([
    Buffer.from(
      'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAFElEQVR42mP8z8BQz0AEYBxVSF+FANqkA/8ZBEwuAAAAAElFTkSuQmCC',
      'base64',
    ),
    Buffer.from(stamp),
  ]);
  const id = await uploadMedia(staff, 'FOD', `bench-row-version-${stamp}.png`, picture);

  afterwards(async () => {
    await deleteMedia(staff, id);
    await staff.close();
  });

  const page = await staff.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  await page.goto(`/staff/media/${id}`);
  await page.getByRole('button', { name: words.media.archive.action, exact: true }).click();
  await whileWaitingFor(page, 'POST', `/api/media/${id}/`, async () => {
    await page
      .getByRole('alertdialog')
      .getByRole('button', { name: words.media.archive.action, exact: true })
      .click();
  });
  await expect(page.getByText(words.media.archive.notice)).toBeVisible();

  // What a file needs before its metadata are saved, and one field more.
  await writeInBothLanguages(page.locator('form'), words.media.fields.alt, 'alt', {
    en: `A bench picture ${stamp}`,
    it: `Una figura del banco ${stamp}`,
  });
  await page.getByLabel(words.media.fields.category, { exact: true }).fill(`bench-${stamp}`);
  await whileWaitingFor(page, 'PUT', `/api/media/${id}`, async () => {
    await page.getByRole('button', { name: words.common.save, exact: true }).click();
  });

  // Saved: back to the library, and never «somebody else changed this».
  await expect(page).toHaveURL(/\/staff\/media(\?|$)/);
  await expect(page.getByText(conflict)).toHaveCount(0);
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    public: {
      registration: { title: string; register: string; registered: string };
      atc: { title: string; give: string; given: string };
    };
    errors: { registrationClosed: string; availabilityClosed: string };
  };
}
