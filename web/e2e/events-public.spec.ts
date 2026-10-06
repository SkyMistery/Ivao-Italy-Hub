import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

import { expect, test, type Page } from '@playwright/test';

import {
  anonymousBootstrap,
  staffBootstrap,
  stubTheApi,
  stubTheApiAsStaff,
  stubTheBlockData,
  stubThePublishedPage,
} from './fixtures';
import { englishCommon } from './locales';

/**
 * The public side of the events in a browser, with the API stubbed (M4, E4): `/events` draws the events to come as cards from the
 * block's answer, narrowed by the address to a kind and an airport, with the calendar under them; the page of an event shows when —
 * in UTC and where the division lives —, who organises it, its airports and routes and its description, a cancelled one its note;
 * an event the reader may not see is not found, and the staff are told when nobody else sees it; the block draws the same cards on
 * a page of the site. What the server decides — what is public, the 404 after the end, who writes the routes — is proved by
 * `EventsPublicTests` (integration); the round against the real server is `full/events-public.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/events.json', import.meta.url)), 'utf8'),
) as {
  events: { options: { state: Record<string, string>; organizer: Record<string, string> }; fields: { airports: string } };
  routes: { fields: { route: string } };
  public: {
    title: string;
    none: string;
    noneHere: string;
    calendar: string;
    back: string;
    backOffice: string;
    staffOnly: string;
    wholeDivision: string;
    cancelled: string;
    routes: string;
    organizerPage: string;
  };
  blocks: { eventList: { all: string } };
};

const json = (body: unknown, status = 200) => ({
  status,
  contentType: 'application/json',
  body: JSON.stringify(body),
});

/** The division's word for the kind of these events, as the bootstrap carries it. */
const rfo = { key: 'rfo', label: { en: 'Real Flight Ops', it: 'Real Flight Ops' }, colour: 'blue' };

/** A visitor of a division whose calendar has a word for the kind of these events. */
const visitorBootstrap = { ...anonymousBootstrap, calendarKinds: [...anonymousBootstrap.calendarKinds, rfo] };

function card(id: number, overrides: Record<string, unknown> = {}) {
  return {
    id,
    slug: `evt-test-smoke-${id}`,
    kind: 'rfo',
    title: { en: `Smoke event ${id}`, it: `Evento smoke ${id}` },
    summary: { en: `What happens at event ${id}`, it: `Che cosa succede all'evento ${id}` },
    bannerMediaId: null,
    state: 'Announced',
    startsAtUtc: `2099-11-${String(10 + id).padStart(2, '0')}T18:00:00.000Z`,
    endsAtUtc: `2099-11-${String(10 + id).padStart(2, '0')}T22:00:00.000Z`,
    wholeDivision: false,
    airports: ['XXAA'],
    ...overrides,
  };
}

const evening = card(1, { airports: ['XXAA', 'XXBB'], state: 'BookingOpen' });
const another = card(2, { kind: 'meeting', airports: ['XXCC'] });
const everywhere = card(3, { wholeDivision: true, airports: [] });

/** The page of an event, as `/api/events/public/{slug}` answers it. */
function event(overrides: Record<string, unknown> = {}) {
  return {
    id: 41,
    slug: 'evt-test-smoke-page',
    kind: 'rfo',
    organizer: 'OtherDivision',
    externalUrl: 'https://example.org/the-event',
    title: { en: 'A smoke evening', it: 'Una sera smoke' },
    summary: { en: 'Two airports and a route.', it: 'Due scali e una rotta.' },
    body: {
      schemaVersion: 1,
      sections: [
        {
          id: 's_text',
          layout: 'stacked',
          blocks: [
            {
              id: 'b_text',
              type: 'text',
              version: 1,
              props: { markdown: { en: 'Bring your best landing.', it: 'Porta il tuo atterraggio migliore.' } },
            },
          ],
        },
      ],
    },
    bannerMediaId: null,
    startsAtUtc: '2099-11-21T18:00:00.000Z',
    endsAtUtc: '2099-11-21T22:00:00.000Z',
    state: 'Announced',
    seen: true,
    wholeDivision: false,
    airports: [
      { icao: 'XXAA', name: 'Smoke Airport A' },
      { icao: 'XXBB', name: null },
    ],
    routes: [
      {
        id: 7,
        departure: { icao: 'XXAA', name: 'Smoke Airport A' },
        arrival: { icao: 'XXBB', name: null },
        route: 'DCT SMOKE UL1 DCT',
        remarks: { en: 'Above the clouds.', it: 'Sopra le nuvole.' },
      },
    ],
    cancelledAt: null,
    cancellationNote: null,
    ...overrides,
  };
}

/** The anonymous shell, with the division's word for the kind and the events' two reads answered on top of it. */
async function stubTheEvents(page: Page, cards: unknown[], pages: Record<string, unknown> = {}): Promise<void> {
  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(visitorBootstrap)));
  await stubTheBlockData(page, 'events.eventList', { items: cards });
  await stubTheEventPages(page, pages);
}

async function stubTheEventPages(page: Page, pages: Record<string, unknown>): Promise<void> {
  await page.route('**/api/events/public/*', (route) => {
    const slug = new URL(route.request().url()).pathname.split('/').at(-1) ?? '';
    return route.fulfill(slug in pages ? json(pages[slug]) : json({ title: 'Not Found', status: 404 }, 404));
  });
}

test.beforeEach(({ page }) => {
  page.on('pageerror', (error) => {
    throw new Error(`The page threw: ${error.message}`);
  });
});

test('a visitor reads the events to come as cards, each leading to its page, and the calendar under them', async ({
  page,
}) => {
  await stubTheEvents(page, [evening, another, everywhere]);

  await page.goto('/events?view=monthList&on=2099-11-01');

  await expect(page.getByRole('heading', { level: 1, name: words.public.title })).toBeVisible();

  // Every card, with its state, the division's word for its kind, where it is — the whole division for an online day.
  const cards = page.locator('article');
  const first = cards.getByRole('link', { name: /Smoke event 1/ });
  await expect(first).toHaveAttribute('href', '/events/evt-test-smoke-1');
  await expect(first).toContainText(words.events.options.state.BookingOpen);
  await expect(first).toContainText('Real Flight Ops');
  await expect(first).toContainText('XXAA · XXBB');
  await expect(cards.getByRole('link', { name: /Smoke event 3/ })).toContainText(words.public.wholeDivision);

  // When: in UTC, and where the division lives.
  await expect(first.getByText(/18:00 – 22:00Z$/)).toBeVisible();
  await expect(first.getByText(/19:00 – 23:00 LT\)$/)).toBeVisible();

  // The same events in the calendar, leading to the same pages.
  const calendar = page.getByRole('region', { name: words.public.calendar });
  await expect(calendar.getByRole('heading', { level: 2, name: words.public.calendar })).toBeVisible();
  await expect(calendar.getByRole('link', { name: 'Smoke event 2' })).toHaveAttribute(
    'href',
    '/events/evt-test-smoke-2',
  );
});

test('the address narrows the cards to an airport and a kind, and says when nothing is left', async ({ page }) => {
  await stubTheEvents(page, [evening, another, everywhere]);

  const cards = page.locator('article');

  // At one airport: the events that name it, and not the one of the whole division, which names none.
  await page.goto('/events?airport=XXBB');
  await expect(cards.getByRole('link', { name: /Smoke event 1/ })).toBeVisible();
  await expect(cards.getByRole('link', { name: /Smoke event 2/ })).toHaveCount(0);
  await expect(cards.getByRole('link', { name: /Smoke event 3/ })).toHaveCount(0);

  // Of one kind.
  await page.goto('/events?kind=meeting');
  await expect(cards.getByRole('link', { name: /Smoke event 2/ })).toBeVisible();
  await expect(cards.getByRole('link', { name: /Smoke event 1/ })).toHaveCount(0);

  // Of a kind at an airport none of them has: nothing, said.
  await page.goto('/events?kind=meeting&airport=XXAA');
  await expect(page.getByText(words.public.noneHere)).toBeVisible();
});

test('with nothing to come, /events says so', async ({ page }) => {
  await stubTheEvents(page, []);

  await page.goto('/events');

  await expect(page.getByText(words.public.none)).toBeVisible();
});

test('the page of an event says when, who organises it, where, its routes and its description', async ({ page }) => {
  await stubTheEvents(page, [], { 'evt-test-smoke-page': event() });

  await page.goto('/events/evt-test-smoke-page');

  await expect(page.getByRole('heading', { level: 1, name: 'A smoke evening' })).toBeVisible();
  await expect(page.getByText('Real Flight Ops')).toBeVisible();

  // When, in UTC and where the division lives: one date and two times, for an evening.
  const facts = page.locator('dl');
  await expect(facts.getByText(/18:00 – 22:00Z$/)).toBeVisible();
  await expect(facts.getByText(/19:00 – 23:00 LT\)$/)).toBeVisible();

  // Who organises it, with their page; its airports, by name when the hub knows it.
  await expect(facts.getByText(words.events.options.organizer.OtherDivision)).toBeVisible();
  await expect(facts.getByRole('link', { name: words.public.organizerPage })).toHaveAttribute(
    'href',
    'https://example.org/the-event',
  );
  await expect(facts.getByText('Smoke Airport A')).toBeVisible();

  // The route of the flight operations, and the description.
  await expect(page.getByRole('heading', { level: 2, name: words.public.routes })).toBeVisible();
  const route = page.getByRole('row').filter({ hasText: 'DCT SMOKE UL1 DCT' });
  await expect(route).toContainText('XXAA');
  await expect(route).toContainText('Above the clouds.');
  await expect(page.getByText('Bring your best landing.')).toBeVisible();

  // Not a page only the staff see, and no way to the back office for a visitor.
  await expect(page.getByText(words.public.staffOnly)).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.public.backOffice })).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.public.back })).toHaveAttribute('href', '/events');
});

test('a cancelled event shows its note', async ({ page }) => {
  await stubTheEvents(page, [], {
    'evt-test-smoke-page': event({
      state: 'Cancelled',
      cancelledAt: '2099-11-20T09:00:00.000Z',
      cancellationNote: { en: 'The weather closed both airports.', it: 'Il meteo ha chiuso i due scali.' },
    }),
  });

  await page.goto('/events/evt-test-smoke-page');

  await expect(page.getByText(words.public.cancelled)).toBeVisible();
  await expect(page.getByText('The weather closed both airports.')).toBeVisible();
  await expect(page.getByText(words.events.options.state.Cancelled, { exact: true })).toBeVisible();
});

test('an event the reader may not see is not found', async ({ page }) => {
  await stubTheEvents(page, []);

  await page.goto('/events/evt-test-smoke-gone');

  await expect(page.getByRole('heading', { name: englishCommon.notFound.title })).toBeVisible();
});

test('the staff of the events read an event nobody else sees, told so, with the way to the back office', async ({
  page,
}) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [...staffBootstrap.permissions, { name: 'Events.View', department: 'ED' }],
  });
  await stubTheEventPages(page, { 'evt-test-smoke-page': event({ seen: false, state: 'Ended' }) });

  await page.goto('/events/evt-test-smoke-page');

  await expect(page.getByText(words.public.staffOnly)).toBeVisible();
  await expect(page.getByText(words.events.options.state.Ended, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.public.backOffice })).toHaveAttribute(
    'href',
    '/staff/events/41',
  );
});

test('on a page of the site the block draws the same cards, and the way to all of them', async ({ page }) => {
  await stubThePublishedPage(page, 'events-on-a-page', {
    schemaVersion: 1,
    sections: [
      {
        id: 's_main',
        layout: 'stacked',
        blocks: [{ id: 'b_events', type: 'events.eventList', version: 1, props: { kinds: [], limit: 3 } }],
      },
    ],
  });
  await stubTheBlockData(page, 'events.eventList', { items: [evening] });

  await page.goto('/events-on-a-page');

  await expect(page.locator('article').getByRole('link', { name: /Smoke event 1/ })).toHaveAttribute(
    'href',
    '/events/evt-test-smoke-1',
  );
  await expect(page.getByRole('link', { name: words.blocks.eventList.all })).toHaveAttribute('href', '/events');
});
