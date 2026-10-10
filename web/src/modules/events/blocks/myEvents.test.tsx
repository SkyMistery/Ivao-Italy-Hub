import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { render, screen, waitFor, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, test } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { MyBookingDto } from '../api';
import englishEvents from '../locales/en/events.json';

import { MyEventsBlock } from './myEvents';

/**
 * `events.myEvents` on `/me` (E6b; design M4 §7.3): a visitor is asked to sign in; a pilot with nothing to fly is sent to the events;
 * a pilot's flights still to fly are under their events, with the way to all of their bookings.
 */

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.blocks.myEvents;

function booking(id: number, eventId: number, callsign: string): MyBookingDto {
  return {
    id,
    slotId: id,
    eventId,
    eventSlug: `evt-test-smoke-${eventId}`,
    eventTitle: { en: `Smoke event ${eventId}`, it: `Evento smoke ${eventId}` },
    eventState: 'BookingOpen',
    kind: 'Public',
    callsign,
    flightNumber: null,
    aircraftIcao: 'XA20',
    departureIcao: 'XXAA',
    offBlockUtc: '2099-11-21T18:00:00Z',
    arrivalIcao: 'XXCC',
    onBlockUtc: '2099-11-21T19:00:00Z',
    isArrival: false,
    stand: null,
    rotation: null,
    leg: null,
    withdrawable: true,
    createdAt: '2099-11-01T10:00:00Z',
  };
}

async function draw(ui: ReactNode) {
  const router = createRouter({
    routeTree: createRootRoute({ component: () => ui }),
    history: createMemoryHistory({ initialEntries: ['/me'] }),
  });

  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient()}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </I18nextProvider>,
  );

  await waitFor(() => expect(router.state.status).toBe('idle'));
}

describe('the block of a pilot’s bookings', () => {
  test('a visitor is asked to sign in, and shown nothing else', async () => {
    await draw(<MyEventsBlock data={{ signedIn: false }} props={{}} />);

    expect(await screen.findByText(words.signIn)).toBeInTheDocument();
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  test('a pilot with nothing to fly is sent to the events', async () => {
    await draw(<MyEventsBlock data={{ signedIn: true, bookings: [] }} props={{}} />);

    expect(await screen.findByText(words.none, { exact: false })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: words.toEvents })).toHaveAttribute('href', '/events');
  });

  test('the flights still to fly under their events, and the way to all the bookings', async () => {
    await draw(
      <MyEventsBlock
        data={{
          signedIn: true,
          bookings: [booking(1, 10, 'XSM101'), booking(2, 11, 'XSM201'), booking(3, 10, 'XSM102')],
        }}
        props={{}}
      />,
    );

    const first = (await screen.findByRole('link', { name: 'Smoke event 10' })).closest('li')!;
    expect(within(first).getByText('XSM101')).toBeInTheDocument();
    expect(within(first).getByText('XSM102')).toBeInTheDocument();
    expect(within(first).queryByText('XSM201')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Smoke event 10' })).toHaveAttribute(
      'href',
      '/events/evt-test-smoke-10',
    );
    expect(screen.getByRole('link', { name: words.all })).toHaveAttribute('href', '/events/mine');
  });
});
