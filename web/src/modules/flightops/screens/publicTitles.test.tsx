import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, render, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import { Route as rootRoute } from '../../../routes/__root';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import englishFlightOps from '../locales/en/flightops.json';
import italianFlightOps from '../locales/it/flightops.json';

import { PublicToursPage } from './public';

/**
 * The public list of the tours names itself in the browser tab (#382), as `/events` and the core's list pages do: its
 * title, then the division, in the language on screen. The page of one tour already did.
 *
 * ⚠️ The root is the application's own (`__root.tsx`), as in the events' `titlesAndEmptyLists.test.tsx`: the division's
 * name is said there, once, above every page.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const bootstrap: Bootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example', it: 'IVAO Esempio' },
    locales: ['en', 'it'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    faviconUrl: null,
    firStaffScope: 'all',
    siteDepartment: 'WD',
    contentApproval: ['Page'],
    tagline: {},
    builtBy: [],
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
  commit: null,
};

afterEach(() => {
  api.get.mockReset();
});

async function open(path: string) {
  // No tour at all: what is under test is the tab, not the cards.
  api.get.mockImplementation(() => Promise.resolve({ data: [], response: new Response(null) }));

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(bootstrapKey, bootstrap);

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      createRoute({ getParentRoute: () => rootRoute, path: '/tours', component: PublicToursPage }),
    ]),
    history: createMemoryHistory({ initialEntries: [path] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishFlightOps);
  i18n.addResourceBundle('it', 'flightops', italianFlightOps);

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());

  return { i18n };
}

describe('the tab of the public list of the tours', () => {
  test('/tours says its title in the tab, followed by the division, in the language on screen', async () => {
    const { i18n } = await open('/tours');

    await waitFor(() => expect(document.title).toBe(`${englishFlightOps.public.title} — IVAO Example`));
    expect(document.head.querySelector('meta[name="description"]')?.getAttribute('content')).toBe(
      englishFlightOps.public.description,
    );

    await act(() => i18n.changeLanguage('it'));
    await waitFor(() => expect(document.title).toBe(`${italianFlightOps.public.title} — IVAO Esempio`));
  });
});
