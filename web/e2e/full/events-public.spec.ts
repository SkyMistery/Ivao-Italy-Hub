import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type APIRequestContext, type Browser, type BrowserContext } from '@playwright/test';

import { englishCommon } from '../locales';

import { benchAirports, benchUrl, readInEnglish, whileWaitingFor, writeInBothLanguages } from './bench';

/**
 * The "done when" of E4 (M4), through the real screens and the real server: an event the coordinator of the events publishes,
 * the assistant coordinator of the flight operations (`?as=assistant`, `IT-FOAC`) gives a route — from the event's page in the back
 * office, in the generated form of its «Routes» tab, holding only what the division gives the flight operations on the events —,
 * and a visitor finds the event on `/events` and the route on its page. An event that ended is on no list and its page is not
 * found, to a visitor; the coordinator still reads it, told that nobody else does because it is over.
 *
 * The bench survives between runs: the events of this spec's stem are taken back in a `finally`, and an interrupted run's
 * leftovers at the start.
 */

const events = englishEvents();
const asTheClientDoes = { 'X-Requested-With': 'hub' };
const stem = 'evt-test-e2e-e4';
const stamp = Date.now().toString(36);

const published = {
  title: { en: `Bench evening ${stamp}`, it: `Serata del banco ${stamp}` },
  summary: { en: `An evening of the bench ${stamp}`, it: `Una serata del banco ${stamp}` },
  slug: `${stem}-evening-${stamp}`,
};
const over = {
  title: { en: `Bench yesterday ${stamp}`, it: `Il banco ieri ${stamp}` },
  summary: { en: `An event that ended ${stamp}`, it: `Un evento concluso ${stamp}` },
  slug: `${stem}-over-${stamp}`,
};
const route = `DCT BENCH${stamp.toUpperCase()} DCT`;

/** A context of its own, reading in English, signed in as one of the bench's people — or nobody, a visitor. */
async function contextAs(browser: Browser, who: 'events' | 'assistant' | null): Promise<BrowserContext> {
  const context = await browser.newContext({ baseURL: benchUrl });
  await readInEnglish(context);

  if (who !== null) {
    const signedIn = await context.request.post(`/e2e/signin?as=${who}`);
    expect(signedIn.status(), await signedIn.text()).toBe(200);
  }

  return context;
}

/** An event published through the API, starting `days` from now: what this spec is about is its routes and its page, not its form. */
async function publishEvent(
  request: APIRequestContext,
  words: { title: { en: string; it: string }; summary: { en: string; it: string }; slug: string },
  days: number,
): Promise<number> {
  const starts = new Date(Date.now() + days * 24 * 3600 * 1000);
  const created = await request.post('/api/events/events', {
    headers: asTheClientDoes,
    data: {
      kind: 'rfo',
      publicSlots: false,
      privateSlots: false,
      wholeDivision: false,
      organizer: 'Division',
      externalUrl: null,
      title: words.title,
      slug: words.slug,
      summary: words.summary,
      body: null,
      bannerMediaId: null,
      visibleFromUtc: null,
      bookingOpensAtUtc: null,
      startsAtUtc: starts.toISOString(),
      endsAtUtc: new Date(starts.getTime() + 4 * 3600 * 1000).toISOString(),
      visibility: 'Public',
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(created.status(), await created.text()).toBe(201);
  const id = ((await created.json()) as { id: number }).id;

  const airport = await request.post('/api/events/airports', {
    headers: asTheClientDoes,
    data: {
      eventId: id,
      icao: benchAirports.rome,
      ordinal: 1,
      maxMovementsPerHour: null,
      maxArrivalsPerHour: null,
      maxDeparturesPerHour: null,
      rowVersion: '0001-01-01T00:00:00',
    },
  });
  expect(airport.status(), await airport.text()).toBe(201);

  const publish = await request.post(`/api/events/events/${id}/publish`, {
    headers: asTheClientDoes,
    data: { rowVersion: '0001-01-01T00:00:00' },
  });
  expect(publish.status(), await publish.text()).toBe(200);

  return id;
}

/** The events of this spec's stem a run left behind, deleted through the API by whoever may. */
async function removeOurEvents(request: APIRequestContext): Promise<void> {
  const page = await request.get(`/api/events/events?q=${stem}&pageSize=100`);
  expect(page.status(), await page.text()).toBe(200);

  for (const row of ((await page.json()) as { items: readonly { id: number }[] }).items) {
    const deleted = await request.delete(`/api/events/events/${row.id}`, { headers: asTheClientDoes });
    expect(deleted.status(), await deleted.text()).toBe(204);
  }
}

test('the assistant of the flight operations gives a published event a route, and a visitor reads it on its page', async ({
  browser,
}) => {
  const coordinator = await contextAs(browser, 'events');
  await removeOurEvents(coordinator.request);

  const assistant = await contextAs(browser, 'assistant');
  const visitor = await contextAs(browser, null);

  try {
    const id = await publishEvent(coordinator.request, published, 20);
    await publishEvent(coordinator.request, over, -3);

    // ---------------------------------------------------------------- the route, from the event's page in the back office
    const page = await assistant.newPage();
    page.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });

    await page.goto(`/staff/events/${id}`);
    await expect(page.getByRole('heading', { name: published.title.en })).toBeVisible();
    // The event is theirs to read and not to write.
    await expect(page.getByText(events.events.readOnly)).toBeVisible();

    await page.getByRole('tab', { name: events.events.tabs.routes }).click();
    await expect(page).toHaveURL(/tab=routes/);
    await page.getByRole('link', { name: events.routes.create }).click();
    await expect(page.getByRole('heading', { name: events.routes.create })).toBeVisible();

    const form = page.locator('form');
    await page.locator('[id="departureIcao"]').fill(benchAirports.rome);
    await page.locator('[id="arrivalIcao"]').fill(benchAirports.milan);
    await page.locator('[id="route"]').fill(route);
    await writeInBothLanguages(form, events.routes.fields.remarks, 'remarks', {
      en: `Above the bench ${stamp}`,
      it: `Sopra il banco ${stamp}`,
    });
    await whileWaitingFor(page, 'POST', '/api/events/routes', async () => {
      await page.getByRole('button', { name: englishCommon.common.save }).click();
    });

    await expect(page).toHaveURL(/tab=routes/);
    await expect(page.getByRole('row').filter({ hasText: route })).toContainText(benchAirports.milan);

    // ---------------------------------------------------------------- a visitor: the card, then the page with the route
    const reader = await visitor.newPage();
    reader.on('pageerror', (error) => {
      throw new Error(`The page threw: ${error.message}`);
    });

    await reader.goto('/events');
    const card = reader.locator('article').getByRole('link', { name: new RegExp(published.title.en) });
    await expect(card).toHaveAttribute('href', `/events/${published.slug}`);
    // The event that ended is on no list.
    await expect(reader.getByText(over.title.en)).toHaveCount(0);

    await card.click();
    await expect(reader).toHaveURL(new RegExp(`/events/${published.slug}$`));
    await expect(reader.getByRole('heading', { level: 1, name: published.title.en })).toBeVisible();
    const row = reader.getByRole('row').filter({ hasText: route });
    await expect(row).toContainText(benchAirports.rome);
    await expect(row).toContainText(benchAirports.milan);
    await expect(row).toContainText(`Above the bench ${stamp}`);

    // ---------------------------------------------------------------- after the end: not found, but to the staff
    await reader.goto(`/events/${over.slug}`);
    await expect(reader.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();

    const staff = await coordinator.newPage();
    await staff.goto(`/events/${over.slug}`);
    await expect(staff.getByRole('heading', { level: 1, name: over.title.en })).toBeVisible();
    await expect(staff.getByText(events.public.staffOnly.Over)).toBeVisible();
  } finally {
    await removeOurEvents(coordinator.request);
    await Promise.all([coordinator.close(), assistant.close(), visitor.close()]);
  }
});

/** The module's own words, read from the copy `pnpm i18n:sync` keeps at the root. */
function englishEvents() {
  return JSON.parse(
    readFileSync(fileURLToPath(new URL('../../../locales/en/events.json', import.meta.url)), 'utf8'),
  ) as {
    events: { readOnly: string; tabs: { routes: string } };
    routes: { create: string; fields: { remarks: string } };
    public: { staffOnly: { Over: string } };
  };
}
