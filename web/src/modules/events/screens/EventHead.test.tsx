import { TooltipProvider } from '@ivao/atmosphere-react';
import { act, fireEvent, screen, waitFor, within } from '@testing-library/react';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { createTestI18n, renderWithProviders } from '../../../test/harness';
import type { PublicEventDto } from '../api';
import englishEvents from '../locales/en/events.json';

/**
 * The head of the page of an event as dalberone drew it (E8b; note 2026-10-11-la-testa-della-pagina-di-un-evento, decided by Carmine on
 * #249): under the title and the summary, when it runs in UTC and where the division lives; then its bookings as a badge — open, from
 * when with the count, or closed —, its airports by their codes, each name read without a mouse, and who organises it only when it is
 * not the division. The words are the module's own file.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const { EventHead } = await import('./EventHead');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.public;

const airportA = { icao: 'XXAA', name: 'Smoke Airport A' };
const airportB = { icao: 'XXBB', name: null };

/** An evening at two airports, organised by the division, whose bookings opened long ago. */
function event(overrides: Partial<PublicEventDto> = {}): PublicEventDto {
  return {
    id: 1,
    slug: 'evt-test-e8b-head',
    kind: 'rfo',
    organizer: 'Division',
    externalUrl: null,
    title: { en: 'Smoke evening' },
    summary: { en: 'Public and private slots.' },
    body: null,
    bannerMediaId: null,
    startsAtUtc: '2099-11-21T17:00:00Z',
    endsAtUtc: '2099-11-21T21:00:00Z',
    bookingOpensAtUtc: '2020-01-01T00:00:00Z',
    state: 'BookingOpen',
    unseen: null,
    wholeDivision: false,
    airports: [airportA, airportB],
    routes: [],
    slots: [],
    privateSlots: [],
    cancelledAt: null,
    cancellationNote: null,
    inPerson: false,
    venue: null,
    questions: [],
    atc: null,
    activities: [],
    ...overrides,
  };
}

/** The division the page is read in, which lives an hour ahead of UTC in November, and the calendar's word for the kind. */
const bootstrap = {
  division: { locales: ['en'], defaultLocale: 'en', timezone: 'Europe/Rome' },
  calendarKinds: [{ key: 'rfo', label: { en: 'RFO' }, colour: 'purple' }],
};

beforeEach(() => {
  api.get.mockReset();
  api.get.mockResolvedValue({ data: bootstrap, response: new Response(null, { status: 200 }) });
});

/** Draws the head, under the tooltips' provider the application mounts, and waits for the calendar's word for its kind. */
async function draw(one: PublicEventDto) {
  renderWithProviders(
    <TooltipProvider>
      <EventHead event={one} timezone="Europe/Rome" onOpen={() => {}} />
    </TooltipProvider>,
    { i18n },
  );
  await screen.findByText('RFO');
}

/** The second line of the head: the bookings, the airports and who organises it. */
function factsLine(): HTMLElement {
  return screen
    .getByText(/17:00–21:00Z$/)
    .closest('div')!
    .querySelectorAll('p')[1]!;
}

describe('the head of the page of an event', () => {
  test('when in UTC and where the division lives, the bookings open, the airports by their codes; the division goes unsaid', async () => {
    await draw(event());

    expect(screen.getByRole('heading', { level: 1, name: 'Smoke evening' })).toBeInTheDocument();
    expect(screen.getByText('Nov 21, 2099 · 17:00–21:00Z')).toBeInTheDocument();
    expect(screen.getByText('(18:00–22:00 LT)')).toBeInTheDocument();

    const line = factsLine();
    expect(within(line).getByText(words.booking.open)).toBeInTheDocument();
    // A code the hub knows is a button named by its code and its name; one it no longer knows is the code alone.
    expect(within(line).getByRole('button', { name: 'XXAA · Smoke Airport A' })).toHaveTextContent(/^XXAA$/);
    expect(within(line).queryByRole('button', { name: /XXBB/ })).not.toBeInTheDocument();
    // Nothing else: the division organising goes without saying on its own site.
    expect(line).toHaveTextContent(new RegExp(`^${words.booking.open}·XXAA·XXBB$`));
    expect(within(line).queryByRole('link')).not.toBeInTheDocument();
  });

  test("an airport's name is read without a mouse: on the keyboard's focus, and on a tap that the next one closes", async () => {
    await draw(event());
    const code = within(factsLine()).getByRole('button', { name: 'XXAA · Smoke Airport A' });

    act(() => code.focus());
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Smoke Airport A');
    act(() => code.blur());
    await waitFor(() => expect(screen.queryByRole('tooltip')).not.toBeInTheDocument());

    fireEvent.click(code);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Smoke Airport A');
    fireEvent.click(code);
    await waitFor(() => expect(screen.queryByRole('tooltip')).not.toBeInTheDocument());
  });

  test('another division, or the network, is said in the same line, with their page when there is one', async () => {
    await draw(event({ organizer: 'OtherDivision', externalUrl: 'https://example.org/the-event' }));

    const line = factsLine();
    expect(within(line).getByText(words.organizedBy.OtherDivision)).toBeInTheDocument();
    expect(within(line).getByRole('link', { name: words.organizerPage })).toHaveAttribute(
      'href',
      'https://example.org/the-event',
    );
  });

  test('the network with the division, without a page of theirs', async () => {
    await draw(event({ organizer: 'Network' }));

    const line = factsLine();
    expect(within(line).getByText(words.organizedBy.Network)).toBeInTheDocument();
    expect(within(line).queryByRole('link')).not.toBeInTheDocument();
  });

  test('until the bookings open, the moment they do in UTC and where the division lives, and the count', async () => {
    await draw(event({ bookingOpensAtUtc: '2099-11-14T18:00:00Z', state: 'Announced' }));

    const line = factsLine();
    expect(within(line).getByText('Bookings from Nov 14, 2099, 18:00Z (19:00 LT)')).toBeInTheDocument();
    expect(within(line).getByRole('timer')).toHaveTextContent(/^In \d+ d \d{2}:\d{2}:\d{2}$/);
  });

  test('closed on a cancelled event, with no count', async () => {
    await draw(
      event({
        bookingOpensAtUtc: '2099-11-14T18:00:00Z',
        cancelledAt: '2099-11-01T09:00:00Z',
        state: 'Cancelled',
      }),
    );

    const line = factsLine();
    expect(within(line).getByText(words.booking.closed)).toBeInTheDocument();
    expect(within(line).queryByRole('timer')).not.toBeInTheDocument();
  });

  test('an online day of the whole division, which takes no booking, says only that', async () => {
    await draw(event({ bookingOpensAtUtc: null, wholeDivision: true, airports: [] }));

    // No badge, and no dot before the one fact left.
    expect(factsLine()).toHaveTextContent(/^Every airport of the division$/);
  });
});
