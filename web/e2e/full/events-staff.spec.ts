import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type Page } from '@playwright/test';

import { englishCommon, englishSeed } from '../locales';

import {
  benchAirports,
  benchUrl,
  choose,
  readInEnglish,
  whileWaitingFor,
  writeInBothLanguages,
} from './bench';

/**
 * The "done when" of E3a (M4), through the real screens, as the bench's coordinator of the events (`?as=events`, E1), who
 * holds only what the division gives the events department: an RFO is created — its switches preset by its kind as the
 * settings say —, gets two airports with their capacity in the generated forms of its tab, is reopened with everything it
 * was given, and is cancelled with its note in both languages; and an empty draft is deleted. And E3b's: «Publish» lists what an
 * event with slots still needs — an airport, the opening of its bookings —, and an event without them is published and announced.
 *
 * The bench survives between runs: the preset this spec writes is put back in a `finally`, its events are taken back, and an
 * interrupted run's leftovers — a preset of the RFO, events of its stem — are taken out at the start.
 */

const events = englishEvents();
const settingsUrl = '/api/modules/events/settings';
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e';
const stamp = Date.now().toString(36);

const rfo = {
  title: { en: `Bench RFO ${stamp}`, it: `RFO del banco ${stamp}` },
  summary: { en: `An RFO of the bench ${stamp}`, it: `Un RFO del banco ${stamp}` },
  slug: `${stem}-rfo-${stamp}`,
};
const draft = {
  title: { en: `Bench draft ${stamp}`, it: `Bozza del banco ${stamp}` },
  summary: { en: `A draft of the bench ${stamp}`, it: `Una bozza del banco ${stamp}` },
  slug: `${stem}-draft-${stamp}`,
};

interface Settings {
  readonly kindPresets: readonly { readonly kind: string }[];
}

/** The wall clock of an instant in UTC, as a field of an instant takes it. */
function wallClock(date: Date): string {
  return date.toISOString().slice(0, 16);
}

/** The events of this spec's stem a run left behind, deleted through the API. */
async function removeOurEvents(request: APIRequestContext): Promise<void> {
  const page = await request.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);

  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await request.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

/** A new event through its form: the kind first — it presets the switches —, then its words and its window. */
async function newEvent(
  page: Page,
  kind: string,
  words: { title: { en: string; it: string }; summary: { en: string; it: string }; slug: string },
): Promise<void> {
  await page.goto('/staff/events');
  await page.getByRole('link', { name: events.events.create }).first().click();
  await expect(page.getByRole('heading', { name: events.events.create })).toBeVisible();

  await choose(page, events.events.fields.kind, kind);

  const starts = new Date(Date.now() + 30 * 24 * 3600 * 1000);
  const form = page.locator('form');
  await writeInBothLanguages(form, events.events.fields.title, 'title', words.title);
  await page.locator('[id="slug"]').fill(words.slug);
  await writeInBothLanguages(form, events.events.fields.summary, 'summary', words.summary);
  await page.locator('[id="startsAtUtc"]').fill(wallClock(starts));
  await page.locator('[id="endsAtUtc"]').fill(wallClock(new Date(starts.getTime() + 4 * 3600 * 1000)));

  await whileWaitingFor(page, 'POST', '/api/events/events', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/\/staff\/events\/\d+$/);
}

/** An airport of the event, in the generated form of its tab. */
async function newAirport(
  page: Page,
  icao: string,
  ordinal: string,
  capacity: Record<string, string>,
): Promise<void> {
  await page.getByRole('link', { name: events.airports.create }).click();
  await expect(page.getByRole('heading', { name: events.airports.create })).toBeVisible();
  await page.locator('[id="icao"]').fill(icao);
  await page.locator('[id="ordinal"]').fill(ordinal);
  for (const [field, value] of Object.entries(capacity)) {
    await page.locator(`[id="${field}"]`).fill(value);
  }

  await whileWaitingFor(page, 'POST', '/api/events/airports', async () => {
    await page.getByRole('button', { name: englishCommon.common.save }).click();
  });
  await expect(page).toHaveURL(/tab=airports/);
}

test('the coordinator of the events creates an RFO with two airports, reopens it and cancels it, and deletes an empty draft', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post('/e2e/signin?as=events');
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  // The settings as this spec found them, without a preset of the RFO an interrupted run left behind: put back at the end.
  const found = (await (await context.request.get(settingsUrl)).json()) as Settings;
  const saved = { ...found, kindPresets: found.kindPresets.filter((preset) => preset.kind !== 'rfo') };
  await removeOurEvents(context.request);

  const page = await context.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  try {
    // The division says what an RFO switches on: public and private slots.
    const preset = {
      kind: 'rfo',
      publicSlots: true,
      privateSlots: true,
      hasRoster: false,
      wholeDivision: false,
      inPerson: false,
    };
    const written = await context.request.put(settingsUrl, {
      headers: asTheClientDoes,
      data: { ...saved, kindPresets: [...saved.kindPresets, preset] },
    });
    expect(written.status(), await written.text()).toBe(200);

    // ---------------------------------------------------------------- the RFO, preset by its kind
    await newEvent(page, englishSeed.seed.calendarKinds.rfo, rfo);
    const eventUrl = page.url();
    await expect(page.getByRole('heading', { name: rfo.title.en })).toBeVisible();
    await expect(page.locator('[id="publicSlots"]')).toBeChecked();
    await expect(page.locator('[id="privateSlots"]')).toBeChecked();
    await expect(page.locator('[id="wholeDivision"]')).not.toBeChecked();

    // ---------------------------------------------------------------- two airports with their capacity
    await page.getByRole('tab', { name: events.events.tabs.airports }).click();
    await expect(page).toHaveURL(/tab=airports/);
    await newAirport(page, benchAirports.rome, '1', { maxMovementsPerHour: '30' });
    await newAirport(page, benchAirports.milan, '2', {
      maxArrivalsPerHour: '20',
      maxDeparturesPerHour: '20',
    });

    // ---------------------------------------------------------------- reopened, with everything it was given
    await page.goto(eventUrl);
    await expect(page.getByRole('heading', { name: rfo.title.en })).toBeVisible();
    await expect(page.getByText(events.events.options.state.Draft, { exact: true })).toBeVisible();
    await expect(page.locator('[id="slug"]')).toHaveValue(rfo.slug);
    await expect(page.locator('[id="publicSlots"]')).toBeChecked();
    await expect(page.locator('[id="privateSlots"]')).toBeChecked();

    await page.getByRole('tab', { name: events.events.tabs.airports }).click();
    const rome = page.getByRole('row').filter({ hasText: benchAirports.rome });
    const milan = page.getByRole('row').filter({ hasText: benchAirports.milan });
    await expect(rome).toContainText('30');
    await expect(milan).toContainText('20');

    // ---------------------------------------------------------------- cancelled, with its note
    await page.getByRole('link', { name: events.events.actions.cancel }).click();
    await expect(page.getByRole('heading', { name: events.cancel.title })).toBeVisible();
    await writeInBothLanguages(page.locator('form'), events.cancel.fields.note, 'note', {
      en: `The bench closes ${stamp}`,
      it: `Il banco chiude ${stamp}`,
    });
    await whileWaitingFor(page, 'POST', '/cancel', async () => {
      await page.getByRole('button', { name: events.cancel.submit }).click();
    });

    await expect(page).toHaveURL(eventUrl);
    await expect(page.getByText(`The bench closes ${stamp}`)).toBeVisible();
    await expect(page.getByText(events.events.options.state.Cancelled, { exact: true })).toBeVisible();
    // Neither cancelled again nor offered to be.
    await expect(page.getByRole('link', { name: events.events.actions.cancel })).toHaveCount(0);

    // Listed among the cancelled ones.
    await page.goto(`/staff/events?view=cancelled&q=${rfo.slug}`);
    await expect(page.getByRole('row').filter({ hasText: rfo.title.en })).toBeVisible();

    // ---------------------------------------------------------------- an empty draft, deleted
    await newEvent(page, englishSeed.seed.calendarKinds.rfe, draft);
    const draftId = Number(new URL(page.url()).pathname.split('/').pop());
    await page.getByRole('button', { name: englishCommon.common.delete }).first().click();
    await whileWaitingFor(page, 'DELETE', `/api/events/events/${draftId}`, async () => {
      await page.getByRole('alertdialog').getByRole('button', { name: englishCommon.common.delete }).click();
    });
    await expect(page).toHaveURL(/\/staff\/events(\?|$)/);

    const gone = await context.request.get(`/api/events/events/${draftId}`);
    expect(gone.status()).toBe(404);
  } finally {
    await removeOurEvents(context.request);
    const putBack = await context.request.put(settingsUrl, { headers: asTheClientDoes, data: saved });
    expect(putBack.status(), await putBack.text()).toBe(200);
    await context.close();
  }
});

test('the coordinator of the events reads what an event still needs, and publishes it', async ({
  browser,
}) => {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post('/e2e/signin?as=events');
  expect(signedIn.status(), await signedIn.text()).toBe(200);
  await removeOurEvents(context.request);

  const page = await context.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  const announced = {
    title: { en: `Bench announced ${stamp}`, it: `Annunciato del banco ${stamp}` },
    summary: { en: `An event of the bench ${stamp}`, it: `Un evento del banco ${stamp}` },
    slug: `${stem}-announced-${stamp}`,
  };

  try {
    // A draft with public slots and nothing else: no airport, no opening of the bookings.
    await newEvent(page, englishSeed.seed.calendarKinds.rfe, announced);
    const eventUrl = page.url();
    await page.locator('[id="publicSlots"]').setChecked(true);
    await page.locator('[id="privateSlots"]').setChecked(false);
    await whileWaitingFor(page, 'PUT', '/api/events/events/', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });

    // «Publish» answers with what it still needs, field by field, and publishes nothing.
    const refused = page.waitForResponse(
      (response) => response.request().method() === 'POST' && response.url().endsWith('/publish'),
    );
    await page.getByRole('button', { name: events.events.actions.publish }).click();
    expect((await refused).status()).toBe(400);
    await expect(page.getByText(events.events.publish.problems)).toBeVisible();
    await expect(page.getByRole('listitem').filter({ hasText: events.events.fields.airports })).toContainText(
      events.errors.slotsNeedAirports,
    );
    await expect(
      page.getByRole('listitem').filter({ hasText: events.events.fields.bookingOpensAtUtc }),
    ).toContainText(events.errors.bookingOpensRequired);
    await expect(page.getByText(events.events.options.state.Draft, { exact: true })).toBeVisible();

    // Without slots it needs neither: published, and seen at once — its «Seen from» is empty.
    await page.locator('[id="publicSlots"]').setChecked(false);
    await whileWaitingFor(page, 'PUT', '/api/events/events/', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });
    await expect(page.getByText(events.events.publish.problems)).toHaveCount(0);
    await whileWaitingFor(page, 'POST', '/publish', async () => {
      await page.getByRole('button', { name: events.events.actions.publish }).click();
    });

    await expect(page.getByText(events.events.options.state.Announced, { exact: true })).toBeVisible();
    // Published once: not offered again.
    await expect(page.getByRole('button', { name: events.events.actions.publish })).toHaveCount(0);

    // Reopened, still announced; and listed among the upcoming ones.
    await page.goto(eventUrl);
    await expect(page.getByText(events.events.options.state.Announced, { exact: true })).toBeVisible();
    await page.goto(`/staff/events?view=upcoming&q=${announced.slug}`);
    await expect(page.getByRole('row').filter({ hasText: announced.title.en })).toBeVisible();
  } finally {
    await removeOurEvents(context.request);
    await context.close();
  }
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: {
      create: string;
      tabs: { airports: string };
      actions: { cancel: string; publish: string };
      publish: { problems: string };
      fields: { kind: string; title: string; summary: string; airports: string; bookingOpensAtUtc: string };
      options: { state: { Draft: string; Announced: string; Cancelled: string } };
    };
    cancel: { title: string; submit: string; fields: { note: string } };
    airports: { create: string };
    errors: { slotsNeedAirports: string; bookingOpensRequired: string };
  };
}
