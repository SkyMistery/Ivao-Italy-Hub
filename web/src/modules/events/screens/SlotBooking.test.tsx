import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { MyBookingDto, PublicEventSlotDto } from '../api';
import englishEvents from '../locales/en/events.json';

import type { SlotViewer } from './myBookings';

/**
 * «Book» in the dialog of a slot (E6b; design M4 §3.3), against an API that answers what the test says: offered to a signed in
 * member on a free slot while the bookings are open and its off block is to come, with the aircraft chosen among the types it admits
 * — the main one first and chosen —; «Book the whole rotation» on a leg, and what it booked and why not the rest; a refusal said as
 * the server words it, a 409 as «try again», and the page read again after it. Otherwise why not: the slot is the reader's, it
 * closed, the bookings open later, a visitor signs in first and comes back to the page; and nothing on a slot another pilot took.
 */

const api = vi.hoisted(() => ({ post: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { POST: api.post },
}));

const { SlotBooking } = await import('./SlotBooking');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.public.booking;

const NOW = Date.parse('2099-11-21T12:00:00Z');
const OPEN = { bookingOpensAtUtc: '2099-11-20T18:00:00Z', cancelledAt: null };

function slot(overrides: Partial<PublicEventSlotDto> = {}): PublicEventSlotDto {
  return {
    id: 7,
    callsign: 'XSM101',
    flightNumber: null,
    aircraftTypes: ['XA20', 'XA21'],
    departure: { icao: 'XXAA', name: null },
    arrival: { icao: 'XXCC', name: null },
    offBlockUtc: '2099-11-21T18:00:00Z',
    onBlockUtc: '2099-11-21T19:00:00Z',
    stand: null,
    rotation: null,
    leg: null,
    isArrival: false,
    taken: false,
    ...overrides,
  };
}

function booking(aircraftIcao: string): MyBookingDto {
  return { id: 70, slotId: 7, aircraftIcao } as unknown as MyBookingDto;
}

const member: SlotViewer = { signedIn: true, mine: new Map() };

const ok = <T,>(data: T, status = 200) => ({ data, response: new Response(null, { status }) });

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

/** The page of the event as the screen reads it, with the slot in its dialog: what a refusal reads again. */
function PageOfOneSlot({ read }: { read: () => Promise<PublicEventSlotDto> }) {
  const { data } = useQuery({ queryKey: ['events', 'public', 'page', 'evt-test-smoke'], queryFn: read });

  return data === undefined ? null : <SlotBooking slot={data} event={OPEN} viewer={member} nowMs={NOW} />;
}

/** The server's answer to a booking it refuses, as the client receives it. */
const refused = (status: number, problem: Record<string, unknown>) => ({
  error: { status, ...problem },
  response: new Response(null, { status }),
});

beforeEach(() => {
  api.post.mockReset();
});

describe('booking a slot from its dialog', () => {
  test('a member chooses the aircraft, the main one first and chosen, and books the slot', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(ok({ ...booking('XA21') }, 201));
    await draw(<SlotBooking slot={slot()} event={OPEN} viewer={member} nowMs={NOW} />);

    const choice = await screen.findByRole('radiogroup', { name: words.aircraft });
    expect(within(choice).getByRole('radio', { name: /XA20/ })).toBeChecked();
    await user.click(within(choice).getByRole('radio', { name: /XA21/ }));

    await user.click(screen.getByRole('button', { name: words.book.replace('{{aircraft}}', 'XA21') }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/api/events/mine/bookings', {
        body: { slotId: 7, aircraftIcao: 'XA21' },
      }),
    );
    expect(await screen.findByText(words.yours.replace('{{aircraft}}', 'XA21'))).toBeInTheDocument();
    expect(screen.getByRole('link', { name: words.toMine })).toHaveAttribute('href', '/events/mine');
    // A slot alone has no rotation to book.
    expect(screen.queryByRole('button', { name: words.bookRotation })).not.toBeInTheDocument();
  });

  test('a leg books its whole rotation with the aircraft chosen, and says which legs were not booked and why', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(
      ok({
        booked: [{ ...booking('XA20'), slotId: 7, callsign: 'XSM101' }],
        notBooked: [{ slotId: 8, leg: 2, callsign: 'XSM102', reason: 'events:errors.slotJustTaken' }],
      }),
    );
    await draw(
      <SlotBooking slot={slot({ rotation: 'R1', leg: 1 })} event={OPEN} viewer={member} nowMs={NOW} />,
    );

    await user.click(await screen.findByRole('button', { name: words.bookRotation }));

    await waitFor(() =>
      expect(api.post).toHaveBeenCalledWith('/api/events/mine/bookings/rotation', {
        body: { slotId: 7, aircraftIcao: 'XA20' },
      }),
    );
    const result = await screen.findByRole('region', { name: words.rotationResult });
    expect(within(result).getByText('Booked: 1 legs of 2.')).toBeInTheDocument();
    expect(within(result).getByText('XSM101')).toBeInTheDocument();
    expect(within(result).getByText(/Slot just taken by another pilot\./)).toBeInTheDocument();
  });

  test('a refusal is said as the server words it, and nothing is said booked', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue({
      error: { title: 'Some fields', status: 400, errors: { slotId: ['events:errors.slotJustTaken'] } },
      response: new Response(null, { status: 400 }),
    });
    await draw(
      <SlotBooking slot={slot({ aircraftTypes: ['XA20'] })} event={OPEN} viewer={member} nowMs={NOW} />,
    );

    // One type only: nothing to choose.
    expect(screen.queryByRole('radiogroup')).not.toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: words.book.replace('{{aircraft}}', 'XA20') }));

    expect(await screen.findByText('Slot just taken by another pilot.')).toBeInTheDocument();
    expect(screen.queryByText(words.yours.replace('{{aircraft}}', 'XA20'))).not.toBeInTheDocument();
  });

  test('a 409 is «try again» in the words of the events, nothing booked, and «Book» is offered again', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(refused(409, { title: 'events:errors.bookingTryAgain' }));
    await draw(
      <SlotBooking slot={slot({ aircraftTypes: ['XA20'] })} event={OPEN} viewer={member} nowMs={NOW} />,
    );

    const book = await screen.findByRole('button', { name: words.book.replace('{{aircraft}}', 'XA20') });
    await user.click(book);

    expect(await screen.findByText(englishEvents.errors.bookingTryAgain)).toBeInTheDocument();
    // Not the core's sentence for a 409, which is about somebody else's change to what is being saved.
    expect(screen.queryByText(i18n.t('errors.conflict.title'))).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: words.book.replace('{{aircraft}}', 'XA20') })).toBeEnabled();
  });

  test('a refusal reads the page again: the slot taken a moment ago is offered no more, and the refusal stays said', async () => {
    const user = userEvent.setup();
    api.post.mockResolvedValue(
      refused(400, { title: 'Some fields', errors: { slotId: ['events:errors.slotJustTaken'] } }),
    );
    // Free when the page was read, taken by another pilot when it is read again.
    const read = vi
      .fn<() => Promise<PublicEventSlotDto>>()
      .mockResolvedValueOnce(slot({ aircraftTypes: ['XA20'] }))
      .mockResolvedValue(slot({ aircraftTypes: ['XA20'], taken: true }));
    await draw(<PageOfOneSlot read={read} />);

    await user.click(await screen.findByRole('button', { name: words.book.replace('{{aircraft}}', 'XA20') }));

    await waitFor(() => expect(read).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByRole('button')).not.toBeInTheDocument());
    expect(screen.getByText('Slot just taken by another pilot.')).toBeInTheDocument();
  });

  test('a slot of the reader says so, with their aircraft and the way to their bookings', async () => {
    await draw(
      <SlotBooking
        slot={slot({ taken: true })}
        event={OPEN}
        viewer={{ signedIn: true, mine: new Map([[7, booking('XA21')]]) }}
        nowMs={NOW}
      />,
    );

    expect(await screen.findByText(words.yours.replace('{{aircraft}}', 'XA21'))).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  test('a visitor is asked to sign in, and comes back to the page with its filters', async () => {
    // The address of the browser, which the link reads: the path and the query, never the host the server would refuse.
    window.history.pushState({}, '', '/events/evt-test-smoke?direction=arrivals');
    try {
      await draw(
        <SlotBooking slot={slot()} event={OPEN} viewer={{ signedIn: false, mine: new Map() }} nowMs={NOW} />,
      );

      const signIn = await screen.findByRole('link', { name: words.signIn });
      expect(signIn).toHaveAttribute(
        'href',
        '/auth/login?returnUrl=%2Fevents%2Fevt-test-smoke%3Fdirection%3Darrivals',
      );
      expect(screen.queryByRole('button')).not.toBeInTheDocument();
    } finally {
      window.history.pushState({}, '', '/');
    }
  });

  test('nothing is offered on a slot another pilot took, nor on a cancelled event', async () => {
    await draw(
      <>
        <SlotBooking slot={slot({ taken: true })} event={OPEN} viewer={member} nowMs={NOW} />
        <SlotBooking
          slot={slot()}
          event={{ ...OPEN, cancelledAt: '2099-11-20T09:00:00Z' }}
          viewer={member}
          nowMs={NOW}
        />
      </>,
    );

    await waitFor(() => expect(screen.queryByRole('button')).not.toBeInTheDocument());
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  test('before the bookings open, and once the off block has passed, it says why not', async () => {
    await draw(
      <>
        <SlotBooking
          slot={slot()}
          event={{ bookingOpensAtUtc: '2099-11-21T13:00:00Z', cancelledAt: null }}
          viewer={member}
          nowMs={NOW}
        />
        <SlotBooking
          slot={slot({ offBlockUtc: '2099-11-21T12:00:00Z' })}
          event={OPEN}
          viewer={member}
          nowMs={NOW}
        />
      </>,
    );

    expect(await screen.findByText(words.notOpen)).toBeInTheDocument();
    expect(screen.getByText(englishEvents.errors.slotClosed)).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
