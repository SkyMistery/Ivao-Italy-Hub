import { TooltipProvider } from '@ivao/atmosphere-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { MyAvailabilityDto, PublicEventAtcDto, PublicEventDto } from '../api';
import englishEvents from '../locales/en/events.json';

/**
 * The ATC of an event on its page (E11a, design M4 §4.2): the positions to everybody; to a visitor the way to sign in while availabilities
 * are open; to a member below the least rating which rating the event asks; to one who may, the form; to one who gave theirs and can no
 * longer change it, their windows. Against an API that answers what the test says, by address.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const { AvailabilitySection } = await import('./Availability');

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.public.atc;

const at = (hour: number) => `2099-11-21T${String(hour).padStart(2, '0')}:00:00Z`;

const open: PublicEventAtcDto = {
  positions: [{ id: 3, callsign: 'XXAA_TWR', name: 'Smoke Tower', fromUtc: at(17), toUtc: at(21) }],
  closesAtUtc: '2099-11-18T17:00:00Z',
  open: true,
  minimumRating: 'XR3',
  ratedEnough: null,
  rosterPublishesAtUtc: '2099-11-19T17:00:00Z',
  rosterPublished: false,
};

function eventWith(atc: PublicEventAtcDto): PublicEventDto {
  return {
    id: 1,
    slug: 'evt-test-e11a-page',
    title: { en: 'Smoke evening' },
    startsAtUtc: at(17),
    endsAtUtc: at(21),
    atc,
  } as unknown as PublicEventDto;
}

let mine: MyAvailabilityDto[] = [];

beforeEach(() => {
  mine = [];
  api.get.mockReset();
  api.get.mockImplementation((path: string) =>
    Promise.resolve({
      data:
        path === '/api/events/mine/availability'
          ? mine
          : path === '/api/me'
            ? { division: { defaultLocale: 'en', timezone: 'UTC' } }
            : null,
      response: new Response(null, { status: 200 }),
    }),
  );
});

function draw(atc: PublicEventAtcDto, signedIn: boolean) {
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <TooltipProvider>
          <AvailabilitySection event={eventWith(atc)} signedIn={signedIn} />
        </TooltipProvider>
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe('the ATC on the page of an event', () => {
  test('a visitor reads the positions and is asked to sign in while availabilities are open', () => {
    draw(open, false);

    expect(screen.getByRole('heading', { level: 2, name: words.title })).toBeInTheDocument();
    expect(screen.getByText('XXAA_TWR')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: words.signIn })).toBeInTheDocument();
  });

  test('a member below the least rating reads which rating the event asks, and no form', async () => {
    draw({ ...open, ratedEnough: false }, true);

    expect(await screen.findByText(words.ratingTooLow.replace('{{rating}}', 'XR3'))).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: words.give })).not.toBeInTheDocument();
  });

  test('a member who may gives their availability with a form', async () => {
    draw({ ...open, ratedEnough: true }, true);
    expect(await screen.findByRole('button', { name: words.give })).toBeInTheDocument();
  });

  test('once availabilities close a member who gave theirs reads their windows, with nothing to change or withdraw', async () => {
    mine = [
      {
        id: 9,
        eventId: 1,
        eventSlug: 'evt-test-e11a-page',
        eventTitle: { en: 'Smoke evening' },
        windows: [{ fromUtc: at(18), toUtc: at(20) }],
        note: 'with my trainee',
        closesAtUtc: '2099-11-18T17:00:00Z',
        changeable: false,
        withdrawable: false,
        createdAt: '2099-11-01T10:00:00Z',
        updatedAt: '2099-11-01T10:00:00Z',
      },
    ];
    draw({ ...open, open: false, ratedEnough: true }, true);

    expect(await screen.findByText(words.given)).toBeInTheDocument();
    expect(screen.getByText('with my trainee')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: words.save })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: englishEvents.mine.withdraw })).not.toBeInTheDocument();
  });
});
