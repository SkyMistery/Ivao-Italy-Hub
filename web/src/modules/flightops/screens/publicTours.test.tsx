import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import { Route as rootRoute } from '../../../routes/__root';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type { MyToursDto, PublicTourCardDto } from '../api';
import englishFlightOps from '../locales/en/flightops.json';
import { tourListSearchSchema } from '../schemas';

import { PublicToursPage } from './public';

/**
 * `/tours` as design 11 §3 draws it (phase 6): «Your tours» on top for a pilot, the three tabs with the open one in the
 * address, the archive asked of the server only when its tab opens, and the card's line of numbers. The answers are the
 * server's, stubbed.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const words = englishFlightOps.public;

const division: Bootstrap['division'] = {
  code: 'XX',
  name: { en: 'IVAO Example', it: 'IVAO Esempio' },
  locales: ['en', 'it'],
  defaultLocale: 'en',
  timezone: 'UTC',
  dateFormat: 'dd/mm/yyyy',
  logoUrl: null,
  faviconUrl: null,
  firStaffScope: 'all',
  siteDepartment: 'WD',
  contentApproval: ['Page'],
  tagline: {},
  builtBy: [],
};

const bootstrap = (signedIn: boolean): Bootstrap => ({
  user: signedIn
    ? {
        vid: 222222,
        firstName: 'Test',
        lastName: 'Pilot',
        positions: [],
        isStaff: false,
        isSuperadmin: false,
        hasAllDepartments: false,
        locale: 'en',
        departments: [],
        firs: [],
        tokenAudiences: [],
      }
    : null,
  permissions: [],
  division,
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
  commit: null,
});

const card = (id: number, title: string, patch: Partial<PublicTourCardDto> = {}): PublicTourCardDto => ({
  id,
  slug: `tour-${id}`,
  kind: 'Sequential',
  title: { en: title },
  summary: { en: `${title} in a sentence.` },
  coverMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  legs: 3,
  totalNm: 900,
  minPilotRatingName: null,
  award: null,
  subtours: 0,
  ...patch,
});

const current = [
  card(1, 'Alpine loop', { minPilotRatingName: 'PP', award: { id: 4, name: { en: 'Alpine award' } } }),
  card(2, 'Island hopping', { kind: 'Hub' }),
  card(3, 'Grand tour', { kind: 'Container', legs: 0, totalNm: 0, subtours: 4 }),
  card(5, 'Winter lights', { state: 'Upcoming' }),
];

const archive = [card(9, 'Summer of 2025', { state: 'Closed' })];

const myTours: MyToursDto = {
  tours: [
    {
      tourId: 2,
      slug: 'tour-2',
      title: { en: 'Island hopping' },
      parentTourId: null,
      startedAt: '2026-09-02T00:00:00Z',
      completedAt: null,
      done: 1,
      target: 3,
      unit: 'Legs',
      next: { id: 12, number: 2, departureIcao: 'LIRF', arrivalIcao: 'LIEO' },
    },
  ],
  toModify: [],
  answered: [],
  summary: { legsAccepted: 1, minutesFlown: 60, toursCompleted: 0 },
};

afterEach(() => {
  api.get.mockReset();
});

async function open(path: string, signedIn: boolean) {
  api.get.mockImplementation((path: string, options?: { params?: { query?: { states?: string[] } } }) => {
    const data =
      path === '/api/flightops/my-tours'
        ? myTours
        : options?.params?.query?.states?.includes('Closed') === true
          ? archive
          : current;

    return Promise.resolve({ data, response: new Response(null) });
  });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(bootstrapKey, bootstrap(signedIn));

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      createRoute({
        getParentRoute: () => rootRoute,
        path: '/tours',
        validateSearch: tourListSearchSchema,
        component: PublicToursPage,
      }),
    ]),
    history: createMemoryHistory({ initialEntries: [path] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishFlightOps);

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());
  await screen.findByRole('heading', { level: 1, name: words.title });

  return { router };
}

const titles = () =>
  screen.queryAllByRole('article').map((article) => within(article).getByRole('heading').textContent);

describe('the list of the tours', () => {
  test('a visitor finds the open tours in the first tab, each card with its line of numbers', async () => {
    await open('/tours', false);

    await waitFor(() => expect(titles()).toEqual(['Alpine loop', 'Island hopping', 'Grand tour']));
    expect(screen.queryByRole('heading', { name: words.myTours })).toBeNull();

    const alpine = screen.getAllByRole('article')[0]!;
    expect(within(alpine).getByText('3 legs · 900 NM')).toBeInTheDocument();
    expect(within(alpine).getByText('PP')).toBeInTheDocument();
    expect(within(alpine).getByText('Alpine award')).toBeInTheDocument();
    expect(within(screen.getAllByRole('article')[2]!).getByText('4 subtours')).toBeInTheDocument();

    // The archive is asked of the server only when its tab opens.
    const asked = api.get.mock.calls as [
      string,
      { params?: { query?: { states?: string[] } } } | undefined,
    ][];
    expect(asked.some(([, options]) => options?.params?.query?.states !== undefined)).toBe(false);
  });

  test('a pilot finds the tour they are flying on top, and not again in the tab', async () => {
    await open('/tours', true);

    const yours = (await screen.findByRole('heading', { name: words.myTours })).closest('section')!;
    await within(yours).findByText('1 of 3 legs · next: leg 2 LIRF → LIEO');
    expect(within(yours).getByRole('heading', { name: 'Island hopping' })).toBeInTheDocument();
    expect(titles().filter((title) => title === 'Island hopping')).toHaveLength(1);
  });

  test.each([
    ['/tours?tab=upcoming', ['Winter lights']],
    ['/tours?q=alp', ['Alpine loop']],
    ['/tours?kind=Hub', ['Island hopping']],
  ])('the address %s is a view of the list: the tab, the search, the kind', async (path, shown) => {
    await open(path, false);
    await waitFor(() => expect(titles()).toEqual(shown));
  });

  test('the archive holds the closed tours, asked for when its tab opens', async () => {
    const { router } = await open('/tours', false);

    act(() => {
      fireEvent.mouseDown(screen.getByRole('tab', { name: words.listTabs.archive }));
    });
    await waitFor(() => expect(router.state.location.search).toEqual({ tab: 'archive' }));
    await waitFor(() => expect(titles()).toEqual(['Summer of 2025']));
  });
});
