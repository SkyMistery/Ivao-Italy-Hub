import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../locales/en/common.json';
import italianCommon from '../../../locales/it/common.json';
import { HubProviders } from '../app/Providers';
import { CORE_BLOCK_TYPES } from '../blocks';
import { PublicCalendarScreen } from '../features/calendar/PublicCalendarScreen';
import { PublicListScreen } from '../features/content/PublicListScreen';
import { bootstrapKey } from '../features/me/queries';
import { SearchResults } from '../features/search/SearchResults';
import type { Bootstrap } from '../shared/api/bootstrap';
import { PageMetadata } from '../shared/seo/PageMetadata';
import { createTestI18n } from '../test/harness';

import { Route as rootRoute } from './__root';

/**
 * What a browser tab says (#224). The list pages and the page that is not there kept the `<title>` of
 * `index.html`, the product's name, while a page of the site said "Page — Division": with a few tabs open,
 * the first ones could not be told apart.
 *
 * ⚠️ The root here is the application's own (`__root.tsx`), not one written for the test: what is under test
 * is where the division's name is said — once, above every page — and the order React gives the titles that
 * follows from it. A root of the test's would prove a tree nobody runs.
 *
 * The file starts with `-` because it lives under `routes/`, like `-routes.test.ts`: the router's plugin
 * ignores what starts with it.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../shared/api/client')),
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

/** The application's root with a handful of pages under it, at `path`. */
async function open(path: string) {
  // The lists ask their block for rows: an empty answer, since what is under test is the tab.
  api.get.mockImplementation(() => Promise.resolve({ data: { items: [] }, response: new Response(null) }));

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // The bootstrap the root's `beforeLoad` asks for, already in the cache, as after the first answer.
  queryClient.setQueryData(bootstrapKey, bootstrap);

  const page = (route: string, component: () => ReactNode) =>
    createRoute({ getParentRoute: () => rootRoute, path: route, component });

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      page('/', () => <p>A page with nothing to say about itself</p>),
      page('/calendar', () => (
        <PublicCalendarScreen filters={{}} onFilter={() => undefined} timezone="UTC" vocabulary={[]} />
      )),
      page('/news', () => (
        <PublicListScreen
          type={CORE_BLOCK_TYPES.newsList}
          titles="news"
          filters={{}}
          onFilter={() => undefined}
          layout={{ layout: 'cards' }}
        />
      )),
      page('/search', () => (
        <SearchResults query="" onQueryChange={() => undefined} answer={undefined} pending={false} />
      )),
      page('/tour', () => (
        // A page written before the root said the division's name: it passes one, and a row's title.
        <PageMetadata title={{ en: 'A tour', it: 'Un tour' }} divisionName="IVAO Example" />
      )),
    ]),
    history: createMemoryHistory({ initialEntries: [path] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());

  return { router, i18n };
}

test('a page that says nothing of itself has the division’s name in its tab, never the product’s', async () => {
  await open('/');

  expect(await screen.findByText('A page with nothing to say about itself')).toBeInTheDocument();
  expect(document.title).toBe('IVAO Example');
});

test('each list page of the core says its own title, followed by the division', async () => {
  const { router } = await open('/calendar');
  await waitFor(() => expect(document.title).toBe(`${englishCommon.calendar.public.title} — IVAO Example`));

  // Through the history rather than `navigate`, whose types are the application's routes and not these.
  act(() => router.history.push('/news'));
  await waitFor(() => expect(document.title).toBe(`${englishCommon.news.public.title} — IVAO Example`));

  act(() => router.history.push('/search'));
  await waitFor(() => expect(document.title).toBe(`${englishCommon.search.title} — IVAO Example`));

  // And leaving a page gives the tab back to the division: the page's title goes with it.
  act(() => router.history.push('/'));
  await waitFor(() => expect(document.title).toBe('IVAO Example'));
});

test('the page that is not there says so in its tab, in the language on screen', async () => {
  const { i18n } = await open('/nowhere/at/all');

  await waitFor(() => expect(document.title).toBe(`${englishCommon.notFound.title} — IVAO Example`));

  await act(() => i18n.changeLanguage('it'));
  await waitFor(() => expect(document.title).toBe(`${italianCommon.notFound.title} — IVAO Esempio`));
});

test('a page that still passes the division’s name and a row’s title reads as before', async () => {
  await open('/tour');

  await waitFor(() => expect(document.title).toBe('A tour — IVAO Example'));
  expect(document.head.querySelector('meta[property="og:site_name"]')).toHaveAttribute(
    'content',
    'IVAO Example',
  );
  expect(document.head.querySelector('meta[property="og:title"]')).toHaveAttribute('content', 'A tour');
});
