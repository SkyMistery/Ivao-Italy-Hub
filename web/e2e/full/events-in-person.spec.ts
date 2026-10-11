import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { benchUrl, mailFor, readInEnglish, test, whileWaitingFor } from './bench';

/**
 * The "done when" of E16 (M4c), through the real screens and the real server: the coordinator of the events (`?as=events`) publishes
 * an event in person with three questions — a yes or no, several choices, a number —; the bench's pilot (`?as=pilot`, a member with a
 * mailbox of Mailpit) reads its venue on its page, registers answering the three, finds the registration with their answers in
 * `/events/mine`, and is told in Mailpit; the coordinator reads the sums in the tab «Questions» and the pilot in the tab
 * «Registrations». The rules of the answers are `EventsRegistrationsTests`'s.
 *
 * The bench survives between runs: the pilot withdraws from this spec's events and the coordinator deletes them after the test
 * (`afterwards`), and an interrupted run's leftovers at the start.
 */

const events = englishEvents();
// What the server writes in words — the answers, the sums — is in the language of the account, Italian for the bench's people: the
// cookie of the page changes the page only (the switcher of a member changes both).
const words = wordsOf('it');
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e16';
const stamp = Date.now().toString(36);
const slug = `${stem}-${stamp}`;
const title = { en: `Bench in person ${stamp}`, it: `Il banco in presenza ${stamp}` };
const venue = { en: `The bench hall ${stamp}`, it: `La sala del banco ${stamp}` };
const pilotAddress = 'bench-pilot@bench.test';
const blank = '0001-01-01T00:00:00';

/** The three questions, in both languages: the pilot's page reads English. */
const dinner = { en: 'Are you coming to dinner?', it: 'Vieni a cena?' };
const equipment = { en: 'What do you bring?', it: 'Che cosa porti?' };
const chairs = { en: 'How many chairs do you need?', it: 'Quante sedie ti servono?' };

/** A context of its own, reading in English, signed in as one of the bench's people. */
async function contextAs(browser: Browser, who: 'events' | 'pilot'): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
  expect(signedIn.status(), await signedIn.text()).toBe(200);

  return context;
}

/** The day of the event, twenty days from now. */
const day = new Date(Date.now() + 20 * 24 * 3600 * 1000).toISOString().slice(0, 10);

/** An event in person with its venue and its three questions, published. */
async function publishedEvent(request: APIRequestContext): Promise<number> {
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
      slug,
      summary: title,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: null,
      startsAtUtc: `${day}T09:00:00Z`,
      endsAtUtc: `${day}T18:00:00Z`,
      visibility: 'Public',
      rowVersion: blank,
      inPerson: true,
      venue,
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const questions = [
    { ordinal: 1, type: 'YesNo', label: dinner, choices: [], required: true },
    {
      ordinal: 2,
      type: 'MultiChoice',
      label: equipment,
      choices: [
        { key: null, label: { en: 'PC', it: 'PC' } },
        { key: null, label: { en: 'Monitor', it: 'Monitor' } },
      ],
      required: false,
    },
    { ordinal: 3, type: 'Quantity', label: chairs, choices: [], required: false },
  ];
  for (const question of questions) {
    const written = await request.post('/api/events/questions', {
      headers: asTheClientDoes,
      data: { eventId: id, ...question, rowVersion: blank },
    });
    expect(written.status(), await written.text()).toBe(201);
  }

  const published = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: blank },
  });
  expect(published.status(), await published.text()).toBe(200);

  return id;
}

/** What a run of this spec left: the pilot withdraws from its events — an event with registrations is not deleted —, then they go. */
async function removeOurEvents(coordinator: APIRequestContext, pilot: APIRequestContext): Promise<void> {
  const mine = await pilot.get('/api/events/mine/registrations');
  expect(mine.status(), await mine.text()).toBe(200);
  for (const registration of (await mine.json()) as readonly { eventId: number; eventSlug: string }[]) {
    if (registration.eventSlug.startsWith(stem)) {
      const withdrawn = await pilot.delete(`/api/events/mine/registrations/${registration.eventId}`, {
        headers: asTheClientDoes,
      });
      expect(withdrawn.status(), await withdrawn.text()).toBe(204);
    }
  }

  const page = await coordinator.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);
  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await coordinator.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('a pilot registers to an event in person answering three questions, and the staff read the sums', async ({
  browser,
  afterwards,
}) => {
  // The mail leaves with the core's dispatch, once a minute.
  test.setTimeout(240_000);

  const coordinator = await contextAs(browser, 'events');
  const pilot = await contextAs(browser, 'pilot');
  await removeOurEvents(coordinator.request, pilot.request);

  afterwards(async () => {
    await removeOurEvents(coordinator.request, pilot.request);
    await Promise.all([coordinator.close(), pilot.close()]);
  });

  const id = await publishedEvent(coordinator.request);

  const page = await pilot.newPage();
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });

  // ---------------------------------------------------------------- the pilot registers from the page of the event
  await page.goto(`/events/${slug}`);
  await expect(page.getByRole('heading', { level: 1, name: title.en })).toBeVisible();
  await expect(page.locator('article > header')).toContainText(venue.en);

  const registration = page.getByRole('region', { name: events.public.registration.title });
  await registration.getByLabel(dinner.en).click();
  const brought = registration.getByRole('group', { name: equipment.en });
  await brought.getByLabel('PC').click();
  await brought.getByLabel('Monitor').click();
  await registration.getByLabel(chairs.en).fill('2');

  await whileWaitingFor(page, 'PUT', '/api/events/mine/registrations/', async () => {
    await registration.getByRole('button', { name: events.public.registration.register }).click();
  });
  await expect(registration.getByText(events.public.registration.registered)).toBeVisible();

  // ---------------------------------------------------------------- their registrations: this one, with the answers
  await page.goto('/events/mine');
  const mine = page
    .getByRole('region', { name: events.mine.registrations })
    .getByRole('listitem', { name: title.en });
  await expect(mine).toContainText(`${dinner.it}: ${words.registrations.answers.yes}`);
  await expect(mine).toContainText(`${equipment.it}: PC, Monitor`);
  await expect(mine).toContainText(`${chairs.it}: 2`);

  // ---------------------------------------------------------------- the coordinator reads the sums and the pilot
  const staff = await coordinator.newPage();
  staff.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
  await staff.goto(`/staff/events/${id}?tab=questions`);
  // The questions in the page's language, the sums in the account's.
  await expect(staff.getByRole('row').filter({ hasText: dinner.en })).toContainText(
    `${words.registrations.answers.yes} 1`,
  );
  await expect(staff.getByRole('row').filter({ hasText: equipment.en })).toContainText('Monitor 1');
  await expect(staff.getByRole('row').filter({ hasText: chairs.en })).toContainText(
    `${words.registrations.sums.total} 2`,
  );

  await staff.goto(`/staff/events/${id}?tab=registrations`);
  await expect(staff.getByRole('tab', { name: events.events.tabs.registrations })).toHaveAttribute(
    'aria-selected',
    'true',
  );
  await expect(staff.getByRole('row').filter({ hasText: 'Bench Pilot (999002)' })).toContainText(
    `${chairs.it}: 2`,
  );

  // The pilot is told, in their mailbox and in their language: the run's stamp is in the title of both.
  await expect
    .poll(async () => (await mailFor(pilot.request, pilotAddress, stamp))?.Text ?? '', {
      timeout: 150_000,
      intervals: [5_000],
    })
    .toContain(venue.it);
});

/** The words the server writes, in a language, from the copy `pnpm i18n:sync` keeps at the root. */
function wordsOf(locale: string) {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL(`../../../locales/${locale}/events.json`, import.meta.url)), 'utf8'),
  ) as { registrations: { answers: { yes: string }; sums: { total: string } } };
}

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { tabs: { registrations: string } };
    public: { registration: { title: string; register: string; registered: string } };
    mine: { registrations: string };
  };
}
