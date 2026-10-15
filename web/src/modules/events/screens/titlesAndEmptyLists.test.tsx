import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRoute, createRouter } from '@tanstack/react-router';
import { act, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, test, vi } from 'vitest';

import englishCommon from '../../../../../locales/en/common.json';
import italianCommon from '../../../../../locales/it/common.json';
import { HubProviders } from '../../../app/Providers';
import { bootstrapKey } from '../../../features/me/queries';
import { Route as rootRoute } from '../../../routes/__root';
import type { Bootstrap } from '../../../shared/api/bootstrap';
import { createTestI18n } from '../../../test/harness';
import type { AtcEventDto, EventDetailDto, PublicEventDto } from '../api';
import englishEvents from '../locales/en/events.json';
import italianEvents from '../locales/it/events.json';

import { AirportsTab } from './airports';
import { AtcPanel } from './atc';
import { BookingsTab } from './bookings';
import { EventsPage } from './events';
import { MyBookingsPage } from './mine';
import { EventPublicPage, EventsPublicPage } from './public';
import { RoutesTab } from './routes';
import { SlotsTab } from './slots';

/**
 * What the events say of themselves with the words the core now takes (#224, E4c): `/events` and a member's `/events/mine` name
 * themselves in the browser tab, as the core's list pages do; the page of an event keeps the tab it had, now that the division's
 * name after its title comes from the root and not from a name the page worked out by hand; and the lists of the events in the back
 * office — the «Slots», «Routes», «Airports» and «Bookings» tabs of an event, and the events themselves —, empty, say what will be
 * there instead of the core's sentence about what a department creates: the same words to whoever writes the rows and to whoever only
 * reads them, who has none of the buttons.
 *
 * ⚠️ The root is the application's own (`__root.tsx`), as in the core's `-titles.test.tsx`: the division's name is said there,
 * once, above every page, and a root of the test's would prove a tree nobody runs.
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
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
  commit: null,
};

/** An event to come, published, with nothing but its words: what is under test on its page is the tab. */
const event: PublicEventDto = {
  id: 7,
  slug: 'evt-test-tab',
  kind: 'rfe',
  organizer: 'Division',
  externalUrl: null,
  title: { en: 'A test event', it: 'Un evento di prova' },
  summary: { en: 'Flown for a test.', it: 'Volato per un test.' },
  body: null,
  bannerMediaId: null,
  startsAtUtc: '2099-11-21T16:00:00Z',
  endsAtUtc: '2099-11-21T22:00:00Z',
  // An event whose bookings open nobody has said when: the page draws no count down (E6b), which this test does not look at.
  bookingOpensAtUtc: null,
  state: 'Announced',
  unseen: null,
  wholeDivision: false,
  airports: [],
  routes: [],
  slots: [],
  // The private slots of the page's read (E7): none, as nothing here looks at them.
  privateSlots: [],
  cancelledAt: null,
  cancellationNote: null,
  inPerson: false,
  venue: null,
  questions: [],
  // The ATC of an event with a roster (E11a): none here.
  atc: null,
};

/** The same event as its tabs in the back office read it: by its id, and with public slots alone unless a test says otherwise. */
const detail = { id: event.id, publicSlots: true, privateSlots: false } as EventDetailDto;

/** The same event as the staff of the ATC read its head (E11a): its roster not proposed yet, so the panel shows its positions alone. */
const atcHead: AtcEventDto = {
  id: event.id,
  ownerDepartment: 'ED',
  slug: event.slug,
  title: event.title,
  state: 'Announced',
  startsAtUtc: event.startsAtUtc,
  endsAtUtc: event.endsAtUtc,
  closesAtUtc: '2099-11-18T16:00:00Z',
  shiftMinutes: 60,
  cancelledAt: null,
  rosterProposedAt: null,
  publishesAtUtc: '2099-11-19T16:00:00Z',
  rosterPublishedAt: null,
};

/** What every list and the block of the events answer: nothing at all. */
const EMPTY = { items: [], page: 1, pageSize: 100, total: 0 };

/**
 * The answers that are not a page of rows: the page of the event, and a member's own bookings (E6b), registrations (E16), shifts and
 * availabilities (E12), plain lists.
 */
const ANSWERS: Readonly<Record<string, unknown>> = {
  '/api/events/public/{slug}': event,
  '/api/events/mine/bookings': [],
  '/api/events/mine/registrations': [],
  '/api/events/mine/shifts': [],
  '/api/events/mine/availability': [],
  '/api/events/atc/events/{id}': atcHead,
};

afterEach(() => {
  api.get.mockReset();
});

/**
 * The application's root with the pages of the events under it, at `path`: `/events`, a member's `/events/mine`, the page of the
 * event, and under a route `/_staff`, whose context is the root's bootstrap as the back office's layout hands it on, its tabs and the
 * list of the events.
 */
async function open(
  path: string,
  {
    editable = false,
    slots = {},
  }: { editable?: boolean; slots?: Partial<Pick<EventDetailDto, 'publicSlots' | 'privateSlots'>> } = {},
) {
  api.get.mockImplementation((route: string) =>
    Promise.resolve({ data: ANSWERS[route] ?? EMPTY, response: new Response(null) }),
  );

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  // The bootstrap the root's `beforeLoad` asks for, already in the cache, as after the first answer.
  queryClient.setQueryData(bootstrapKey, bootstrap);

  const staff = createRoute({ getParentRoute: () => rootRoute, id: '_staff' });

  const router = createRouter({
    routeTree: rootRoute.addChildren([
      createRoute({ getParentRoute: () => rootRoute, path: '/events', component: EventsPublicPage }),
      createRoute({ getParentRoute: () => rootRoute, path: '/events/mine', component: MyBookingsPage }),
      createRoute({ getParentRoute: () => rootRoute, path: '/events/$slug', component: EventPublicPage }),
      staff.addChildren([
        createRoute({
          getParentRoute: () => staff,
          path: '/slots',
          component: () => <SlotsTab event={{ ...detail, ...slots }} editable={editable} />,
        }),
        createRoute({
          getParentRoute: () => staff,
          path: '/routes',
          component: () => <RoutesTab event={detail} editable={editable} />,
        }),
        createRoute({
          getParentRoute: () => staff,
          path: '/airports',
          component: () => <AirportsTab event={detail} editable={editable} />,
        }),
        createRoute({
          getParentRoute: () => staff,
          path: '/bookings',
          component: () => <BookingsTab event={detail} editable={editable} />,
        }),
        createRoute({
          getParentRoute: () => staff,
          path: '/atc',
          component: () => <AtcPanel eventId={event.id} />,
        }),
        createRoute({ getParentRoute: () => staff, path: '/staff/events', component: EventsPage }),
      ]),
    ]),
    history: createMemoryHistory({ initialEntries: [path] }),
    context: { queryClient },
  });

  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'events', englishEvents);
  i18n.addResourceBundle('it', 'events', italianEvents);

  render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );

  await act(() => router.load());

  return { i18n };
}

/** What a `<meta>` of the head of the document says, as a link pasted into a chat reads it. */
function said(selector: string) {
  return document.head.querySelector(selector)?.getAttribute('content');
}

describe('the tab of a page of the events', () => {
  test('/events says its title in the tab, followed by the division, in the language on screen', async () => {
    const { i18n } = await open('/events');

    await waitFor(() => expect(document.title).toBe(`${englishEvents.public.title} — IVAO Example`));
    // Its sentence too, the one under its heading: what a link pasted into a chat shows of it.
    expect(said('meta[name="description"]')).toBe(englishEvents.public.description);

    await act(() => i18n.changeLanguage('it'));
    await waitFor(() => expect(document.title).toBe(`${italianEvents.public.title} — IVAO Esempio`));
  });

  test('the page of an event keeps its tab — its title, then the division — now that the root says the name', async () => {
    const { i18n } = await open(`/events/${event.slug}`);

    await waitFor(() => expect(document.title).toBe('A test event — IVAO Example'));
    expect(said('meta[property="og:site_name"]')).toBe('IVAO Example');
    expect(said('meta[property="og:title"]')).toBe('A test event');

    await act(() => i18n.changeLanguage('it'));
    await waitFor(() => expect(document.title).toBe('Un evento di prova — IVAO Esempio'));
    expect(said('meta[property="og:site_name"]')).toBe('IVAO Esempio');
  });

  test('a member’s /events/mine says its title in the tab, in the language on screen', async () => {
    const { i18n } = await open('/events/mine');

    await waitFor(() => expect(document.title).toBe(`${englishEvents.mine.title} — IVAO Example`));
    expect(said('meta[name="description"]')).toBe(englishEvents.mine.lead);

    await act(() => i18n.changeLanguage('it'));
    await waitFor(() => expect(document.title).toBe(`${italianEvents.mine.title} — IVAO Esempio`));
  });
});

describe('the empty lists of the events', () => {
  test('an empty «Slots» tab says where the slots come from, in both languages, and not the core’s sentence', async () => {
    const { i18n } = await open('/slots', { editable: true });

    // The core's title stays; the sentence under it is the events'.
    expect(await screen.findByText(englishEvents.slots.empty)).toBeInTheDocument();
    expect(screen.getByText(englishCommon.list.empty.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();
    // Whoever writes the slots has the ways the sentence speaks of, above it.
    expect(screen.getByRole('link', { name: englishEvents.slots.load.open })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.slots.create })).toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.slots.empty)).toBeInTheDocument();
    expect(screen.queryByText(italianCommon.list.empty.description)).not.toBeInTheDocument();
  });

  test('whoever only reads the slots, with none of the buttons, reads the same sentence', async () => {
    await open('/slots');

    expect(await screen.findByText(englishEvents.slots.empty)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: englishEvents.slots.load.open })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: englishEvents.slots.create })).not.toBeInTheDocument();
  });

  test('on an event with private slots alone, an empty «Slots» tab says they are generated, in both languages', async () => {
    const { i18n } = await open('/slots', {
      editable: true,
      slots: { publicSlots: false, privateSlots: true },
    });

    expect(await screen.findByText(englishEvents.slots.emptyPrivate)).toBeInTheDocument();
    expect(screen.queryByText(englishEvents.slots.empty)).not.toBeInTheDocument();
    // Whoever writes the slots has the way the sentence speaks of, above it (E7).
    expect(screen.getByRole('button', { name: englishEvents.slots.generate.trigger })).toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.slots.emptyPrivate)).toBeInTheDocument();
  });

  test('on an event with both kinds of slots, an empty «Slots» tab says how each comes, in both languages', async () => {
    const { i18n } = await open('/slots', { slots: { publicSlots: true, privateSlots: true } });

    expect(await screen.findByText(englishEvents.slots.emptyBoth)).toBeInTheDocument();
    expect(screen.queryByText(englishEvents.slots.empty)).not.toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.slots.emptyBoth)).toBeInTheDocument();
  });

  test('an empty «Routes» tab says what its routes will be, in both languages, and not the core’s sentence', async () => {
    const { i18n } = await open('/routes', { editable: true });

    expect(await screen.findByText(englishEvents.routes.empty)).toBeInTheDocument();
    expect(screen.getByText(englishCommon.list.empty.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.routes.create })).toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.routes.empty)).toBeInTheDocument();
    expect(screen.queryByText(italianCommon.list.empty.description)).not.toBeInTheDocument();
  });

  test('whoever only reads the routes, with no button to write one, reads the same sentence', async () => {
    await open('/routes');

    expect(await screen.findByText(englishEvents.routes.empty)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: englishEvents.routes.create })).not.toBeInTheDocument();
  });

  test('an empty «Airports» tab says what will be there, in both languages, and not the core’s sentence', async () => {
    const { i18n } = await open('/airports', { editable: true });

    expect(await screen.findByText(englishEvents.airports.empty)).toBeInTheDocument();
    expect(screen.getByText(englishCommon.list.empty.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: englishEvents.airports.create })).toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.airports.empty)).toBeInTheDocument();
    expect(screen.queryByText(italianCommon.list.empty.description)).not.toBeInTheDocument();
  });

  test('an empty «Bookings» tab says what will be there, in both languages, and not the core’s sentence', async () => {
    const { i18n } = await open('/bookings', { editable: true });

    expect(await screen.findByText(englishEvents.bookings.empty)).toBeInTheDocument();
    expect(screen.getByText(englishCommon.list.empty.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.bookings.empty)).toBeInTheDocument();
    expect(screen.queryByText(italianCommon.list.empty.description)).not.toBeInTheDocument();
  });

  test('whoever only reads the ATC of an event, with no «New position», reads that the positions will show up there (#263)', async () => {
    const { i18n } = await open('/atc');

    expect(await screen.findByText(englishEvents.atc.positions.empty)).toBeInTheDocument();
    expect(screen.getByText(englishEvents.atc.readOnly)).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: englishEvents.atc.positions.create })).not.toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.atc.positions.empty)).toBeInTheDocument();
  });

  test('the events of the back office, none yet, say what will be there, in both languages', async () => {
    const { i18n } = await open('/staff/events');

    expect(await screen.findByText(englishEvents.events.empty)).toBeInTheDocument();
    expect(screen.getByText(englishCommon.list.empty.title)).toBeInTheDocument();
    expect(screen.queryByText(englishCommon.list.empty.description)).not.toBeInTheDocument();

    await act(() => i18n.changeLanguage('it'));
    expect(await screen.findByText(italianEvents.events.empty)).toBeInTheDocument();
    expect(screen.queryByText(italianCommon.list.empty.description)).not.toBeInTheDocument();
  });
});
