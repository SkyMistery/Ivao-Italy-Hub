import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { render, screen, waitFor, within } from '@testing-library/react';
import type { ReactNode } from 'react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import englishEvents from '../locales/en/events.json';

import { AtcCoverageBlock, type AtcCoverageEvent } from './atcCoverage';

/**
 * `events.atcCoverage` on `/staff` (E12; design M4 §7.3; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 8): a visitor is
 * asked to sign in; nothing to do says so with the days it looked; an event says where its roster stands and what it needs — the numbers
 * the server sends, in words —, with the way to its ATC.
 */

// The bootstrap of a reader of the events, which says where the ATC of an event lives for them: the tab of its page.
const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

api.get.mockImplementation((path: string) =>
  Promise.resolve({
    data:
      path === '/api/me'
        ? { division: { defaultLocale: 'en', timezone: 'UTC' }, permissions: [{ name: 'Events.View' }] }
        : null,
    response: new Response(null, { status: 200 }),
  }),
);

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const words = englishEvents.blocks.atcCoverage;

function coverage(overrides: Partial<AtcCoverageEvent>): AtcCoverageEvent {
  return {
    id: 4,
    slug: 'evt-test-smoke-cover',
    title: { en: 'Smoke evening', it: 'Sera smoke' },
    startsAtUtc: '2099-11-21T17:00:00Z',
    closesAtUtc: '2099-11-18T17:00:00Z',
    publishesAtUtc: '2099-11-19T17:00:00Z',
    stage: 'Published',
    availabilities: 6,
    pieces: 9,
    uncovered: 2,
    transfers: 1,
    ...overrides,
  };
}

async function draw(ui: ReactNode) {
  const router = createRouter({
    routeTree: createRootRoute({ component: () => ui }),
    history: createMemoryHistory({ initialEntries: ['/staff'] }),
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

describe('the block of the coverage of the rosters', () => {
  test('a visitor is asked to sign in', async () => {
    await draw(<AtcCoverageBlock data={{ signedIn: false }} props={{}} />);

    expect(await screen.findByText(words.signIn)).toBeInTheDocument();
  });

  test('nothing to do says so, with the days it looked', async () => {
    await draw(<AtcCoverageBlock data={{ signedIn: true, days: 14, events: [] }} props={{}} />);

    expect(await screen.findByText('No roster needs anybody in the next 14 days.')).toBeInTheDocument();
  });

  test('an event says where its roster stands and what it needs, with the way to its ATC', async () => {
    await draw(
      <AtcCoverageBlock
        data={{
          signedIn: true,
          days: 14,
          events: [
            coverage({}),
            coverage({ id: 5, title: { en: 'Open evening' }, stage: 'Open', uncovered: 9, transfers: 0 }),
          ],
        }}
        props={{}}
      />,
    );

    const published = (await screen.findByRole('link', { name: 'Smoke evening' })).closest('li')!;
    expect(screen.getByRole('link', { name: 'Smoke evening' })).toHaveAttribute(
      'href',
      '/staff/events/4?tab=atc',
    );
    expect(within(published).getByText(words.stage.Published)).toBeInTheDocument();
    expect(within(published).getByText('2 pieces of 9 uncovered')).toBeInTheDocument();
    expect(within(published).getByText('1 request to hand a shift over')).toBeInTheDocument();

    const open = screen.getByRole('link', { name: 'Open evening' }).closest('li')!;
    expect(within(open).getByText(words.stage.Open)).toBeInTheDocument();
    expect(within(open).getByText(/Availabilities open until .*: 6 given/)).toBeInTheDocument();
    expect(within(open).queryByText(/request/)).not.toBeInTheDocument();
  });
});
