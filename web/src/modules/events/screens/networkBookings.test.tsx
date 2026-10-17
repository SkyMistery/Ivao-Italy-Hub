import { QueryClient } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { act, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type { NetworkBookingRow } from '../api';
import englishEvents from '../locales/en/events.json';
import italianEvents from '../locales/it/events.json';

import { NetworkBookingsSection } from './networkBookings';

/**
 * The bookings of the network beside the roster (E15b): the rows in words of the language on screen — what each is for, and whether its booker
 * holds the shift —; «not available» when the server says the network did not answer, never an empty list; and the empty list's own sentence.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const bootstrap = {
  division: { code: 'XX', locales: ['en', 'it'], defaultLocale: 'en', timezone: 'UTC' },
} as Bootstrap;

const rows: NetworkBookingRow[] = [
  {
    callsign: 'XXAA_TWR',
    fromUtc: '2099-11-21T18:00:00Z',
    toUtc: '2099-11-21T20:00:00Z',
    booker: { vid: 761300, name: 'Test Controller' },
    kind: 'Controlling',
    onRoster: true,
  },
  {
    callsign: 'XXAA_APP',
    fromUtc: '2099-11-21T18:30:00Z',
    toUtc: '2099-11-21T20:30:00Z',
    booker: { vid: 761399, name: null },
    kind: 'Exam',
    onRoster: false,
  },
];

afterEach(() => {
  api.get.mockReset();
});

async function open(answer: { items: NetworkBookingRow[]; available: boolean }) {
  api.get.mockResolvedValue({
    data: { ...answer, page: 1, pageSize: 100, total: answer.items.length },
    response: new Response(null),
  });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(bootstrapKey, bootstrap);

  const root = createRootRoute();
  const staff = createRoute({ getParentRoute: () => root, id: '_staff', beforeLoad: () => ({ bootstrap }) });
  const router = createRouter({
    routeTree: root.addChildren([
      staff.addChildren([
        createRoute({
          getParentRoute: () => staff,
          path: '/atc',
          component: () => <NetworkBookingsSection eventId={7} />,
        }),
      ]),
    ]),
    history: createMemoryHistory({ initialEntries: ['/atc'] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'events', englishEvents);
  i18n.addResourceBundle('it', 'events', italianEvents);

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());
  return { i18n };
}

describe('the bookings of the network beside the roster', () => {
  test('each booking says its position, its booker, what it is for and whether the booker holds the shift', async () => {
    await open({ items: rows, available: true });

    const section = await screen.findByRole('region', { name: englishEvents.atc.network.title });
    const tower = (await within(section).findByText('XXAA_TWR')).closest('tr')!;
    expect(within(tower).getByText(englishEvents.atc.network.options.kind.Controlling)).toBeInTheDocument();
    expect(within(tower).getByText(/Test Controller/)).toBeInTheDocument();

    const approach = within(section).getByText('XXAA_APP').closest('tr')!;
    expect(within(approach).getByText(englishEvents.atc.network.options.kind.Exam)).toBeInTheDocument();
    expect(within(approach).getByText(/761399/)).toBeInTheDocument();

    // The server asked once, for the event, with the list's page.
    expect(api.get).toHaveBeenCalledWith(
      '/api/events/atc/events/{id}/network-bookings',
      expect.objectContaining({ params: expect.objectContaining({ path: { id: 7 } }) }),
    );
  });

  test('a network that did not answer is said as such, in both languages, never as an empty list', async () => {
    const { i18n } = await open({ items: [], available: false });

    expect(await screen.findByText(englishEvents.atc.network.unavailable)).toBeInTheDocument();
    expect(screen.queryByText(englishEvents.atc.network.empty)).not.toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.atc.network.unavailable)).toBeInTheDocument();
  });

  test('nobody booked: the list says so with its own sentence', async () => {
    await open({ items: [], available: true });

    expect(await screen.findByText(englishEvents.atc.network.empty)).toBeInTheDocument();
    expect(screen.queryByText(englishEvents.atc.network.unavailable)).not.toBeInTheDocument();
  });
});
