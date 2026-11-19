import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import { Route as rootRoute } from '../../../routes/__root';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import englishCommon from '../../../../../locales/en/common.json';
import englishErrors from '../../../../../locales/en/errors.json';
import type { PublicTourDto, TourDetailDto } from '../api';
import englishFlightOps from '../locales/en/flightops.json';

import { TourPreview } from './TourPreview';

/**
 * The preview of a tour in its editor (design 11 §5): the page of §4 drawn from the server's answer for the draft — the head
 * and the tabs a visitor finds, and nothing of a pilot's — and, while the tour has no address or no dates, the way to the
 * step that writes them instead of a call that would be refused. The map is a stand-in, as on the page's own test.
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

const words = englishFlightOps;

const bootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
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
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
  commit: null,
} as unknown as Bootstrap;

const page: PublicTourDto = {
  id: 7,
  slug: 'alpine-loop',
  kind: 'Sequential',
  title: { en: 'Alpine loop' },
  summary: { en: 'Three legs around the Alps.' },
  briefing: { schemaVersion: 1, sections: [] },
  coverMediaId: null,
  bannerMediaId: null,
  state: 'Draft',
  releaseAt: '2026-12-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  reportWindowDays: 7,
  progression: 'FlyAhead',
  hubRotationOrder: null,
  requiresProcedures: false,
  minPilotRating: null,
  minPilotRatingName: null,
  referenceAircraftIcao: null,
  aircraft: { types: [], groups: [] },
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
  legs: [
    {
      id: 1,
      number: 1,
      kind: 'Normal',
      rotationId: null,
      departureIcao: 'LIRF',
      departureIata: null,
      departureLatitude: 41.8,
      departureLongitude: 12.25,
      arrivalIcao: 'LIMC',
      arrivalIata: null,
      arrivalLatitude: 45.63,
      arrivalLongitude: 8.72,
      distanceNm: 319,
      estimatedMinutes: 65,
      callsigns: [],
      flightNumbers: [],
      aircraft: { types: [], groups: [] },
      releaseAt: null,
      released: false,
    },
  ],
  totalNm: 319,
  totalEstimatedMinutes: 65,
  parent: null,
  subtours: [],
  department: 'FOD',
  award: null,
};

/** The editor's row: only what the preview reads of it. */
const draft = (patch: Partial<TourDetailDto> = {}) =>
  ({
    id: 7,
    slug: 'alpine-loop',
    releaseAt: page.releaseAt,
    closeAt: page.closeAt,
    ...patch,
  }) as TourDetailDto;

afterEach(() => {
  api.get.mockReset();
});

/** The server's answer to the preview: the page, or a refusal with its status. */
async function open(tour: TourDetailDto, refusedWith?: number) {
  api.get.mockImplementation((path: string) =>
    Promise.resolve(
      refusedWith === undefined
        ? {
            data: path === '/api/flightops/tours/{id}/preview' ? page : undefined,
            response: new Response(null),
          }
        : { error: { status: refusedWith }, response: new Response(null, { status: refusedWith }) },
    ),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  queryClient.setQueryData(bootstrapKey, bootstrap);

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      createRoute({
        getParentRoute: () => rootRoute,
        path: '/staff/tours/$id',
        component: () => <TourPreview tour={tour} identityHref="/staff/tours/7?tab=identity" />,
      }),
    ]),
    history: createMemoryHistory({ initialEntries: ['/staff/tours/7?tab=check'] }),
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
}

describe('the preview of a tour', () => {
  test('draws the page of the draft as a visitor finds it, with nothing of a pilot', async () => {
    await open(draft());

    expect(await screen.findByRole('heading', { level: 1, name: 'Alpine loop' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: words.tours.steps.preview })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: words.public.tabs.overview })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: words.public.tabs.route })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: words.public.tabs.rules })).toBeInTheDocument();
    expect(screen.queryByRole('tab', { name: words.public.myReports })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: words.public.reportLeg })).not.toBeInTheDocument();
    expect(api.get).toHaveBeenCalledWith('/api/flightops/tours/{id}/preview', {
      params: { path: { id: 7 } },
    });
  });

  // The master's review of #544: a refusal used to leave "loading" on the screen for ever.
  test("a refusal of the server is said in the core's words, not left loading", async () => {
    await open(draft(), 403);

    expect(await screen.findByText(englishErrors.errors.forbidden.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.common.loading)).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { level: 1 })).not.toBeInTheDocument();
  });

  test('without its dates, sends to the step that writes them and asks the server nothing', async () => {
    await open(draft({ releaseAt: null }));

    expect(await screen.findByText(words.tours.steps.previewNeeds)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Name, dates and pictures/ })).toHaveAttribute(
      'href',
      '/staff/tours/7?tab=identity',
    );
    expect(api.get).not.toHaveBeenCalled();
  });
});
