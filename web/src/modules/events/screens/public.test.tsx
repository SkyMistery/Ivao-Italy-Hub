import { TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { render, screen, within } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { PublicEventDto } from '../api';
import englishEvents from '../locales/en/events.json';

/**
 * The page of an event in its order (E8b; note 2026-10-11-la-testa-della-pagina-di-un-evento, dalberone's drawing): the head with
 * its two short lines, then what a pilot came for — the public slots, the private ones —, and at the bottom the description and the
 * routes. Against an API that answers what the test says, by address.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const { EventPublicPage } = await import('./public');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents;

const airportA = { icao: 'XXAA', name: 'Smoke Airport A' };
const airportC = { icao: 'XXCC', name: 'Smoke Airport C' };
const at = (hour: number) => `2099-11-21T${String(hour).padStart(2, '0')}:00:00Z`;

/** An evening at one airport with a public slot, a private one, a route and a description of one heading. */
const event = {
  id: 1,
  slug: 'evt-test-e8b-page',
  kind: 'rfo',
  organizer: 'Division',
  externalUrl: null,
  title: { en: 'Smoke evening' },
  summary: { en: 'Public and private slots.' },
  body: {
    schemaVersion: 1,
    sections: [
      {
        id: 's_about',
        key: 'about',
        title: { en: 'About' },
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [
          {
            id: 'b_about',
            type: 'heading',
            version: 1,
            props: { level: 2, text: { en: 'About the evening' } },
          },
        ],
      },
    ],
  },
  bannerMediaId: null,
  startsAtUtc: at(17),
  endsAtUtc: at(21),
  bookingOpensAtUtc: '2020-01-01T00:00:00Z',
  state: 'BookingOpen',
  unseen: null,
  wholeDivision: false,
  airports: [airportA],
  routes: [{ id: 5, departure: airportA, arrival: airportC, route: 'XDCT1 XAWY XDCT2', remarks: null }],
  slots: [
    {
      id: 11,
      callsign: 'XSM101',
      flightNumber: null,
      aircraftTypes: ['XA20'],
      departure: airportA,
      arrival: airportC,
      offBlockUtc: at(18),
      onBlockUtc: at(19),
      stand: null,
      rotation: null,
      leg: null,
      isArrival: false,
      taken: false,
    },
  ],
  privateSlots: [{ id: 21, airportIcao: airportA.icao, isArrival: true, timeUtc: at(17), taken: false }],
  cancelledAt: null,
  cancellationNote: null,
  inPerson: false,
  venue: null,
  questions: [],
  atc: null,
} as unknown as PublicEventDto;

const bootstrap = {
  user: null,
  permissions: [],
  division: { name: { en: 'IVAO Example' }, locales: ['en'], defaultLocale: 'en', timezone: 'UTC' },
  calendarKinds: [{ key: 'rfo', label: { en: 'RFO' }, colour: 'purple' }],
};

beforeEach(() => {
  api.get.mockReset();
  api.get.mockImplementation((path: string) =>
    Promise.resolve({
      data: path === '/api/me' ? bootstrap : path === '/api/events/public/{slug}' ? event : null,
      response: new Response(null, { status: 200 }),
    }),
  );
});

/** The page at its address, in a router of its own in memory. */
function draw() {
  const root = createRootRoute();
  const page = createRoute({ getParentRoute: () => root, path: '/events/$slug', component: EventPublicPage });
  const router = createRouter({
    routeTree: root.addChildren([page]),
    history: createMemoryHistory({ initialEntries: ['/events/evt-test-e8b-page'] }),
  });

  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <TooltipProvider>
          <RouterProvider router={router} />
        </TooltipProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

/** Whether `one` comes before `other` in the page. */
function before(one: Element, other: Element): boolean {
  return (one.compareDocumentPosition(other) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0;
}

describe('the page of an event', () => {
  test('the head, then the slots and the private slots, and at the bottom the description and the routes', async () => {
    draw();

    const title = await screen.findByRole('heading', { level: 1, name: 'Smoke evening' });
    const slots = screen.getByRole('region', { name: words.public.slots });
    const privates = screen.getByRole('region', { name: words.private.title });
    const description = screen.getByRole('heading', { level: 2, name: 'About the evening' });
    const routes = screen.getByRole('heading', { level: 2, name: words.public.routes });

    expect(before(title, slots)).toBe(true);
    expect(before(slots, privates)).toBe(true);
    expect(before(privates, description)).toBe(true);
    expect(before(description, routes)).toBe(true);

    // The bookings are said once, in the head, before the slots; the rule of each slot under the heading of the slots.
    const open = screen.getByText(words.public.booking.open);
    expect(before(title, open) && before(open, slots)).toBe(true);
    expect(within(slots).getByText(words.public.booking.untilOffBlock)).toBeInTheDocument();
    expect(screen.getAllByText(words.public.booking.untilOffBlock)).toHaveLength(1);
  });
});
