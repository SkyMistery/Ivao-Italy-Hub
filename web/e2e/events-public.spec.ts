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
 * in UTC and where the division lives —, who organises it, its airports and routes and its description, a cancelled one its note,
 * and its public slots (E5) by airport, departures and arrivals apart, free or taken, a leg of a rotation marked, a slot opened read
 * only — and a tap on a phone shows what a hover shows (note 2026-10-07-gli-slot-sulla-pagina-dell-evento); an event the reader
 * may not see is not found, and the
 * staff are told when nobody else sees it; the block draws the same cards on a page of the site. What the server decides — what is
 * public, the 404 after the end, who writes the routes and the slots — is proved by `EventsPublicTests` and `EventsSlotsTests`
 * (integration); the rounds against the real server are `full/events-public.spec.ts` and `full/events-slots.spec.ts`.
 */

/** The words of the module, read from the file the browser fetches: a copied sentence passes while the screen shows a key. */
const words = JSON.parse(
  readFileSync(fileURLToPath(new URL('../../locales/en/events.json', import.meta.url)), 'utf8'),
) as {
  events: {
    options: {
      state: { BookingOpen: string; Cancelled: string; Ended: string };
      organizer: { OtherDivision: string };
    };
  };
  routes: { fields: { route: string } };
  public: {
    title: string;
    none: string;
    noneHere: string;
    calendar: string;
    back: string;
    backOffice: string;
    staffOnly: { Draft: string; NotSeenYet: string; Over: string };
    wholeDivision: string;
    cancelled: string;
    routes: string;
    organizerPage: string;
    slots: string;
    free: string;
    taken: string;
    departures: string;
    arrivals: string;
    otherTypes: string;
    rotationHint: string;
    detail: { main: string; legs: string };
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

/** Airports as the server names them: by the name the hub knows, and one it no longer knows by its code alone. */
const airportA = { icao: 'XXAA', name: 'Smoke Airport A' };
const airportB = { icao: 'XXBB', name: null };
const airportC = { icao: 'XXCC', name: 'Smoke Airport C' };

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
    airports: [airportA],
    ...overrides,
  };
}

const evening = card(1, { airports: [airportA, airportB], state: 'BookingOpen' });
const another = card(2, { kind: 'meeting', airports: [airportC] });
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
              props: {
                markdown: { en: 'Bring your best landing.', it: 'Porta il tuo atterraggio migliore.' },
              },
            },
          ],
        },
      ],
    },
    bannerMediaId: null,
    startsAtUtc: '2099-11-21T18:00:00.000Z',
    endsAtUtc: '2099-11-21T22:00:00.000Z',
    state: 'Announced',
    unseen: null,
    wholeDivision: false,
    airports: [airportA, airportB],
    routes: [
      {
        id: 7,
        departure: airportA,
        arrival: airportB,
        route: 'DCT SMOKE UL1 DCT',
        remarks: { en: 'Above the clouds.', it: 'Sopra le nuvole.' },
      },
    ],
    slots: [],
    cancelledAt: null,
    cancellationNote: null,
    ...overrides,
  };
}

/** A public slot of the page (E5), as the server lists it: free unless said, never who took it. */
function slot(
  id: number,
  callsign: string,
  hours: readonly [number, number],
  from: unknown,
  to: unknown,
  overrides: Record<string, unknown> = {},
) {
  const at = (hour: number) => `2099-11-21T${String(hour).padStart(2, '0')}:00:00.000Z`;

  return {
    id,
    callsign,
    flightNumber: null,
    aircraftTypes: ['XA20'],
    departure: from,
    arrival: to,
    offBlockUtc: at(hours[0]),
    onBlockUtc: at(hours[1]),
    stand: null,
    rotation: null,
    leg: null,
    isArrival: false,
    taken: false,
    ...overrides,
  };
}

/** The anonymous shell, with the division's word for the kind and the events' two reads answered on top of it. */
async function stubTheEvents(
  page: Page,
  cards: unknown[],
  pages: Record<string, unknown> = {},
): Promise<void> {
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
  // Its airports by code and, when the hub knows it, by name — as the page of the event names them.
  await expect(first).toContainText('XXAA · Smoke Airport A');
  await expect(first).toContainText('XXBB');
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

test('the address narrows the cards to an airport and a kind, and says when nothing is left', async ({
  page,
}) => {
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

  // Of a kind at an airport none of them has: nothing, said. The filter names the airport as the cards do.
  await page.goto('/events?kind=meeting&airport=XXAA');
  await expect(page.getByText(words.public.noneHere)).toBeVisible();
  await expect(page.locator('#events-airport')).toContainText('XXAA · Smoke Airport A');
});

test('with nothing to come, /events says so', async ({ page }) => {
  await stubTheEvents(page, []);

  await page.goto('/events');

  await expect(page.getByText(words.public.none)).toBeVisible();
});

test('the page of an event says when, who organises it, where, its routes and its description', async ({
  page,
}) => {
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
  for (const line of Object.values(words.public.staffOnly)) {
    await expect(page.getByText(line)).toHaveCount(0);
  }
  await expect(page.getByRole('link', { name: words.public.backOffice })).toHaveCount(0);
  await expect(page.getByRole('link', { name: words.public.back })).toHaveAttribute('href', '/events');
});

/**
 * A rotation out of A and back, and a flight alone landing at B, taken (note 2026-10-07-gli-slot-sulla-pagina-dell-evento): by their
 * off block, as the server sends them.
 */
const slotsOfTwoAirports = [
  slot(1, 'XSM101', [18, 19], airportA, airportC, {
    flightNumber: 'XS101',
    stand: 'B12',
    rotation: 'R1',
    leg: 1,
    aircraftTypes: ['XA20', 'XA21'],
  }),
  slot(2, 'XSM300', [19, 20], airportC, airportB, { isArrival: true, taken: true }),
  slot(3, 'XSM102', [20, 21], airportC, airportA, { rotation: 'R1', leg: 2, isArrival: true }),
];

test('the page of an event lists its public slots by airport, departures and arrivals apart, and opens one', async ({
  page,
}) => {
  await stubTheEvents(page, [], { 'evt-test-smoke-page': event({ slots: slotsOfTwoAirports }) });

  await page.goto('/events/evt-test-smoke-page');

  const slots = page.getByRole('region', { name: words.public.slots });
  await expect(slots.getByRole('heading', { level: 2, name: words.public.slots })).toBeVisible();

  // Two airports of the event have slots: a section each. A's leg out among its departures, the leg back among its arrivals.
  const atA = slots.getByRole('region', { name: 'XXAA · Smoke Airport A' });
  const departuresA = atA.getByRole('table', { name: words.public.departures });
  const arrivalsA = atA.getByRole('table', { name: words.public.arrivals });
  await expect(departuresA.getByRole('row')).toHaveCount(2);
  await expect(arrivalsA.getByRole('row')).toHaveCount(2);

  // The flight: callsign and number, its main type, where it goes by name, its hour at A in UTC of the one day, the stand, free.
  const out = departuresA.getByRole('row').nth(1);
  for (const said of ['XSM101', 'XS101', 'Smoke Airport C', '18:00', 'B12', words.public.free]) {
    await expect(out).toContainText(said);
  }
  await expect(out).not.toContainText('XA21');
  await expect(arrivalsA.getByRole('row').nth(1)).toContainText('XSM102');

  // B, an airport the hub has no name for, has an arrival only, taken — and nothing of whoever took it.
  const atB = slots.getByRole('region', { name: 'XXBB' });
  await expect(atB.getByRole('table', { name: words.public.departures })).toHaveCount(0);
  await expect(atB.getByRole('table', { name: words.public.arrivals })).toContainText('XSM300');
  await expect(atB.getByRole('table', { name: words.public.arrivals })).toContainText(words.public.taken);

  // The other types the slot admits on hover.
  const types = out.getByRole('button', { name: /XA20/ });
  await types.hover();
  await expect(page.getByRole('tooltip')).toHaveText(words.public.otherTypes.replace('{{types}}', 'XA21'));

  // The two legs of the rotation are marked, the flight alone is not.
  await expect(slots.getByRole('button', { name: words.public.rotationHint })).toHaveCount(2);

  // A row opens the slot, read only: every type, the main one said, and the legs of its rotation, this one marked.
  await out.getByRole('cell', { name: 'B12' }).click();
  const detail = page.getByRole('dialog', { name: 'XSM101 · XS101' });
  await expect(detail).toContainText(`XA20 (${words.public.detail.main})`);
  await expect(detail).toContainText('XA21');
  const legs = detail.getByRole('region', { name: words.public.detail.legs.replace('{{rotation}}', 'R1') });
  await expect(legs.getByRole('listitem')).toHaveCount(2);
  await expect(legs.getByRole('listitem').nth(0)).toHaveAttribute('aria-current', 'true');
  await expect(legs.getByRole('listitem').nth(1)).toContainText('XSM102');

  // Closed, the focus is back on the callsign of the row that opened it, not at the top of the page.
  await page.keyboard.press('Escape');
  await expect(detail).toHaveCount(0);
  await expect(out.getByRole('button', { name: 'XSM101' })).toBeFocused();
});

test.describe('on a phone', () => {
  test.use({ hasTouch: true });

  test('a tap opens what a hover would show, the next tap closes it, and neither opens the slot', async ({
    page,
  }) => {
    await stubTheEvents(page, [], { 'evt-test-smoke-page': event({ slots: slotsOfTwoAirports }) });
    await page.goto('/events/evt-test-smoke-page');

    const mark = page.getByRole('button', { name: words.public.rotationHint }).first();
    await mark.tap();
    await expect(page.getByRole('tooltip')).toHaveText(words.public.rotationHint);
    await mark.tap();
    await expect(page.getByRole('tooltip')).toHaveCount(0);

    const types = page.getByRole('button', { name: /XA20/ });
    await types.tap();
    await expect(page.getByRole('tooltip')).toHaveText(words.public.otherTypes.replace('{{types}}', 'XA21'));
    await expect(page.getByRole('dialog')).toHaveCount(0);
  });
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

test('the staff of the events read an event nobody else sees, told so and why, with the way to the back office', async ({
  page,
}) => {
  await stubTheApiAsStaff(page, {
    ...staffBootstrap,
    permissions: [...staffBootstrap.permissions, { name: 'Events.View', department: 'ED' }],
  });
  await stubTheEventPages(page, {
    'evt-test-smoke-page': event({ unseen: 'Over', state: 'Ended' }),
    'evt-test-smoke-draft': event({ slug: 'evt-test-smoke-draft', unseen: 'Draft', state: 'Draft' }),
  });

  await page.goto('/events/evt-test-smoke-page');

  // The reason the server gives, and only that one.
  await expect(page.getByText(words.public.staffOnly.Over)).toBeVisible();
  await expect(page.getByText(words.public.staffOnly.Draft)).toHaveCount(0);
  await expect(page.getByText(words.events.options.state.Ended, { exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: words.public.backOffice })).toHaveAttribute(
    'href',
    '/staff/events/41',
  );

  await page.goto('/events/evt-test-smoke-draft');
  await expect(page.getByText(words.public.staffOnly.Draft)).toBeVisible();
  await expect(page.getByText(words.public.staffOnly.Over)).toHaveCount(0);
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
  await expect(page.getByRole('link', { name: words.blocks.eventList.all })).toHaveAttribute(
    'href',
    '/events',
  );
});
