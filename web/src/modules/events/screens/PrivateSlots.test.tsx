import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { MyBookingDto, PublicEventDto, PublicPrivateSlotDto } from '../api';
import englishEvents from '../locales/en/events.json';

import type { SlotViewer } from './myBookings';

/**
 * The private slots on the page of an event (E7; design M4 §3.4, §7.1), against an API that answers what the test says: the hours of
 * each direction with how many slots are free and the reader's own; an hour opened, the flight a member books through one of its free
 * slots — alone, or an arrival with its departure from the same airport —, a refusal on its field, a 409 as «try again»; and why not,
 * otherwise: a visitor signs in first, the bookings open later, the hour is full.
 */

const api = vi.hoisted(() => ({ post: vi.fn(), get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { POST: api.post, GET: api.get },
}));

const { PrivateSlots } = await import('./PrivateSlots');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.private;

const at = (hour: number, minute = 0) =>
  `2099-11-21T${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}:00Z`;

const airport = { icao: 'XXAA', name: 'Smoke Airport A' };

function slot(id: number, isArrival: boolean, time: string, taken = false): PublicPrivateSlotDto {
  return { id, airportIcao: airport.icao, isArrival, timeUtc: time, taken };
}

/** Two arrivals and a departure from 17:00, an arrival taken at 18:00, two departures later on. */
const slots = [
  slot(11, true, at(17)),
  slot(12, true, at(17, 30), true),
  slot(13, true, at(18), true),
  slot(21, false, at(17)),
  slot(22, false, at(18, 30)),
  slot(23, false, at(18, 45)),
];

function event(overrides: Partial<PublicEventDto> = {}): PublicEventDto {
  return {
    airports: [airport],
    startsAtUtc: at(17),
    endsAtUtc: at(19),
    bookingOpensAtUtc: '2020-01-01T00:00:00Z',
    cancelledAt: null,
    privateSlots: slots,
    ...overrides,
  } as unknown as PublicEventDto;
}

const member: SlotViewer = { signedIn: true, mine: new Map() };

const ok = <T,>(data: T, status = 200) => ({ data, response: new Response(null, { status }) });

const refused = (status: number, problem: Record<string, unknown>) => ({
  error: { status, ...problem },
  response: new Response(null, { status }),
});

/** In a router of its own, in memory: the way to the reader's bookings is a link of the application. */
async function draw(ui: ReactNode) {
  const router = createRouter({
    routeTree: createRootRoute({ component: () => ui }),
    history: createMemoryHistory({ initialEntries: ['/events/evt-test-smoke'] }),
  });

  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider
        client={
          new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
        }
      >
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );

  await waitFor(() => expect(router.state.status).toBe('idle'));
}

/** Opens the hour of the arrivals at 17:00 and waits for its form. */
async function openArrivals(user: ReturnType<typeof userEvent.setup>) {
  const arrivals = await screen.findByRole('table', { name: englishEvents.public.arrivals });
  await user.click(within(arrivals).getByRole('button', { name: '17:00–18:00' }));
  return screen.findByRole('dialog');
}

beforeEach(() => {
  api.post.mockReset();
  api.get.mockReset();
  // The division the page is read in: the form says a time in UTC and where the division lives.
  api.get.mockResolvedValue(
    ok({ division: { locales: ['en', 'it'], defaultLocale: 'en', timezone: 'UTC' } }),
  );
});

describe('the private slots on the page of an event', () => {
  test('each direction hour by hour, with how many slots are free, full ones and the reader own', async () => {
    const mine = new Map([[21, { id: 90, slotId: 21, callsign: 'XSM900' } as unknown as MyBookingDto]]);
    await draw(<PrivateSlots event={event()} viewer={{ signedIn: true, mine }} />);

    const arrivals = await screen.findByRole('table', { name: englishEvents.public.arrivals });
    const rows = within(arrivals).getAllByRole('row').slice(1);
    expect(rows.map((row) => row.textContent)).toEqual([
      `17:00–18:001 of 2${englishEvents.public.free}`,
      `18:00–19:000 of 1${words.full}`,
    ]);

    // The departures first, as for the public slots; the hour where the reader holds one says so.
    const departures = screen.getByRole('table', { name: englishEvents.public.departures });
    expect(within(departures).getAllByRole('row')[1]).toHaveTextContent(englishEvents.public.yours);
  });

  test('a member books an arrival with the flight they fly, at the first free time of the hour', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(ok({ booking: { id: 70, callsign: 'XSM701' }, departure: null }, 201));
    await draw(<PrivateSlots event={event()} viewer={member} />);

    const dialog = await openArrivals(user);
    expect(
      within(dialog).getByText(words.dialog.arrivals.replace('{{airport}}', 'XXAA · Smoke Airport A')),
    ).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(words.arrival.fields.callsign), 'xsm701');
    await user.type(within(dialog).getByLabelText(words.arrival.fields.aircraftIcao), 'XA20');
    await user.type(within(dialog).getByLabelText(words.arrival.fields.otherIcao), 'XXBB');
    fireEvent.change(within(dialog).getByLabelText(words.arrival.fields.otherTimeUtc), {
      target: { value: '2099-11-21T16:00' },
    });
    await user.click(within(dialog).getByRole('button', { name: words.dialog.book }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/api/events/mine/bookings/private', {
        body: {
          slotId: 11,
          aircraftIcao: 'XA20',
          callsign: 'XSM701',
          otherIcao: 'XXBB',
          otherTimeUtc: '2099-11-21T16:00:00Z',
          departure: null,
        },
      }),
    );
    expect(
      await screen.findByText(words.dialog.booked.replace('{{callsign}}', 'XSM701')),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.public.booking.toMine })).toHaveAttribute(
      'href',
      '/events/mine',
    );
  });

  test('an arrival takes its departure from the same airport with it, flown on with its callsign unless changed', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(
      ok({ booking: { id: 70, callsign: 'XSM701' }, departure: { id: 71, callsign: 'XSM701' } }, 201),
    );
    await draw(<PrivateSlots event={event()} viewer={member} />);

    const dialog = await openArrivals(user);
    await user.type(within(dialog).getByLabelText(words.arrival.fields.callsign), 'XSM701');
    await user.type(within(dialog).getByLabelText(words.arrival.fields.aircraftIcao), 'XA20');
    await user.type(within(dialog).getByLabelText(words.arrival.fields.otherIcao), 'XXBB');
    // The form says what it holds a moment after it is written (the generated form's live delay, 150 ms), as a hand takes to reach
    // the switch.
    await new Promise((resolve) => setTimeout(resolve, 250));
    await user.click(within(dialog).getByRole('switch', { name: words.dialog.withDeparture }));

    // What was written stays — the arrival's callsign is the first, the departure's is in its group —; the departure's own flight,
    // its callsign the arrival's.
    expect(within(dialog).getAllByLabelText(words.arrival.fields.callsign)[0]).toHaveValue('XSM701');
    const departure = within(dialog).getByRole('group', { name: words.arrival.fields.departure });
    expect(within(departure).getByLabelText(words.arrival.fields['departure.callsign'])).toHaveValue(
      'XSM701',
    );
    await user.type(within(departure).getByLabelText(words.arrival.fields['departure.otherIcao']), 'XXCC');
    await user.click(within(dialog).getByRole('button', { name: words.dialog.book }));

    // The first departure after the hour began, 18:30: the one at 17:00 is not offered.
    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/api/events/mine/bookings/private', {
        body: {
          slotId: 11,
          aircraftIcao: 'XA20',
          callsign: 'XSM701',
          otherIcao: 'XXBB',
          otherTimeUtc: null,
          departure: { slotId: 22, callsign: 'XSM701', otherIcao: 'XXCC', otherTimeUtc: null },
        },
      }),
    );
    expect(
      await screen.findByText(
        words.dialog.bookedWithDeparture.replace('{{callsign}}', 'XSM701').replace('{{departure}}', 'XSM701'),
      ),
    ).toBeInTheDocument();
  });

  test('a refusal is said on the field it is about, and a 409 is «try again», nothing booked', async () => {
    const user = userEvent.setup();
    api.post
      .mockResolvedValueOnce(
        refused(400, {
          title: 'Some fields',
          errors: { 'departure.slotId': ['events:errors.pairedTooSoon'] },
        }),
      )
      .mockResolvedValueOnce(refused(409, { title: 'events:errors.bookingTryAgain' }));
    await draw(<PrivateSlots event={event()} viewer={member} />);

    const dialog = await openArrivals(user);
    await user.click(within(dialog).getByRole('switch', { name: words.dialog.withDeparture }));
    await user.click(within(dialog).getByRole('button', { name: words.dialog.book }));

    const departure = within(dialog).getByRole('group', { name: words.arrival.fields.departure });
    expect(await within(departure).findByText(englishEvents.errors.pairedTooSoon)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: words.dialog.book }));
    expect(await within(dialog).findByText(englishEvents.errors.bookingTryAgain)).toBeInTheDocument();
    // Not the core's sentence for a 409, which is about somebody else's change to what is being saved.
    expect(within(dialog).queryByText(i18n.t('errors.conflict.title'))).not.toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: words.dialog.book })).toBeEnabled();
  });

  test('otherwise it says why not: a visitor signs in first, the bookings open later, the hour is full', async () => {
    const user = userEvent.setup();
    window.history.pushState({}, '', '/events/evt-test-smoke#private');
    try {
      await draw(<PrivateSlots event={event()} viewer={{ signedIn: false, mine: new Map() }} />);
      const dialog = await openArrivals(user);

      // Back to the page, its hash included.
      expect(within(dialog).getByRole('link', { name: words.dialog.signIn })).toHaveAttribute(
        'href',
        '/auth/login?returnUrl=%2Fevents%2Fevt-test-smoke%23private',
      );
      expect(within(dialog).queryByRole('button', { name: words.dialog.book })).not.toBeInTheDocument();
    } finally {
      window.history.pushState({}, '', '/');
    }
  });

  test('before the opening, and on an hour with nothing left, no form is offered', async () => {
    const user = userEvent.setup();
    await draw(<PrivateSlots event={event({ bookingOpensAtUtc: '2099-11-01T00:00:00Z' })} viewer={member} />);

    const dialog = await openArrivals(user);
    expect(within(dialog).getByText(englishEvents.public.booking.notOpen)).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: words.dialog.book })).not.toBeInTheDocument();
  });

  test('a full hour says so', async () => {
    const user = userEvent.setup();
    await draw(<PrivateSlots event={event()} viewer={member} />);

    const arrivals = await screen.findByRole('table', { name: englishEvents.public.arrivals });
    await user.click(within(arrivals).getByRole('button', { name: '18:00–19:00' }));
    const dialog = await screen.findByRole('dialog');

    expect(within(dialog).getByText(words.dialog.noneFree)).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: words.dialog.book })).not.toBeInTheDocument();
  });
});
