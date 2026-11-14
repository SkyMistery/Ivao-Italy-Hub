import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import { Route as rootRoute } from '../../../routes/__root';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type { MyTourDto, MyToursDto, PublicLegDto, PublicTourDto } from '../api';
import englishFlightOps from '../locales/en/flightops.json';
import { reportSearchSchema, tourPageSearchSchema } from '../schemas';

import { PublicTourPage } from './public';

/**
 * The page of a tour as design 11 §4 draws it (phase 6): the head with its numbers, «Your tour» with the next leg and the
 * two things to do with it, the reason a pilot may send nothing, and the four tabs with the open one in the address. The
 * answers are the server's, stubbed; what is under test is where the page puts them.
 *
 * The map is MapLibre, which needs a browser with WebGL: here it is a stand-in, and the suite of the real browser
 * (`e2e/tours-map.spec.ts`) draws the real one.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

vi.mock('../../../shared/ui', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/ui')),
  RouteMap: () => <div data-testid="route-map" />,
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

const leg = (id: number, from: string, to: string): PublicLegDto => ({
  id,
  number: id,
  kind: 'Normal',
  rotationId: null,
  departureIcao: from,
  departureIata: null,
  departureLatitude: 41.8,
  departureLongitude: 12.25,
  arrivalIcao: to,
  arrivalIata: null,
  arrivalLatitude: 45.63,
  arrivalLongitude: 8.72,
  distanceNm: 319,
  estimatedMinutes: 65,
  callsigns: ['ITY1234'],
  flightNumbers: [],
  aircraft: { types: [], groups: [] },
  releaseAt: null,
  released: true,
});

const tour: PublicTourDto = {
  id: 7,
  slug: 'alpine-loop',
  kind: 'Sequential',
  title: { en: 'Alpine loop' },
  summary: { en: 'Three legs around the Alps.' },
  briefing: { schemaVersion: 1, sections: [] },
  coverMediaId: null,
  bannerMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  reportWindowDays: 7,
  progression: 'FlyAhead',
  hubRotationOrder: null,
  requiresProcedures: false,
  minPilotRating: 3,
  minPilotRatingName: 'PP',
  referenceAircraftIcao: 'A320',
  aircraft: { types: ['A320'], groups: [] },
  requiredNm: null,
  requiredSubtours: null,
  openGoal: null,
  openGoalParameters: null,
  openGoalValues: [],
  constraints: [],
  callsignRules: [],
  rules: [],
  errors: [],
  rotations: [],
  legs: [leg(1, 'LIRF', 'LIPZ'), leg(2, 'LIPZ', 'LIMC'), leg(3, 'LIMC', 'LIRF')],
  totalNm: 957,
  totalEstimatedMinutes: 195,
  parent: null,
  subtours: [],
  department: 'FOD',
  award: { id: 4, name: { en: 'Alpine award' } },
};

const pilot = (patch: Partial<MyTourDto> = {}): MyTourDto => ({
  tourId: 7,
  legs: [
    { id: 1, progress: 'Done' },
    { id: 2, progress: 'Todo' },
    { id: 3, progress: 'Locked' },
  ],
  flyable: [2],
  next: 2,
  finished: false,
  blocked: null,
  goal: null,
  reports: [],
  ...patch,
});

const myTours: MyToursDto = {
  tours: [
    {
      tourId: 7,
      slug: 'alpine-loop',
      title: { en: 'Alpine loop' },
      parentTourId: null,
      startedAt: '2026-09-02T00:00:00Z',
      completedAt: null,
      done: 1,
      target: 3,
      unit: 'Legs',
      next: { id: 2, number: 2, departureIcao: 'LIPZ', arrivalIcao: 'LIMC' },
    },
  ],
  toModify: [],
  answered: [],
  summary: { legsAccepted: 1, minutesFlown: 60, toursCompleted: 0 },
};

afterEach(() => {
  api.get.mockReset();
});

async function open(path: string, { signedIn, mine }: { signedIn: boolean; mine?: MyTourDto }) {
  const answers: Record<string, unknown> = {
    '/api/flightops/tours/public/{slug}': tour,
    '/api/flightops/tours/{tourId}/reports/mine': mine ?? pilot(),
    '/api/flightops/my-tours': myTours,
  };
  api.get.mockImplementation((path: string) =>
    Promise.resolve({ data: answers[path], response: new Response(null) }),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(bootstrapKey, bootstrap(signedIn));

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      createRoute({
        getParentRoute: () => rootRoute,
        path: '/tours/$slug',
        validateSearch: tourPageSearchSchema,
        component: PublicTourPage,
      }),
      // The page the report links go to, so that the router writes their query as the application does.
      createRoute({
        getParentRoute: () => rootRoute,
        path: '/tours/$slug/report',
        validateSearch: reportSearchSchema,
        component: () => null,
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
  await screen.findByRole('heading', { level: 1, name: 'Alpine loop' });

  return { router };
}

describe('the page of a tour', () => {
  test('a visitor reads the head with its numbers, is asked to sign in, and finds three tabs', async () => {
    await open('/tours/alpine-loop', { signedIn: false });

    for (const value of ['3', '957 NM', '3:15', 'PP', 'A320', 'Alpine award']) {
      expect(screen.getByText(value, { exact: true })).toBeInTheDocument();
    }
    expect(screen.getByRole('link', { name: words.signInToReport })).toHaveAttribute(
      'href',
      `/auth/login?returnUrl=${encodeURIComponent('/tours/alpine-loop')}`,
    );
    expect(screen.getAllByRole('tab').map((tab) => tab.textContent)).toEqual([
      words.tabs.overview,
      words.tabs.route,
      words.tabs.rules,
    ]);
    expect(screen.getByRole('tab', { name: words.tabs.overview })).toHaveAttribute('aria-selected', 'true');
  });

  test('the tab in the address is the one that opens, and one the tour has not got opens the first', async () => {
    await open('/tours/alpine-loop?tab=route', { signedIn: false });

    expect(screen.getByRole('tab', { name: words.tabs.route })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('route-map')).toBeInTheDocument();
    expect(within(screen.getByRole('table')).getAllByRole('row')).toHaveLength(4);
  });

  test('a visitor asking for the reports by address gets the first tab', async () => {
    await open('/tours/alpine-loop?tab=reports', { signedIn: false });

    expect(screen.getByRole('tab', { name: words.tabs.overview })).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByRole('tab', { name: words.myReports })).toBeNull();
  });

  test('a pilot finds the next leg in «Your tour», with the report to send and the flight plan', async () => {
    const { router } = await open('/tours/alpine-loop', { signedIn: true });

    const yours = await screen.findByRole('region', { name: words.pilot });
    await within(yours).findByText('Leg 2 · LIPZ → LIMC');
    expect(within(yours).getByRole('link', { name: words.simbrief })).toHaveAttribute(
      'href',
      expect.stringContaining('orig=LIPZ&dest=LIMC&type=A320'),
    );
    // The bar of the pilot's own progress, from `/my-tours`, the one the cards draw.
    expect(within(yours).getByText('1 of 3 legs')).toBeInTheDocument();

    // «Send the report» goes to the report of that leg.
    act(() => {
      fireEvent.click(within(yours).getByRole('link', { name: words.sendReport }));
    });
    await waitFor(() => expect(router.state.location.pathname).toBe('/tours/alpine-loop/report'));
    expect(router.state.location.search).toEqual({ leg: 2 });
  });

  test('a pilot who may send nothing reads why, and is offered nothing to send', async () => {
    await open('/tours/alpine-loop', {
      signedIn: true,
      mine: pilot({ blocked: 'flightops:errors.reportBanned' }),
    });

    const yours = await screen.findByRole('region', { name: words.pilot });
    await within(yours).findByText(englishFlightOps.errors.reportBanned);
    expect(within(yours).queryByRole('link', { name: words.sendReport })).toBeNull();
  });
});
