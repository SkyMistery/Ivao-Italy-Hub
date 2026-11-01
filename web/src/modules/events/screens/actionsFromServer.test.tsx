import { QueryClient } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRouteWithContext,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import type { Bootstrap, BootstrapPermission } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type { AtcEventDto, EventActionsDto, EventAtcPositionDto, EventDetailDto } from '../api';
import englishEvents from '../locales/en/events.json';

import { AtcPanel, AtcPositionForm } from './atc';
import { EventEditor } from './events';

/**
 * What the reader may do on an event and on its positions is the server's answer on the row (issue #427, note
 * 2026-11-01-le-azioni-di-un-evento-dal-server), never the bootstrap's: a grant on one event draws no button on another, the staff of a
 * FIR no «Save» on the positions of another FIR. Each test gives
 * the page a bootstrap that says the opposite of the server, so the page is seen to read the server.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const held = (name: string, resourceScope: string | null = null): BootstrapPermission => ({
  name,
  department: 'ED',
  resourceScope,
});

const bootstrapHolding = (...permissions: BootstrapPermission[]): Bootstrap => ({
  user: null,
  permissions,
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    faviconUrl: null,
    firStaffScope: 'own',
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
});

const nothing: EventActionsDto = {
  canEdit: false,
  canDelete: false,
  canPublish: false,
  canCancel: false,
  canDuplicate: false,
  canReadRoutes: false,
  canWriteRoutes: false,
  canReadBookings: false,
  canWriteBookings: false,
  canReadAtc: false,
  canReadReports: false,
};

/** A draft to come, with a roster and nothing else: what is under test is the bar of its page and its tabs. */
const eventWith = (actions: EventActionsDto): EventDetailDto => ({
  id: 7,
  ownerDepartment: 'ED',
  slug: 'evt-test-actions',
  kind: 'rfo',
  publicSlots: false,
  privateSlots: false,
  hasRoster: true,
  wholeDivision: true,
  inPerson: false,
  organizer: 'Division',
  externalUrl: null,
  title: { en: 'A test event' },
  summary: { en: 'Flown for a test.' },
  venue: null,
  body: { schemaVersion: 1, sections: [] },
  bannerMediaId: null,
  visibleFromUtc: null,
  bookingOpensAtUtc: null,
  startsAtUtc: '2099-11-21T16:00:00Z',
  endsAtUtc: '2099-11-21T22:00:00Z',
  status: 'Draft',
  publishedAt: null,
  visibility: 'Public',
  state: 'Draft',
  cancelledAt: null,
  cancellationNote: null,
  updatedAt: '2099-01-01T00:00:00Z',
  rowVersion: '2099-01-01T00:00:00Z',
  shiftMinutes: null,
  restrictedWindowHours: null,
  restrictedMaxPerWindow: null,
  restrictedMaxPerEvent: null,
  actions,
});

const headWith = (canAddPositions: boolean): AtcEventDto => ({
  id: 7,
  ownerDepartment: 'ED',
  slug: 'evt-test-actions',
  title: { en: 'A test event' },
  state: 'Draft',
  startsAtUtc: '2099-11-21T16:00:00Z',
  endsAtUtc: '2099-11-21T22:00:00Z',
  closesAtUtc: '2099-11-18T16:00:00Z',
  shiftMinutes: 60,
  cancelledAt: null,
  rosterProposedAt: null,
  publishesAtUtc: '2099-11-19T16:00:00Z',
  rosterPublishedAt: null,
  canAddPositions,
});

const positionWith = (editable: boolean): EventAtcPositionDto => ({
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  callsign: 'XXBB_APP',
  name: 'Test Approach',
  fir: 'XXBB',
  fromUtc: '2099-11-21T16:00:00Z',
  toUtc: '2099-11-21T22:00:00Z',
  note: null,
  candidates: 0,
  updatedAt: '2099-01-01T00:00:00Z',
  rowVersion: '2099-01-01T00:00:00Z',
  editable,
});

const EMPTY = { items: [], page: 1, pageSize: 100, total: 0 };

afterEach(() => {
  api.get.mockReset();
});

/** The page at `path`, under a `/_staff` route whose context is the bootstrap, the server answering `answers` and an empty page otherwise. */
async function open(path: string, bootstrap: Bootstrap, answers: Readonly<Record<string, unknown>>) {
  api.get.mockImplementation((route: string) =>
    Promise.resolve({ data: answers[route] ?? EMPTY, response: new Response(null) }),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const root = createRootRouteWithContext<{ queryClient: QueryClient }>()();
  const staff = createRoute({ getParentRoute: () => root, id: '_staff', beforeLoad: () => ({ bootstrap }) });

  const router = createRouter({
    routeTree: root.addChildren([
      staff.addChildren([
        createRoute({ getParentRoute: () => staff, path: '/staff/events/$id', component: EventEditor }),
        createRoute({ getParentRoute: () => staff, path: '/atc', component: () => <AtcPanel eventId={7} /> }),
        createRoute({
          getParentRoute: () => staff,
          path: '/staff/events/atc/$id/positions/$positionId',
          component: AtcPositionForm,
        }),
      ]),
    ]),
    history: createMemoryHistory({ initialEntries: [path] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'events', englishEvents);

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());
}

describe('the page of an event', () => {
  test('a grant on another event draws no button on this one, whatever the bootstrap names', async () => {
    // Events.Edit and Events.Delete on event 42 alone: the bootstrap names the department, the server says no to event 7.
    const bootstrap = bootstrapHolding(
      held('Events.View'),
      held('Events.Edit', 'events:event:42'),
      held('Events.Delete', 'events:event:42'),
    );

    await open('/staff/events/7', bootstrap, { '/api/events/events/{id}': eventWith(nothing) });

    expect(await screen.findByText(englishEvents.events.readOnly)).toBeInTheDocument();
    expect(
      screen.queryByRole('button', { name: englishEvents.events.actions.publish }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: englishEvents.events.actions.cancel })).not.toBeInTheDocument();
    expect(
      screen.queryByRole('link', { name: englishEvents.events.actions.duplicate }),
    ).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
    // Nor the tab of the ATC, which the server does not open to them.
    expect(screen.queryByRole('tab', { name: englishEvents.events.tabs.atc })).not.toBeInTheDocument();
  });

  test('the buttons and the tabs the server says are drawn, though the bootstrap names none of them', async () => {
    const bootstrap = bootstrapHolding(held('Events.View'));
    const actions: EventActionsDto = {
      ...nothing,
      canEdit: true,
      canPublish: true,
      canCancel: true,
      canDuplicate: true,
      canDelete: true,
      canReadAtc: true,
    };

    await open('/staff/events/7', bootstrap, {
      '/api/events/events/{id}': eventWith(actions),
      '/api/events/kind-presets': [],
    });

    expect(
      await screen.findByRole('button', { name: englishEvents.events.actions.publish }),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.events.actions.cancel })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.events.actions.duplicate })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: englishEvents.events.tabs.atc })).toBeInTheDocument();
    expect(screen.queryByText(englishEvents.events.readOnly)).not.toBeInTheDocument();
  });
});

describe('the ATC of an event', () => {
  test('«New position» is the server’s answer on the event', async () => {
    await open('/atc', bootstrapHolding(), { '/api/events/atc/events/{id}': headWith(true) });

    expect(await screen.findAllByRole('link', { name: englishEvents.atc.positions.create })).not.toHaveLength(
      0,
    );
    expect(screen.queryByText(englishEvents.atc.readOnly)).not.toBeInTheDocument();
  });

  test('the chief of a FIR has no «Save» nor «Delete» on a position of another FIR, though the bootstrap names the department', async () => {
    const bootstrap = bootstrapHolding(held('EventAtc.View'), held('EventAtc.Edit'));

    await open('/staff/events/atc/7/positions/70', bootstrap, {
      '/api/events/atc/events/{id}': headWith(true),
      '/api/events/atc/positions/{id}': positionWith(false),
    });

    expect(await screen.findByRole('heading', { name: 'XXBB_APP' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  test('a position the server says the reader writes has «Save» and «Delete»', async () => {
    await open('/staff/events/atc/7/positions/70', bootstrapHolding(held('EventAtc.View')), {
      '/api/events/atc/events/{id}': headWith(false),
      '/api/events/atc/positions/{id}': positionWith(true),
      '/api/events/atc/division-positions': [
        { callsign: 'XXBB_APP', name: 'Test Approach', type: 'APP', fir: 'XXBB' },
      ],
    });

    expect(await screen.findByRole('button', { name: 'Save' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
  });
});
