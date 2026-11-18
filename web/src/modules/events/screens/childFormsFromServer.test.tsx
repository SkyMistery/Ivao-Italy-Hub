import { QueryClient } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRouteWithContext,
  createRoute,
  createRouter,
  type RouteComponent,
} from '@tanstack/react-router';
import { act, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import { HubProviders } from '../../../app/Providers';
import type { Bootstrap, BootstrapPermission } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type {
  AwardRuleChoicesDto,
  EventActionsDto,
  EventActivityDto,
  EventAirportDto,
  EventAwardRuleDto,
  EventDetailDto,
  EventQuestionDto,
  EventRouteDto,
  EventSlotDto,
} from '../api';
import englishEvents from '../locales/en/events.json';

import { ActivityForm } from './activities';
import { AirportForm } from './airports';
import { AwardRuleForm } from './awardRules';
import { QuestionForm } from './questions';
import { RouteForm } from './routes';
import { SlotForm, SlotLoadPage } from './slots';

/**
 * The child forms of an event — its routes, airports, slots, questions, activities and rules of award — draw «Save» and «Delete» from
 * the server's answer on the event (issue #444, after #427), never from the bootstrap: a row of an event is judged on the event's care and
 * scope as it is now, so the event's `actions` are the row's. Each test gives the page a bootstrap that says the opposite of the server,
 * so the page is seen to read the server.
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
  fir: null,
  fromOutside: false,
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
    dateFormat: 'dd/mm/yyyy',
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

/** Every write of the events on the whole department: what a bootstrap names, and what the server may still refuse on one event. */
const theDepartment = bootstrapHolding(
  held('Events.View'),
  held('Events.Edit'),
  held('Events.Delete'),
  held('EventRoutes.View'),
  held('EventRoutes.Edit'),
  held('EventBookings.View'),
  held('EventBookings.Edit'),
);

/** Reads the events and writes none: a bootstrap that names no write the server may say yes to. */
const aReader = bootstrapHolding(held('Events.View'), held('EventRoutes.View'), held('EventBookings.View'));

const nothing: EventActionsDto = {
  canEdit: false,
  canDelete: false,
  canPublish: false,
  canCancel: false,
  canDuplicate: false,
  canReadRoutes: true,
  canWriteRoutes: false,
  canReadBookings: true,
  canWriteBookings: false,
  canReadAtc: false,
  canReadReports: false,
};

const everything: EventActionsDto = {
  ...nothing,
  canEdit: true,
  canWriteRoutes: true,
  canWriteBookings: true,
};

const STAMPS = { updatedAt: '2099-01-01T00:00:00Z', rowVersion: '2099-01-01T00:00:00Z' };

const eventWith = (actions: EventActionsDto): EventDetailDto => ({
  id: 7,
  ownerDepartment: 'ED',
  slug: 'evt-test-children',
  kind: 'rfo',
  publicSlots: true,
  privateSlots: false,
  hasRoster: false,
  wholeDivision: false,
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
  ...STAMPS,
  shiftMinutes: null,
  restrictedWindowHours: null,
  restrictedMaxPerWindow: null,
  restrictedMaxPerEvent: null,
  actions,
});

const route: EventRouteDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  departureIcao: 'XXAA',
  arrivalIcao: 'XXBB',
  route: 'DCT',
  remarks: null,
  ...STAMPS,
};

const airport: EventAirportDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  icao: 'XXAA',
  ordinal: 1,
  maxMovementsPerHour: null,
  maxArrivalsPerHour: null,
  maxDeparturesPerHour: null,
  ...STAMPS,
};

const slot: EventSlotDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  kind: 'Public',
  eventAirportIcao: 'XXAA',
  isArrival: false,
  callsign: 'XXX123',
  flightNumber: null,
  aircraftTypes: [],
  departureIcao: 'XXAA',
  arrivalIcao: 'XXBB',
  offBlockUtc: '2099-11-21T17:00:00Z',
  onBlockUtc: null,
  stand: null,
  rotationCode: null,
  rotationLeg: null,
  generated: false,
  ...STAMPS,
};

const question: EventQuestionDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  ordinal: 1,
  type: 'YesNo',
  label: { en: 'Do you come by train?' },
  choices: [],
  required: false,
  sum: { questionId: 70, answered: 0, yes: 0, no: 0, total: 0, choices: {} },
  ...STAMPS,
};

const activity: EventActivityDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  name: { en: 'A tour of the tower' },
  place: null,
  fromUtc: '2099-11-21T16:00:00Z',
  toUtc: '2099-11-21T18:00:00Z',
  slotMinutes: 30,
  seatsPerSlot: 4,
  turns: 4,
  seatsTaken: 0,
  ...STAMPS,
};

const rule: EventAwardRuleDto = {
  id: 70,
  eventId: 7,
  ownerDepartment: 'ED',
  awardId: 1,
  kind: 'Pilot',
  criterion: 'AnyFlight',
  departureIcao: null,
  arrivalIcao: null,
  count: null,
  distanceNm: null,
  minutes: null,
  positionType: null,
  ...STAMPS,
};

const choices: AwardRuleChoicesDto = { awards: [{ id: 1, name: { en: 'A test award' } }], positionTypes: [] };

const EMPTY = { items: [], page: 1, pageSize: 100, total: 0 };

/** Each child form: its address, its component, the title its page shows, and what the server answers besides the event. */
const FORMS: readonly {
  name: string;
  path: string;
  route: string;
  component: RouteComponent;
  title: string;
  answers: Readonly<Record<string, unknown>>;
}[] = [
  {
    name: 'a route',
    path: '/staff/events/7/routes/70',
    route: '/staff/events/$id/routes/$routeId',
    component: RouteForm,
    title: englishEvents.routes.edit,
    answers: { '/api/events/routes/{id}': route },
  },
  {
    name: 'an airport',
    path: '/staff/events/7/airports/70',
    route: '/staff/events/$id/airports/$airportId',
    component: AirportForm,
    title: englishEvents.airports.edit,
    answers: {
      '/api/events/airports/{id}': airport,
      '/api/events/airports': { ...EMPTY, items: [airport], total: 1 },
    },
  },
  {
    name: 'a slot',
    path: '/staff/events/7/slots/70',
    route: '/staff/events/$id/slots/$slotId',
    component: SlotForm,
    title: englishEvents.slots.edit,
    answers: { '/api/events/slots/{id}': slot },
  },
  {
    name: 'a question',
    path: '/staff/events/7/questions/70',
    route: '/staff/events/$id/questions/$questionId',
    component: QuestionForm,
    title: englishEvents.questions.edit,
    answers: { '/api/events/questions/{id}': question },
  },
  {
    name: 'an activity',
    path: '/staff/events/7/activities/70',
    route: '/staff/events/$id/activities/$activityId',
    component: ActivityForm,
    title: englishEvents.activities.edit,
    answers: { '/api/events/activities/{id}': activity },
  },
  {
    name: 'a rule of award',
    path: '/staff/events/7/award-rules/70',
    route: '/staff/events/$id/award-rules/$ruleId',
    component: AwardRuleForm,
    title: englishEvents.awardRules.edit,
    answers: { '/api/events/award-rules/{id}': rule, '/api/events/award-rules/choices': choices },
  },
];

afterEach(() => {
  api.get.mockReset();
});

/** The page at `path`, mounted at `pattern` under a `/_staff` route whose context is the bootstrap, the server answering `answers`. */
async function open(
  path: string,
  pattern: string,
  component: RouteComponent,
  bootstrap: Bootstrap,
  answers: Readonly<Record<string, unknown>>,
) {
  api.get.mockImplementation((route: string) =>
    Promise.resolve({ data: answers[route] ?? EMPTY, response: new Response(null) }),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const root = createRootRouteWithContext<{ queryClient: QueryClient }>()();
  const staff = createRoute({ getParentRoute: () => root, id: '_staff', beforeLoad: () => ({ bootstrap }) });

  const router = createRouter({
    routeTree: root.addChildren([
      staff.addChildren([createRoute({ getParentRoute: () => staff, path: pattern, component })]),
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

describe.each(FORMS)('the form of $name', (form) => {
  test('has no «Save» nor «Delete» when the server says the reader does not write the event, though the bootstrap names the department', async () => {
    await open(form.path, form.route, form.component, theDepartment, {
      ...form.answers,
      '/api/events/events/{id}': eventWith(nothing),
    });

    expect(await screen.findByRole('heading', { name: form.title })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Delete' })).not.toBeInTheDocument();
  });

  test('has «Save» and «Delete» when the server says the reader writes the event, though the bootstrap names no write', async () => {
    await open(form.path, form.route, form.component, aReader, {
      ...form.answers,
      '/api/events/events/{id}': eventWith(everything),
    });

    expect(await screen.findByRole('button', { name: 'Save' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
  });
});

describe('«Paste or load» the slots', () => {
  const path = '/staff/events/7/slots/load';
  const pattern = '/staff/events/$id/slots/load';

  test('loads nothing when the server says the reader does not write the bookings of the event', async () => {
    await open(path, pattern, SlotLoadPage, theDepartment, { '/api/events/events/{id}': eventWith(nothing) });

    expect(await screen.findByRole('heading', { name: englishEvents.slots.load.title })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: englishEvents.slots.load.submit })).not.toBeInTheDocument();
    expect(screen.getByLabelText(englishEvents.slots.load.file)).toBeDisabled();
  });

  test('loads when the server says the reader writes them', async () => {
    await open(path, pattern, SlotLoadPage, aReader, { '/api/events/events/{id}': eventWith(everything) });

    expect(await screen.findByRole('button', { name: englishEvents.slots.load.submit })).toBeInTheDocument();
    expect(screen.getByLabelText(englishEvents.slots.load.file)).toBeEnabled();
  });
});
