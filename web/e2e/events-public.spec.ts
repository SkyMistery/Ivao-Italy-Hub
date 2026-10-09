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
 *
 * And the bookings (E6b): a visitor reads «taken» and nobody's name, and is asked to sign in to book; a member books a slot from
 * its dialog with the aircraft they choose, and finds it theirs; the page says when the bookings open and counts down to it; on the
 * event's day the core's strip counts its airports; the filters of the address narrow the slots; `/events/mine` lists a member's
 * bookings and withdraws one. The server's side is `EventsBookingsTests` and `EventsBookingPagesTests`, the round
 * `full/events-bookings.spec.ts`.
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
    yours: string;
    departures: string;
    arrivals: string;
    otherTypes: string;
    rotationHint: string;
    onlineAtTheAirports: string;
    detail: { main: string; legs: string };
    filters: { direction: string; noSlots: string };
    booking: {
      title: string;
      opensAt: string;
      openSince: string;
      untilOffBlock: string;
      notOpen: string;
      signIn: string;
      aircraft: string;
      book: string;
      yours: string;
      toMine: string;
    };
  };
  mine: {
    title: string;
    upcoming: string;
    past: string;
    withdraw: string;
    withdrawTitle: string;
  };
  blocks: { eventList: { all: string } };
};

/** The core's title of the strip asked for some airports (E4b), read from the file the browser fetches. */
const airportsTitle = (
  JSON.parse(
    readFileSync(fileURLToPath(new URL('../../locales/en/common.json', import.meta.url)), 'utf8'),
  ) as {
    liveStatus: { airportsTitle: string };
  }
).liveStatus.airportsTitle;

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
    bookingOpensAtUtc: null,
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

// ---- the bookings (E6b) ----------------------------------------------------------------------------------------------

/** A member of the division, signed in: no position, nothing of the staff. */
const memberBootstrap = {
  ...visitorBootstrap,
  user: {
    vid: 222222,
    firstName: 'Test',
    lastName: 'Pilot',
    positions: [],
    isStaff: false,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: [],
    firs: [],
    tokenAudiences: [],
  },
};

/** The bookings of the page's event opened at the start of the year: any slot whose off block is to come is booked. */
const OPENED = '2026-01-01T00:00:00.000Z';

/** A booking of the member's, as `/api/events/mine/bookings` lists it. */
function myBooking(
  id: number,
  slotId: number,
  callsign: string,
  hours: readonly [string, string],
  extra: object = {},
) {
  return {
    id,
    slotId,
    eventId: 41,
    eventSlug: 'evt-test-smoke-page',
    eventTitle: { en: 'A smoke evening', it: 'Una sera smoke' },
    eventState: 'BookingOpen',
    kind: 'Public',
    callsign,
    flightNumber: null,
    aircraftIcao: 'XA21',
    departureIcao: 'XXAA',
    offBlockUtc: hours[0],
    arrivalIcao: 'XXCC',
    onBlockUtc: hours[1],
    isArrival: false,
    stand: null,
    rotation: null,
    leg: null,
    withdrawable: true,
    createdAt: '2099-11-01T10:00:00.000Z',
    ...extra,
  };
}

test('a visitor reads «taken» and nobody’s name, and is asked to sign in to book a free slot', async ({
  page,
}) => {
  await stubTheEvents(page, [], {
    'evt-test-smoke-page': event({
      bookingOpensAtUtc: OPENED,
      state: 'BookingOpen',
      slots: slotsOfTwoAirports,
    }),
  });

  await page.goto('/events/evt-test-smoke-page');

  // The taken slot: taken, and nothing of whoever took it — nothing to book either.
  await page.getByRole('button', { name: 'XSM300' }).click();
  const taken = page.getByRole('dialog', { name: 'XSM300' });
  await expect(taken.getByText(words.public.taken)).toBeVisible();
  await expect(taken.getByRole('button', { name: /Book/ })).toHaveCount(0);
  await expect(taken.getByRole('link')).toHaveCount(0);
  await expect(taken).not.toContainText(/Test|Pilot|\d{6}/);
  await page.keyboard.press('Escape');

  // A free one: the way to sign in, back to this page.
  await page.getByRole('button', { name: 'XSM101' }).click();
  const free = page.getByRole('dialog', { name: 'XSM101 · XS101' });
  await expect(free.getByRole('link', { name: words.public.booking.signIn })).toHaveAttribute(
    'href',
    /^\/auth\/login\?returnUrl=/,
  );
  await expect(free.getByRole('button', { name: /Book/ })).toHaveCount(0);
});

test('a member books a free slot from its dialog with the aircraft they choose, and the slot is theirs', async ({
  page,
}) => {
  let booked = false;
  const page41 = (taken: boolean) =>
    event({
      bookingOpensAtUtc: OPENED,
      state: 'BookingOpen',
      slots: slotsOfTwoAirports.map((one) => (one.id === 1 ? { ...one, taken } : one)),
    });
  const theirs = myBooking(70, 1, 'XSM101', ['2099-11-21T18:00:00.000Z', '2099-11-21T19:00:00.000Z']);

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(memberBootstrap)));
  await page.route('**/api/events/public/*', (route) => route.fulfill(json(page41(booked))));
  await page.route('**/api/events/mine/bookings', async (route) => {
    if (route.request().method() === 'POST') {
      expect(route.request().postDataJSON()).toEqual({ slotId: 1, aircraftIcao: 'XA21' });
      booked = true;
      return route.fulfill(json(theirs, 201));
    }

    return route.fulfill(json(booked ? [theirs] : []));
  });

  await page.goto('/events/evt-test-smoke-page');
  await page.getByRole('button', { name: 'XSM101' }).click();
  const detail = page.getByRole('dialog', { name: 'XSM101 · XS101' });

  // The main type is chosen; the pilot flies the other one.
  const choice = detail.getByRole('radiogroup', { name: words.public.booking.aircraft });
  await expect(choice.getByRole('radio', { name: /XA20/ })).toBeChecked();
  await choice.getByRole('radio', { name: /XA21/ }).click();
  await detail
    .getByRole('button', { name: words.public.booking.book.replace('{{aircraft}}', 'XA21') })
    .click();

  await expect(detail.getByText(words.public.booking.yours.replace('{{aircraft}}', 'XA21'))).toBeVisible();
  await expect(detail.getByRole('link', { name: words.public.booking.toMine })).toHaveAttribute(
    'href',
    '/events/mine',
  );

  // Closed, the row is theirs; the page leads to their bookings.
  await page.keyboard.press('Escape');
  const row = page.getByRole('button', { name: 'XSM101' }).locator('xpath=ancestor::tr');
  await expect(row).toContainText(words.public.yours);
  await expect(page.getByRole('link', { name: words.mine.title })).toHaveAttribute('href', '/events/mine');
});

test('the page says when the bookings open, counts down to it, and offers nothing before', async ({
  page,
}) => {
  await stubTheEvents(page, [], {
    'evt-test-smoke-page': event({
      bookingOpensAtUtc: '2099-11-01T18:00:00.000Z',
      slots: slotsOfTwoAirports,
    }),
    'evt-test-smoke-open': event({
      slug: 'evt-test-smoke-open',
      bookingOpensAtUtc: OPENED,
      state: 'BookingOpen',
      slots: slotsOfTwoAirports,
    }),
  });

  await page.goto('/events/evt-test-smoke-page');

  // When, in UTC and where the division lives, and how long is left, to the second.
  const facts = page.locator('dl');
  await expect(facts.getByText(words.public.booking.opensAt)).toBeVisible();
  await expect(facts.getByText(/18:00Z$/).last()).toBeVisible();
  await expect(facts.getByText(/19:00 LT\)$/).last()).toBeVisible();
  const timer = facts.getByRole('timer');
  await expect(timer).toHaveText(/^In \d+ d \d{2}:\d{2}:\d{2}$/);
  const first = await timer.textContent();
  await expect(timer).not.toHaveText(first ?? '');

  // Nothing to book yet.
  await page.getByRole('button', { name: 'XSM101' }).click();
  await expect(page.getByRole('dialog').getByText(words.public.booking.notOpen)).toBeVisible();
  await page.keyboard.press('Escape');

  // Once open: since when, until each off block, and no count.
  await page.goto('/events/evt-test-smoke-open');
  await expect(page.locator('dl').getByText(words.public.booking.openSince)).toBeVisible();
  await expect(page.locator('dl').getByText(words.public.booking.untilOffBlock)).toBeVisible();
  await expect(page.locator('dl').getByRole('timer')).toHaveCount(0);
});

test('on the event’s day the core’s strip counts its airports, and not on another day', async ({ page }) => {
  const asked: unknown[] = [];
  const now = Date.now();

  await stubTheEvents(page, [], {
    'evt-test-smoke-page': event({
      startsAtUtc: new Date(now).toISOString(),
      endsAtUtc: new Date(now + 3_600_000).toISOString(),
    }),
    'evt-test-smoke-later': event({ slug: 'evt-test-smoke-later' }),
  });
  await page.route('**/api/blocks/data/networkStats**', (route) => {
    const encoded = new URL(route.request().url()).searchParams.get('props') ?? '';
    asked.push(JSON.parse(Buffer.from(encoded, 'base64url').toString('utf8')));
    return route.fulfill(
      json({
        updatedAt: new Date(now).toISOString(),
        figures: [
          { figure: 'divisionAtc', value: 2 },
          { figure: 'divisionPilots', value: 5 },
        ],
      }),
    );
  });

  await page.goto('/events/evt-test-smoke-page');

  const strip = page.getByRole('region', { name: words.public.onlineAtTheAirports });
  await expect(strip.getByText(airportsTitle)).toBeVisible();
  await expect
    .poll(() =>
      asked.some((props) => JSON.stringify((props as { airports?: unknown }).airports) === '["XXAA","XXBB"]'),
    )
    .toBe(true);

  // An event twenty years away is not on its day.
  await page.goto('/events/evt-test-smoke-later');
  await expect(page.getByRole('heading', { level: 1, name: 'A smoke evening' })).toBeVisible();
  await expect(page.getByRole('region', { name: words.public.onlineAtTheAirports })).toHaveCount(0);
});

test('the filters of the address narrow the slots, and a choice is kept in it', async ({ page }) => {
  await stubTheEvents(page, [], { 'evt-test-smoke-page': event({ slots: slotsOfTwoAirports }) });

  await page.goto('/events/evt-test-smoke-page?direction=arrivals');

  const slots = page.getByRole('region', { name: words.public.slots });
  await expect(slots.getByRole('table', { name: words.public.arrivals })).toHaveCount(2);
  await expect(slots.getByRole('table', { name: words.public.departures })).toHaveCount(0);

  // Departures, chosen on the page: the address says it.
  await page.locator('#slots-direction').click();
  await page.getByRole('option', { name: words.public.departures, exact: true }).click();
  await expect(page).toHaveURL(/direction=departures/);
  await expect(slots.getByRole('table', { name: words.public.arrivals })).toHaveCount(0);
  await expect(slots.getByRole('table', { name: words.public.departures })).toHaveCount(1);

  // A filter nothing passes says so.
  await page.goto('/events/evt-test-smoke-page?type=XNONE');
  await expect(page.getByText(words.public.filters.noSlots)).toBeVisible();
});

test('/events/mine lists a member’s flights still to fly and the past ones, and withdraws one', async ({
  page,
}) => {
  let withdrawn = false;
  const toFly = myBooking(71, 1, 'XSM101', ['2099-11-21T18:00:00.000Z', '2099-11-21T19:00:00.000Z']);
  const flown = myBooking(72, 9, 'XSM050', ['2026-01-10T18:00:00.000Z', '2026-01-10T19:00:00.000Z'], {
    withdrawable: false,
    eventState: 'Ended',
  });

  await stubTheApi(page);
  await page.route('**/api/me', (route) => route.fulfill(json(memberBootstrap)));
  await page.route('**/api/events/mine/bookings', (route) =>
    route.fulfill(json(withdrawn ? [flown] : [flown, toFly])),
  );
  await page.route('**/api/events/mine/bookings/71', (route) => {
    expect(route.request().method()).toBe('DELETE');
    withdrawn = true;
    return route.fulfill({ status: 204 });
  });

  await page.goto('/events/mine');

  await expect(page.getByRole('heading', { level: 1, name: words.mine.title })).toBeVisible();
  const upcoming = page.getByRole('region', { name: words.mine.upcoming });
  const past = page.getByRole('region', { name: words.mine.past });
  await expect(upcoming).toContainText('XSM101');
  await expect(upcoming.getByRole('link', { name: 'A smoke evening' })).toHaveAttribute(
    'href',
    '/events/evt-test-smoke-page',
  );
  await expect(past).toContainText('XSM050');
  // A flight gone is not withdrawn.
  await expect(past.getByRole('button', { name: words.mine.withdraw })).toHaveCount(0);

  await upcoming.getByRole('button', { name: words.mine.withdraw }).click();
  const confirm = page.getByRole('alertdialog', {
    name: words.mine.withdrawTitle.replace('{{callsign}}', 'XSM101'),
  });
  await confirm.getByRole('button', { name: words.mine.withdraw }).click();

  await expect(page.getByText('XSM101')).toHaveCount(0);
  await expect(past).toContainText('XSM050');
});
