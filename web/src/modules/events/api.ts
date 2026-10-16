import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { blockDataKey, type Body } from '../../blocks';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

import { settingsFromFormValues } from './schemas';
import type {
  ActivityFormValues,
  AirportFormValues,
  AtcPositionFormValues,
  AvailabilityFormValues,
  CancelFormValues,
  DuplicateFormValues,
  EventFormValues,
  EventsSearch,
  EventsSettings,
  KindPreset,
  QuestionFormValues,
  RouteFormValues,
  SettingsFormValues,
  ShiftFormValues,
  SlotFormValues,
  SlotLoadFormValues,
} from './schemas';

/**
 * Every call the screens of the events make (M4): the settings through the core's settings of a module (E2); the events, their
 * airports, their routes and their slots through the CRUD engine, and the endpoints written by hand beside it — cancelling and the
 * presets of the kinds (E3a), publishing (E3b), loading the slots of an event from a table and deleting its free ones (E5),
 * generating its private slots (E7), duplicating it (E8b); and the page of an event, the one read of the site (E4), which lists its
 * public slots (E5) and its private ones (E7). The list of `/events` is the block `events.eventList`, read through the endpoint every
 * block is read through.
 * The bookings (E6b): a pilot's own — read, made, made for a whole rotation, made for a private slot with its flight (E7), withdrawn —
 * through the verbs of E6a and E7, and the staff's list of the bookings of an event with «take away».
 * The events in person (E16): the questions of an event through the CRUD engine, with their sums; a member's registration — read, sent or
 * changed, withdrawn —; and the staff's list of the registrations of an event with «take away».
 * The parallel activities (E17): the activities of an event through the CRUD engine, the staff's list of the seats, and a member's seat
 * taken in a turn and given back.
 * The ATC (E11a): the positions of an event through the CRUD engine, the controllers available on one, the heads of the events with a
 * roster and the two lists of the core the forms choose from; a member's availability — read, given or changed, withdrawn.
 * The roster (E11b): read by position and hour, the one hand written read, and corrected through the CRUD engine — add, move, take away.
 * Its publication (E12): the page of a published roster, a member's own shifts with handing one over, and the staff's requests with their
 * two decisions.
 */

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'events';

const settingsKey = ['events', 'settings'] as const;
const eventsKey = ['events', 'events'] as const;
const airportsKey = ['events', 'airports'] as const;
const routesKey = ['events', 'routes'] as const;
const slotsKey = ['events', 'slots'] as const;
const publicKey = ['events', 'public'] as const;
const mineKey = ['events', 'mine'] as const;
const bookingsKey = ['events', 'bookings'] as const;

export type EventListDto = components['schemas']['EventListDto'];
export type EventDetailDto = components['schemas']['EventDetailDto'];
export type EventDuplicatedDto = components['schemas']['EventDuplicatedDto'];
export type EventAirportDto = components['schemas']['EventAirportDto'];
export type EventRouteDto = components['schemas']['EventRouteDto'];
export type PublicEventDto = components['schemas']['PublicEventDto'];
export type PublicEventRouteDto = components['schemas']['PublicEventRouteDto'];
export type PublicEventAirportDto = components['schemas']['PublicEventAirportDto'];
export type PublicEventSlotDto = components['schemas']['PublicEventSlotDto'];
export type PublicPrivateSlotDto = components['schemas']['PublicPrivateSlotDto'];
export type EventSlotDto = components['schemas']['EventSlotDto'];
export type SlotLoadResultDto = components['schemas']['SlotLoadResultDto'];
export type PrivateSlotsGeneratedDto = components['schemas']['PrivateSlotsGeneratedDto'];
export type MyBookingDto = components['schemas']['MyBookingDto'];
export type RotationBookingDto = components['schemas']['RotationBookingDto'];
export type PrivateBookingRequest = components['schemas']['PrivateBookingRequest'];
export type PrivateBookingDto = components['schemas']['PrivateBookingDto'];
export type EventBookingDto = components['schemas']['EventBookingDto'];
type EventWriteDto = components['schemas']['EventWriteDto'];

export function settingsQuery() {
  return queryOptions({
    queryKey: settingsKey,
    queryFn: async (): Promise<EventsSettings> =>
      unwrap(
        await api.GET('/api/modules/{key}/settings', { params: { path: { key: MODULE_KEY } } }),
      ) as EventsSettings,
  });
}

export function useSaveSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SettingsFormValues): Promise<EventsSettings> =>
      unwrap(
        await api.PUT('/api/modules/{key}/settings', {
          params: { path: { key: MODULE_KEY } },
          body: settingsFromFormValues(values),
        }),
      ) as EventsSettings,
    onSuccess: async (saved) => {
      queryClient.setQueryData(settingsKey, saved);
      // The form of an event presets its switches from these.
      await queryClient.invalidateQueries({ queryKey: kindPresetsQuery().queryKey });
    },
  });
}

// ---- the events (E3a) ------------------------------------------------------------------------------

/** The events of the back office, narrowed to one view of their state when the list asks for one (§7.2). */
export function eventsListQuery(search: EventsSearch) {
  const { view, ...list } = search;

  return queryOptions({
    queryKey: [...eventsKey, 'list', search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/events', {
          params: { query: toQuery(list) },
          querySerializer: listQuerySerializer(view === undefined ? {} : { view }),
        }),
      ),
  });
}

export function eventQuery(id: number) {
  return queryOptions({
    queryKey: [...eventsKey, 'detail', id] as const,
    queryFn: async (): Promise<EventDetailDto> =>
      unwrap(await api.GET('/api/events/events/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** What each kind presets (§1.12), to whoever writes events: the form presets the switches from it when the kind changes. */
export function kindPresetsQuery() {
  return queryOptions({
    queryKey: ['events', 'kind-presets'] as const,
    queryFn: async (): Promise<KindPreset[]> => unwrap(await api.GET('/api/events/kind-presets')),
  });
}

export function eventToFormValues(event: EventDetailDto, locales: readonly string[]): EventFormValues {
  const spread = (value: Record<string, string>) =>
    Object.fromEntries(locales.map((locale) => [locale, value[locale] ?? '']));

  return {
    kind: event.kind,
    publicSlots: event.publicSlots,
    privateSlots: event.privateSlots,
    hasRoster: event.hasRoster,
    ...(event.shiftMinutes === null ? {} : { shiftMinutes: event.shiftMinutes }),
    wholeDivision: event.wholeDivision,
    inPerson: event.inPerson,
    organizer: event.organizer,
    externalUrl: event.externalUrl ?? '',
    title: spread(event.title),
    slug: event.slug,
    summary: spread(event.summary),
    venue: spread(event.venue ?? {}),
    startsAtUtc: event.startsAtUtc,
    endsAtUtc: event.endsAtUtc,
    ...(event.visibleFromUtc === null ? {} : { visibleFromUtc: event.visibleFromUtc }),
    ...(event.bookingOpensAtUtc === null ? {} : { bookingOpensAtUtc: event.bookingOpensAtUtc }),
    // The column takes Public and Members only; the server refuses anything else.
    visibility: event.visibility === 'Members' ? 'Members' : 'Public',
    ...(event.bannerMediaId === null ? {} : { bannerMediaId: event.bannerMediaId }),
    rowVersion: event.rowVersion,
  };
}

/** What the form holds, as the API expects it. The description is sent only by its own tab, and null keeps it as it is. */
function eventBody(values: EventFormValues, body: Body | null): EventWriteDto {
  const text = (value: string | undefined) =>
    value === undefined || value.trim() === '' ? null : value.trim();

  return {
    kind: values.kind,
    publicSlots: values.publicSlots,
    privateSlots: values.privateSlots,
    wholeDivision: values.wholeDivision,
    organizer: values.organizer,
    externalUrl: text(values.externalUrl),
    title: values.title,
    slug: values.slug.trim(),
    summary: values.summary,
    body,
    bannerMediaId: values.bannerMediaId ?? null,
    visibleFromUtc: text(values.visibleFromUtc),
    bookingOpensAtUtc: text(values.bookingOpensAtUtc),
    startsAtUtc: text(values.startsAtUtc),
    endsAtUtc: text(values.endsAtUtc),
    visibility: values.visibility,
    rowVersion: values.rowVersion,
    inPerson: values.inPerson,
    hasRoster: values.hasRoster,
    shiftMinutes: values.shiftMinutes ?? null,
    // A venue written in no language is none, and left out.
    ...(values.venue === undefined || Object.values(values.venue).every((text) => text.trim() === '')
      ? {}
      : { venue: values.venue }),
  };
}

/** Saves an event: its settings, and the description when its tab sends it — one `PUT`, with the row's version. */
export function useSaveEvent(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      values,
      body = null,
    }: {
      values: EventFormValues;
      body?: Body | null;
    }): Promise<EventDetailDto> =>
      id === null
        ? unwrap(await api.POST('/api/events/events', { body: eventBody(values, body) }))
        : unwrap(
            await api.PUT('/api/events/events/{id}', {
              params: { path: { id: String(id) } },
              body: eventBody(values, body),
            }),
          ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(eventQuery(saved.id).queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: [...eventsKey, 'list'] });
    },
  });
}

/**
 * Publishes an event (§2.2, E3b), with the version on screen. A refusal comes back field by field — what it still needs to be
 * published —, and the page lists it.
 */
export function usePublishEvent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (rowVersion: string): Promise<EventDetailDto> =>
      unwrap(
        await api.POST('/api/events/events/{id}/publish', {
          params: { path: { id } },
          body: { rowVersion },
        }),
      ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(eventQuery(saved.id).queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: [...eventsKey, 'list'] });
    },
  });
}

/** Cancels an event, with its note (§2.3). A refusal comes back field by field. */
export function useCancelEvent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: CancelFormValues): Promise<EventDetailDto> =>
      unwrap(
        await api.POST('/api/events/events/{id}/cancel', {
          params: { path: { id } },
          body: { note: values.note, rowVersion: values.rowVersion },
        }),
      ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(eventQuery(saved.id).queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: [...eventsKey, 'list'] });
    },
  });
}

/**
 * Duplicates an event (§2.3-bis, E8b): a new draft from its start and its address, with its public slots when asked. The answer is the
 * new event, which the page then opens; a refusal comes back field by field.
 */
export function useDuplicateEvent(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: DuplicateFormValues): Promise<EventDuplicatedDto> =>
      unwrap(
        await api.POST('/api/events/events/{id}/duplicate', {
          params: { path: { id } },
          body: {
            startsAtUtc:
              values.startsAtUtc === undefined || values.startsAtUtc.trim() === ''
                ? null
                : values.startsAtUtc,
            slug: values.slug.trim(),
            publicSlots: values.publicSlots,
          },
        }),
      ),
    onSuccess: async ({ event: copy }) => {
      queryClient.setQueryData(eventQuery(copy.id).queryKey, copy);
      await queryClient.invalidateQueries({ queryKey: [...eventsKey, 'list'] });
    },
  });
}

export function useDeleteEvent() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/events/{id}', { params: { path: { id: String(id) } } })),
    // Not awaited: the screen that deleted still observes the row it deleted.
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [...eventsKey, 'list'] });
    },
  });
}

// ---- the airports of an event (E3a) ---------------------------------------------------------------

/** Every airport of an event on one page: an event has a handful, never pages of them. */
const allOfAnEvent: ListSearch = listSearchSchema.parse({ pageSize: 100 });

export function airportsQuery(eventId: number, search: ListSearch = allOfAnEvent) {
  return queryOptions({
    queryKey: [...airportsKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/airports', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function airportQuery(id: number) {
  return queryOptions({
    queryKey: [...airportsKey, 'detail', id] as const,
    queryFn: async (): Promise<EventAirportDto> =>
      unwrap(await api.GET('/api/events/airports/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function airportToFormValues(airport: EventAirportDto): AirportFormValues {
  return {
    eventId: airport.eventId,
    icao: airport.icao,
    ordinal: airport.ordinal,
    ...(airport.maxMovementsPerHour === null ? {} : { maxMovementsPerHour: airport.maxMovementsPerHour }),
    ...(airport.maxArrivalsPerHour === null ? {} : { maxArrivalsPerHour: airport.maxArrivalsPerHour }),
    ...(airport.maxDeparturesPerHour === null ? {} : { maxDeparturesPerHour: airport.maxDeparturesPerHour }),
    rowVersion: airport.rowVersion,
  };
}

export function useSaveAirport(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AirportFormValues): Promise<EventAirportDto> => {
      const body = {
        eventId: values.eventId,
        icao: values.icao.trim().toUpperCase(),
        ordinal: values.ordinal,
        maxMovementsPerHour: values.maxMovementsPerHour ?? null,
        maxArrivalsPerHour: values.maxArrivalsPerHour ?? null,
        maxDeparturesPerHour: values.maxDeparturesPerHour ?? null,
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/events/airports', { body }))
        : unwrap(await api.PUT('/api/events/airports/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: airportsKey });
    },
  });
}

export function useDeleteAirport() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/airports/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: airportsKey });
    },
  });
}

// ---- the routes of an event (E4) -------------------------------------------------------------------

/** The routes of an event, in the order they were written: an event has a handful, never pages of them. */
export function routesQuery(eventId: number, search: ListSearch = allOfAnEvent) {
  return queryOptions({
    queryKey: [...routesKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/routes', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function routeQuery(id: number) {
  return queryOptions({
    queryKey: [...routesKey, 'detail', id] as const,
    queryFn: async (): Promise<EventRouteDto> =>
      unwrap(await api.GET('/api/events/routes/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function routeToFormValues(route: EventRouteDto, locales: readonly string[]): RouteFormValues {
  return {
    eventId: route.eventId,
    departureIcao: route.departureIcao,
    arrivalIcao: route.arrivalIcao,
    route: route.route,
    remarks: Object.fromEntries(locales.map((locale) => [locale, route.remarks?.[locale] ?? ''])),
    rowVersion: route.rowVersion,
  };
}

/** Saves a route; remarks written in no language travel as none. The page of the event shows the routes, so it is read again. */
export function useSaveRoute(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: RouteFormValues): Promise<EventRouteDto> => {
      const body = {
        eventId: values.eventId,
        departureIcao: values.departureIcao.trim().toUpperCase(),
        arrivalIcao: values.arrivalIcao.trim().toUpperCase(),
        route: values.route.trim(),
        remarks: Object.values(values.remarks).some((text) => text.trim() !== '') ? values.remarks : null,
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/events/routes', { body }))
        : unwrap(await api.PUT('/api/events/routes/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: routesKey });
      await queryClient.invalidateQueries({ queryKey: publicKey });
    },
  });
}

export function useDeleteRoute() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/routes/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: routesKey });
      void queryClient.invalidateQueries({ queryKey: publicKey });
    },
  });
}

// ---- the slots of an event (E5) --------------------------------------------------------------------

/** The slots of an event, a page of them, by their time at the event's airport unless a column is sorted. */
export function slotsQuery(eventId: number, search: ListSearch) {
  return queryOptions({
    queryKey: [...slotsKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/slots', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function slotQuery(id: number) {
  return queryOptions({
    queryKey: [...slotsKey, 'detail', id] as const,
    queryFn: async (): Promise<EventSlotDto> =>
      unwrap(await api.GET('/api/events/slots/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** A slot in its form: its first aircraft type is the main one, the others written as the table writes them, `A20N/A321`. */
export function slotToFormValues(slot: EventSlotDto): SlotFormValues {
  const [main = '', ...others] = slot.aircraftTypes;

  return {
    eventId: slot.eventId,
    callsign: slot.callsign ?? '',
    flightNumber: slot.flightNumber ?? '',
    mainAircraftType: main,
    otherAircraftTypes: others.join('/'),
    departureIcao: slot.departureIcao ?? '',
    ...(slot.offBlockUtc === null ? {} : { offBlockUtc: slot.offBlockUtc }),
    arrivalIcao: slot.arrivalIcao ?? '',
    ...(slot.onBlockUtc === null ? {} : { onBlockUtc: slot.onBlockUtc }),
    stand: slot.stand ?? '',
    rotationCode: slot.rotationCode ?? '',
    ...(slot.rotationLeg === null ? {} : { rotationLeg: slot.rotationLeg }),
    rowVersion: slot.rowVersion,
  };
}

/** What the slots change shows: their tab, and the page of their event. */
async function slotsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await queryClient.invalidateQueries({ queryKey: slotsKey });
  await queryClient.invalidateQueries({ queryKey: publicKey });
}

/** Saves one slot, the correction of a load or one more; the server reads its airport and its direction off its airports. */
export function useSaveSlot(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SlotFormValues): Promise<EventSlotDto> => {
      const text = (value: string | undefined) =>
        value === undefined || value.trim() === '' ? null : value.trim();
      const body = {
        eventId: values.eventId,
        callsign: values.callsign.trim().toUpperCase(),
        flightNumber: text(values.flightNumber),
        mainAircraftType: values.mainAircraftType.trim().toUpperCase(),
        otherAircraftTypes: text(values.otherAircraftTypes),
        departureIcao: values.departureIcao.trim().toUpperCase(),
        offBlockUtc: text(values.offBlockUtc),
        arrivalIcao: values.arrivalIcao.trim().toUpperCase(),
        onBlockUtc: text(values.onBlockUtc),
        stand: text(values.stand),
        rotationCode: text(values.rotationCode),
        rotationLeg: values.rotationLeg ?? null,
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/events/slots', { body }))
        : unwrap(await api.PUT('/api/events/slots/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: async () => {
      await slotsChanged(queryClient);
    },
  });
}

export function useDeleteSlot() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/slots/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void slotsChanged(queryClient);
    },
  });
}

/**
 * Loads the public slots of an event from a table (§3.1): all or nothing. A refusal comes back row by row, under
 * `rows[12].aircraft_types` — the row as the table numbers it, the column as its header names it —, and the page lists it.
 */
export function useLoadSlots(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SlotLoadFormValues): Promise<SlotLoadResultDto> =>
      unwrap(
        await api.POST('/api/events/events/{id}/slots/load', {
          params: { path: { id: eventId } },
          body: { text: values.text, mode: values.mode },
        }),
      ),
    onSuccess: async () => {
      await slotsChanged(queryClient);
    },
  });
}

/** «Delete the free ones» (§7.2): every slot of the event nobody booked. */
export function useDeleteFreeSlots(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (): Promise<number> =>
      unwrap(
        await api.POST('/api/events/events/{id}/slots/delete-free', { params: { path: { id: eventId } } }),
      ).removed,
    onSuccess: async () => {
      await slotsChanged(queryClient);
    },
  });
}

/**
 * «Generate the private slots» (§3.2, E7): the free private slots of the event replaced by the ones the capacity of its airports
 * leaves room for; the booked ones stay. A refusal — no private slots, no capacity — comes back as one sentence. A refusal reads the
 * slots again too: a 409 says another write changed them in the same moment (the review of #246, point 5).
 */
export function useGeneratePrivateSlots(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (): Promise<PrivateSlotsGeneratedDto> =>
      unwrap(await api.POST('/api/events/events/{id}/slots/generate', { params: { path: { id: eventId } } })),
    onSuccess: async () => {
      await slotsChanged(queryClient);
    },
    onError: () => {
      void slotsChanged(queryClient);
    },
  });
}

// ---- the public side (E4) --------------------------------------------------------------------------

/**
 * The page of an event (§7.1): what whoever is reading may see of it, or null — not seen, or for the members and read by a
 * visitor. The staff of the events read it in every state, and `unseen` tells them why nobody else does.
 */
export function publicEventQuery(slug: string) {
  return queryOptions({
    queryKey: [...publicKey, 'page', slug] as const,
    queryFn: async (): Promise<PublicEventDto | null> => {
      const answer = await api.GET('/api/events/public/{slug}', { params: { path: { slug } } });

      return answer.response.status === 404 ? null : unwrap(answer);
    },
  });
}

// ---- the bookings (E6b) ------------------------------------------------------------------------------

/** The type of the block of a pilot's bookings still to fly, as `MyEventsProvider.BlockType` spells it on the server. */
export const MY_EVENTS_BLOCK = 'events.myEvents';

/**
 * A pilot's own bookings, past ones too (§7.1), by the off block of their flights: what `/events/mine` lists, and what the page of
 * an event reads to say which of its slots are the reader's. A signed in member's only: a visitor has none to ask for.
 */
export function myBookingsQuery() {
  return queryOptions({
    queryKey: [...mineKey, 'bookings'] as const,
    queryFn: async (): Promise<MyBookingDto[]> => unwrap(await api.GET('/api/events/mine/bookings')),
  });
}

/**
 * What a booking changes on screen: the page of its event (taken), the pilot's list, the block of their bookings still to fly, and
 * the staff's list of the event. Not awaited where the screen that changed it still shows the row.
 */
async function bookingsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: publicKey }),
    queryClient.invalidateQueries({ queryKey: mineKey }),
    queryClient.invalidateQueries({ queryKey: [...blockDataKey, MY_EVENTS_BLOCK] }),
    queryClient.invalidateQueries({ queryKey: bookingsKey }),
  ]);
}

/**
 * Books one public slot with the aircraft chosen among those it allows (§3.3). A refusal comes back on `slotId` or `aircraftIcao`
 * (taken a moment ago, closed, too close to another of the pilot's, not open yet); a 409 is «try again», nothing booked. A refusal
 * reads the page again too: it says the page was behind the server — a slot taken a moment ago still read free.
 */
export function useBook() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      slotId,
      aircraftIcao,
    }: {
      slotId: number;
      aircraftIcao: string;
    }): Promise<MyBookingDto> =>
      unwrap(await api.POST('/api/events/mine/bookings', { body: { slotId, aircraftIcao } })),
    onSuccess: async () => {
      await bookingsChanged(queryClient);
    },
    onError: () => {
      void bookingsChanged(queryClient);
    },
  });
}

/**
 * Books the whole rotation of a leg with one aircraft (§3.3): the legs booked, and the ones that were not with why — never a refusal
 * for a leg alone. A 409 is «try again», nothing booked. A refusal reads the page again, as for one slot.
 */
export function useBookRotation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      slotId,
      aircraftIcao,
    }: {
      slotId: number;
      aircraftIcao: string;
    }): Promise<RotationBookingDto> =>
      unwrap(await api.POST('/api/events/mine/bookings/rotation', { body: { slotId, aircraftIcao } })),
    onSuccess: async () => {
      await bookingsChanged(queryClient);
    },
    onError: () => {
      void bookingsChanged(queryClient);
    },
  });
}

/**
 * Books a private slot with the flight the pilot flies (§3.4, E7) — and an arrival's linked departure with it, both or neither. A
 * refusal comes back on its field, the departure's under `departure.…`; a 409 is «try again», nothing booked. A refusal reads the page
 * again, as for a public slot.
 */
export function useBookPrivate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: PrivateBookingRequest): Promise<PrivateBookingDto> =>
      unwrap(await api.POST('/api/events/mine/bookings/private', { body: request })),
    onSuccess: async () => {
      await bookingsChanged(queryClient);
    },
    onError: () => {
      void bookingsChanged(queryClient);
    },
  });
}

/** Withdraws a booking of the pilot's (§3.6), until the off block of its slot: the slot is free again. */
export function useWithdrawBooking() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/mine/bookings/{id}', { params: { path: { id } } })),
    onSuccess: () => {
      void bookingsChanged(queryClient);
    },
  });
}

/** The bookings of an event as its staff read them (§7.2): a page of them, by the time of their flights unless sorted. */
export function staffBookingsQuery(eventId: number, search: ListSearch) {
  return queryOptions({
    queryKey: [...bookingsKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/bookings', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

/** «Take away» (§3.6): a booking of the event, with the reason the pilot reads in the mail. */
export function useRemoveBooking() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, reason }: { id: number; reason: string }): Promise<void> =>
      unwrapEmpty(
        await api.POST('/api/events/bookings/{id}/remove', { params: { path: { id } }, body: { reason } }),
      ),
    onSuccess: () => {
      void bookingsChanged(queryClient);
    },
  });
}

// ---- the events in person (E16) ----------------------------------------------------------------------

export type EventQuestionDto = components['schemas']['EventQuestionDto'];
export type EventRegistrationDto = components['schemas']['EventRegistrationDto'];
export type MyRegistrationDto = components['schemas']['MyRegistrationDto'];
export type PublicQuestionDto = components['schemas']['PublicQuestionDto'];

const questionsKey = ['events', 'questions'] as const;
const registrationsKey = ['events', 'registrations'] as const;

/** The questions of an event in the order of its form, with the sums of what the registrations answered (§4-bis.2). */
export function questionsQuery(eventId: number, search: ListSearch = allOfAnEvent) {
  return queryOptions({
    queryKey: [...questionsKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/questions', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function questionQuery(id: number) {
  return queryOptions({
    queryKey: [...questionsKey, 'detail', id] as const,
    queryFn: async (): Promise<EventQuestionDto> =>
      unwrap(await api.GET('/api/events/questions/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function questionToFormValues(
  question: EventQuestionDto,
  locales: readonly string[],
): QuestionFormValues {
  const spread = (value: Record<string, string>) =>
    Object.fromEntries(locales.map((locale) => [locale, value[locale] ?? '']));

  return {
    eventId: question.eventId,
    ordinal: question.ordinal,
    type: question.type,
    label: spread(question.label),
    choices: question.choices.map((choice) => ({ key: choice.key, label: spread(choice.label) })),
    required: question.required,
    rowVersion: question.rowVersion,
  };
}

/**
 * What changes when a question changes: its list, the page of its event, which builds the member's form from them, and the staff's list
 * of the registrations, whose answers are written in words with them.
 */
async function questionsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: questionsKey }),
    queryClient.invalidateQueries({ queryKey: publicKey }),
    queryClient.invalidateQueries({ queryKey: registrationsKey }),
  ]);
}

/** Saves a question; a new choice travels without a key, and the server gives it one. */
export function useSaveQuestion(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: QuestionFormValues): Promise<EventQuestionDto> => {
      const body = {
        ...values,
        choices: values.choices.map((choice) => ({
          key: choice.key === '' ? null : choice.key,
          label: choice.label,
        })),
      };

      return id === null
        ? unwrap(await api.POST('/api/events/questions', { body }))
        : unwrap(await api.PUT('/api/events/questions/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: () => questionsChanged(queryClient),
  });
}

export function useDeleteQuestion() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/questions/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void questionsChanged(queryClient);
    },
  });
}

/** A question of the staff's list with its sums in words of the language on screen (Carmine's answer 6 on #255). */
export type QuestionRow = EventQuestionDto & { readonly sums: string[] };

/**
 * The questions of an event as the tab «Questions» lists them: each with its sums, which the server sends as numbers and sums writes
 * in the language on screen — part of the key, so another language reads the page again.
 */
export function questionRowsQuery(
  eventId: number,
  search: ListSearch,
  language: string,
  sums: (question: EventQuestionDto) => string[],
) {
  return queryOptions({
    queryKey: [...questionsKey, 'rows', eventId, search, language] as const,
    queryFn: async () => {
      const page = unwrap(
        await api.GET('/api/events/questions', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      );

      return {
        ...page,
        items: page.items.map((question): QuestionRow => ({ ...question, sums: sums(question) })),
      };
    },
  });
}

/** A registration of the staff's list with its answers in words of the language on screen. */
export type RegistrationRow = EventRegistrationDto & { readonly lines: string[] };

/**
 * The registrations of an event as its staff read them (§4-bis.2): a page of them, the first registered first unless sorted, each with
 * its answers, which the server sends as they were given and lines writes in the language on screen with the event's questions.
 */
export function staffRegistrationsQuery(
  eventId: number,
  search: ListSearch,
  language: string,
  lines: (registration: EventRegistrationDto) => string[],
) {
  return queryOptions({
    queryKey: [...registrationsKey, 'list', eventId, search, language] as const,
    queryFn: async () => {
      const page = unwrap(
        await api.GET('/api/events/registrations', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      );

      return {
        ...page,
        items: page.items.map((registration): RegistrationRow => ({
          ...registration,
          lines: lines(registration),
        })),
      };
    },
  });
}

/** What a registration changes on screen: the member's own list, the staff's, and the sums of the questions. */
async function registrationsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...mineKey, 'registrations'] }),
    queryClient.invalidateQueries({ queryKey: registrationsKey }),
    queryClient.invalidateQueries({ queryKey: questionsKey }),
  ]);
}

/** «Take away» a registration (§4-bis.2), with the reason the member reads in the mail. */
export function useRemoveRegistration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, reason }: { id: number; reason: string }): Promise<void> =>
      unwrapEmpty(
        await api.POST('/api/events/registrations/{id}/remove', {
          params: { path: { id } },
          body: { reason },
        }),
      ),
    onSuccess: () => {
      void registrationsChanged(queryClient);
    },
  });
}

/** A member's own registrations, past ones too, by the start of their events: `/events/mine`, and the page of an event. */
export function myRegistrationsQuery() {
  return queryOptions({
    queryKey: [...mineKey, 'registrations'] as const,
    queryFn: async (): Promise<MyRegistrationDto[]> =>
      unwrap(await api.GET('/api/events/mine/registrations')),
  });
}

/** Registers to an event in person, or changes the answers: the answers by question, as the member's form holds them. */
export function useRegister(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (answers: Record<string, unknown>): Promise<MyRegistrationDto> =>
      unwrap(
        await api.PUT('/api/events/mine/registrations/{eventId}', {
          params: { path: { eventId } },
          body: { answers: answers as never },
        }),
      ),
    onSuccess: () => {
      void registrationsChanged(queryClient);
    },
  });
}

/** Withdraws from an event in person, until it starts. */
export function useWithdrawRegistration() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (eventId: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/events/mine/registrations/{eventId}', { params: { path: { eventId } } }),
      ),
    onSuccess: () => {
      void registrationsChanged(queryClient);
    },
  });
}

// ---- the parallel activities (E17) -------------------------------------------------------------------

export type EventActivityDto = components['schemas']['EventActivityDto'];
export type EventSeatDto = components['schemas']['EventSeatDto'];
export type MySeatDto = components['schemas']['MySeatDto'];
export type PublicActivityDto = components['schemas']['PublicActivityDto'];
export type PublicTurnDto = components['schemas']['PublicTurnDto'];

const activitiesKey = ['events', 'activities'] as const;
const seatsKey = ['events', 'seats'] as const;

/** The activities of an event by the hour they start, with the seats taken in each (§4-bis.1). */
export function activitiesQuery(eventId: number, search: ListSearch = allOfAnEvent) {
  return queryOptions({
    queryKey: [...activitiesKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/activities', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function activityQuery(id: number) {
  return queryOptions({
    queryKey: [...activitiesKey, 'detail', id] as const,
    queryFn: async (): Promise<EventActivityDto> =>
      unwrap(await api.GET('/api/events/activities/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function activityToFormValues(
  activity: EventActivityDto,
  locales: readonly string[],
): ActivityFormValues {
  const spread = (value: Record<string, string> | null | undefined) =>
    Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));

  return {
    eventId: activity.eventId,
    name: spread(activity.name),
    place: spread(activity.place),
    fromUtc: activity.fromUtc,
    toUtc: activity.toUtc,
    slotMinutes: activity.slotMinutes,
    seatsPerSlot: activity.seatsPerSlot,
    rowVersion: activity.rowVersion,
  };
}

/** What changes when an activity changes: its list, and the page of its event, which lists its turns. */
async function activitiesChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: activitiesKey }),
    queryClient.invalidateQueries({ queryKey: publicKey }),
    queryClient.invalidateQueries({ queryKey: seatsKey }),
  ]);
}

export function useSaveActivity(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: ActivityFormValues): Promise<EventActivityDto> => {
      // An hour left empty travels as none, and the server says it is required.
      const body = { ...values, fromUtc: values.fromUtc ?? null, toUtc: values.toUtc ?? null };

      return id === null
        ? unwrap(await api.POST('/api/events/activities', { body }))
        : unwrap(
            await api.PUT('/api/events/activities/{id}', { params: { path: { id: String(id) } }, body }),
          );
    },
    onSuccess: () => activitiesChanged(queryClient),
  });
}

export function useDeleteActivity() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/activities/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void activitiesChanged(queryClient);
    },
  });
}

/** The seats of an event as its staff read them (§7.2): a page of them, by the turn unless sorted. */
export function staffSeatsQuery(eventId: number, search: ListSearch) {
  return queryOptions({
    queryKey: [...seatsKey, 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/seats', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

/** What a seat changes on screen: the member's registrations, the page of the event with its free seats, and the staff's lists. */
async function seatsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...mineKey, 'registrations'] }),
    queryClient.invalidateQueries({ queryKey: publicKey }),
    queryClient.invalidateQueries({ queryKey: seatsKey }),
    queryClient.invalidateQueries({ queryKey: activitiesKey }),
  ]);
}

/** Takes a seat in a turn of an activity (§4-bis.2): the activity and the start of the turn. */
export function useTakeSeat() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (seat: { activityId: number; startsAtUtc: string }): Promise<MySeatDto> =>
      unwrap(await api.POST('/api/events/mine/seats', { body: seat })),
    onSettled: () => seatsChanged(queryClient),
  });
}

/** Gives a seat back, until the event starts. */
export function useGiveSeatBack() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/mine/seats/{id}', { params: { path: { id } } })),
    onSettled: () => seatsChanged(queryClient),
  });
}

// ---- the ATC (E11a) ----------------------------------------------------------------------------------

export type EventAtcPositionDto = components['schemas']['EventAtcPositionDto'];
export type AtcCandidateDto = components['schemas']['AtcCandidateDto'];
export type AtcEventDto = components['schemas']['AtcEventDto'];
export type AtcPositionChoiceDto = components['schemas']['AtcPositionChoiceDto'];
export type AtcRatingChoiceDto = components['schemas']['AtcRatingChoiceDto'];
export type MyAvailabilityDto = components['schemas']['MyAvailabilityDto'];
export type PublicEventAtcDto = components['schemas']['PublicEventAtcDto'];

const atcKey = ['events', 'atc'] as const;

/** A page of a list the server reads by hand: its number, its size and the term of its search box. */
function pageOf(search: ListSearch): { page: number; pageSize: number; q?: string } {
  return { page: search.page, pageSize: search.pageSize, ...(search.q === undefined ? {} : { q: search.q }) };
}

/** The heads of the events with a roster still to end, a page of them: the list of the staff of the ATC (§6.3). */
export function atcEventsQuery(search: ListSearch) {
  return queryOptions({
    queryKey: [...atcKey, 'heads', search] as const,
    queryFn: async () =>
      unwrap(await api.GET('/api/events/atc/events', { params: { query: pageOf(search) } })),
  });
}

/** The head of one event with a roster, as the staff of the ATC read it — the staff of a FIR among them, who do not read the event. */
export function atcEventQuery(id: number) {
  return queryOptions({
    queryKey: [...atcKey, 'head', id] as const,
    queryFn: async (): Promise<AtcEventDto> =>
      unwrap(await api.GET('/api/events/atc/events/{id}', { params: { path: { id } } })),
  });
}

/** The positions of an event, by the hour they open unless a column is sorted, with how many controllers are available on each. */
export function atcPositionsQuery(eventId: number, search: ListSearch = allOfAnEvent) {
  return queryOptions({
    queryKey: [...atcKey, 'positions', 'list', eventId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/atc/positions', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      ),
  });
}

export function atcPositionQuery(id: number) {
  return queryOptions({
    queryKey: [...atcKey, 'positions', 'detail', id] as const,
    queryFn: async (): Promise<EventAtcPositionDto> =>
      unwrap(await api.GET('/api/events/atc/positions/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function atcPositionToFormValues(position: EventAtcPositionDto): AtcPositionFormValues {
  return {
    eventId: position.eventId,
    callsign: position.callsign,
    fromUtc: position.fromUtc,
    toUtc: position.toUtc,
    note: position.note ?? '',
    rowVersion: position.rowVersion,
  };
}

/** What a position changes on screen: the positions of its event, and the page of the event, which lists them. */
async function atcPositionsChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...atcKey, 'positions'] }),
    queryClient.invalidateQueries({ queryKey: publicKey }),
  ]);
}

/** Saves a position; empty hours travel as none, and the server gives the event's. */
export function useSaveAtcPosition(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AtcPositionFormValues): Promise<EventAtcPositionDto> => {
      const text = (value: string | undefined) =>
        value === undefined || value.trim() === '' ? null : value.trim();
      const body = {
        eventId: values.eventId,
        callsign: values.callsign,
        fromUtc: text(values.fromUtc),
        toUtc: text(values.toUtc),
        note: text(values.note),
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/events/atc/positions', { body }))
        : unwrap(
            await api.PUT('/api/events/atc/positions/{id}', { params: { path: { id: String(id) } }, body }),
          );
    },
    onSuccess: () => atcPositionsChanged(queryClient),
  });
}

export function useDeleteAtcPosition() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/events/atc/positions/{id}', { params: { path: { id: String(id) } } }),
      ),
    onSuccess: () => {
      void atcPositionsChanged(queryClient);
    },
  });
}

/** The controllers available in some of the hours of a position, the first given first (§4.2). */
export function atcCandidatesQuery(positionId: number, search: ListSearch) {
  return queryOptions({
    queryKey: [...atcKey, 'candidates', positionId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/events/atc/positions/{id}/candidates', {
          params: { path: { id: positionId }, query: pageOf(search) },
        }),
      ),
  });
}

/**
 * The positions of the division the reader may open on an event, which a position is chosen from: the server asks the one handler of
 * each, so the chief of a FIR is offered their FIR's (the review of #256, point 8).
 */
export function divisionPositionsQuery(eventId: number) {
  return queryOptions({
    queryKey: [...atcKey, 'division-positions', eventId] as const,
    queryFn: async (): Promise<AtcPositionChoiceDto[]> =>
      unwrap(await api.GET('/api/events/atc/division-positions', { params: { query: { eventId } } })),
  });
}

/** The ATC ratings of the core's vocabulary, which the least rating of the settings is chosen from. */
export function atcRatingsQuery() {
  return queryOptions({
    queryKey: [...atcKey, 'ratings'] as const,
    queryFn: async (): Promise<AtcRatingChoiceDto[]> => unwrap(await api.GET('/api/events/atc/ratings')),
    staleTime: Infinity,
  });
}

/** A member's own availabilities, past ones too, by the start of their events: the page of an event finds its own there. */
export function myAvailabilityQuery() {
  return queryOptions({
    queryKey: [...mineKey, 'availability'] as const,
    queryFn: async (): Promise<MyAvailabilityDto[]> => unwrap(await api.GET('/api/events/mine/availability')),
  });
}

/** What an availability changes on screen: the member's own list, the candidates of the positions and how many each has. */
async function availabilityChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...mineKey, 'availability'] }),
    queryClient.invalidateQueries({ queryKey: atcKey }),
  ]);
}

/** Gives the member's availability for an event, or changes it: the windows the form holds, and the note. */
export function useGiveAvailability(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AvailabilityFormValues): Promise<MyAvailabilityDto> =>
      unwrap(
        await api.PUT('/api/events/mine/availability/{eventId}', {
          params: { path: { eventId } },
          body: {
            windows: values.windows.map((window) => ({
              fromUtc: window.fromUtc ?? '',
              toUtc: window.toUtc ?? '',
            })),
            note: values.note.trim() === '' ? null : values.note.trim(),
          },
        }),
      ),
    onSuccess: () => {
      void availabilityChanged(queryClient);
    },
  });
}

// ---- the roster (E11b) --------------------------------------------------------------------------------

export type RosterRowDto = components['schemas']['RosterRowDto'];
export type EventAtcShiftDto = components['schemas']['EventAtcShiftDto'];
export type ShiftReason = components['schemas']['ShiftReason'];
export type ShiftWarning = components['schemas']['ShiftWarning'];

/** A row of the roster with its why and its warnings in words of the language on screen, which the page writes (Carmine on #255). */
export type RosterRow = RosterRowDto & { readonly why: string[]; readonly alerts: string[] };

/**
 * The roster of an event as the staff read it (§4.4): its shifts and its uncovered pieces, by position and hour, a page of them; the server
 * sends the why and the warnings as data, and `words` writes them in the language on screen.
 */
export function rosterQuery(
  eventId: number,
  search: ListSearch,
  language: string,
  words: (row: RosterRowDto) => { why: string[]; alerts: string[] },
) {
  return queryOptions({
    queryKey: [...atcKey, 'roster', eventId, search, language] as const,
    queryFn: async () => {
      const page = unwrap(
        await api.GET('/api/events/atc/events/{id}/roster', {
          params: { path: { id: eventId }, query: pageOf(search) },
        }),
      );

      return { ...page, items: page.items.map((row): RosterRow => ({ ...row, ...words(row) })) };
    },
  });
}

export function shiftQuery(id: number) {
  return queryOptions({
    queryKey: [...atcKey, 'shifts', id] as const,
    queryFn: async (): Promise<EventAtcShiftDto> =>
      unwrap(await api.GET('/api/events/atc/shifts/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function shiftToFormValues(shift: EventAtcShiftDto): ShiftFormValues {
  return {
    positionId: String(shift.positionId),
    fromUtc: shift.fromUtc,
    toUtc: shift.toUtc,
    controllerVid: shift.controllerVid,
    rowVersion: shift.rowVersion,
  };
}

/** What a shift changes on screen: the roster, and the shifts read on their own. */
async function rosterChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...atcKey, 'roster'] }),
    queryClient.invalidateQueries({ queryKey: [...atcKey, 'shifts'] }),
  ]);
}

/** «Add» and «Move»: a shift the staff write; what leaves the rules comes back as a warning on the roster. */
export function useSaveShift(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: ShiftFormValues): Promise<EventAtcShiftDto> => {
      const body = {
        positionId: Number(values.positionId),
        controllerVid: values.controllerVid ?? 0,
        fromUtc: values.fromUtc ?? '',
        toUtc: values.toUtc ?? '',
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/events/atc/shifts', { body }))
        : unwrap(
            await api.PUT('/api/events/atc/shifts/{id}', { params: { path: { id: String(id) } }, body }),
          );
    },
    onSuccess: () => rosterChanged(queryClient),
  });
}

/** «Take away»: the piece of the position is uncovered again. */
export function useDeleteShift() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/events/atc/shifts/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void rosterChanged(queryClient);
    },
  });
}

/** Withdraws the member's availability for an event, until the close. */
export function useWithdrawAvailability() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (eventId: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/events/mine/availability/{eventId}', { params: { path: { eventId } } }),
      ),
    onSuccess: () => {
      void availabilityChanged(queryClient);
    },
  });
}

// ---- the publication, the shifts and handing one over (E12) ------------------------------------------

export type PublicRosterDto = components['schemas']['PublicRosterDto'];
export type PublicRosterRowDto = components['schemas']['PublicRosterRowDto'];
export type MyShiftDto = components['schemas']['MyShiftDto'];
export type MyTransferDto = components['schemas']['MyTransferDto'];
export type EventAtcTransferDto = components['schemas']['EventAtcTransferDto'];
export type TransferStatus = components['schemas']['TransferStatus'];

/** The type of the block of the coverage of the rosters, as `AtcCoverageProvider.BlockType` spells it on the server. */
export const ATC_COVERAGE_BLOCK = 'events.atcCoverage';

/** A request of the staff's list with the warnings on whoever would take the shift in words of the language on screen. */
export type TransferRow = EventAtcTransferDto & { readonly alerts: string[] };

/**
 * The roster of an event as its page shows it (§4.4, E12): before its publication only when it will be; after it, the shifts and the
 * uncovered pieces by position and hour — the names only to a signed in reader. Null for an event the reader does not see or with no roster.
 */
export function publicRosterQuery(slug: string) {
  return queryOptions({
    queryKey: [...publicKey, 'roster', slug] as const,
    queryFn: async (): Promise<PublicRosterDto | null> => {
      const answer = await api.GET('/api/events/public/{slug}/roster', { params: { path: { slug } } });

      return answer.response.status === 404 ? null : unwrap(answer);
    },
  });
}

/** A member's own shifts of the published rosters, past ones too, by their start: `/events/mine`. */
export function myShiftsQuery() {
  return queryOptions({
    queryKey: [...mineKey, 'shifts'] as const,
    queryFn: async (): Promise<MyShiftDto[]> => unwrap(await api.GET('/api/events/mine/shifts')),
  });
}

/** What a request changes on screen: the member's shifts, the block of their events, the staff's requests and the roster. */
async function transfersChanged(queryClient: ReturnType<typeof useQueryClient>): Promise<void> {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: [...mineKey, 'shifts'] }),
    queryClient.invalidateQueries({ queryKey: [...blockDataKey, MY_EVENTS_BLOCK] }),
    queryClient.invalidateQueries({ queryKey: [...blockDataKey, ATC_COVERAGE_BLOCK] }),
    queryClient.invalidateQueries({ queryKey: atcKey }),
    queryClient.invalidateQueries({ queryKey: publicKey }),
  ]);
}

/** Asks to hand a shift over (§4.4-bis): to a member named by VID, or to nobody, and why. The shift stays theirs until the staff decide. */
export function useAskTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      shiftId,
      toVid,
      note,
    }: {
      shiftId: number;
      toVid: number | null;
      note: string;
    }): Promise<MyTransferDto> =>
      unwrap(
        await api.POST('/api/events/mine/shifts/{id}/transfer', {
          params: { path: { id: shiftId } },
          body: { toVid, note: note.trim() === '' ? null : note.trim() },
        }),
      ),
    onSuccess: () => {
      void transfersChanged(queryClient);
    },
  });
}

/** Takes back the request waiting for a shift. */
export function useWithdrawTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (shiftId: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/events/mine/shifts/{id}/transfer', { params: { path: { id: shiftId } } }),
      ),
    onSuccess: () => {
      void transfersChanged(queryClient);
    },
  });
}

/**
 * The requests to hand a shift over of an event, as the staff of the ATC read them (§4.4-bis): a page of them, the first asked first; the
 * server sends the warnings on whoever would take the shift as data, and `alerts` writes them in the language on screen.
 */
export function transfersQuery(
  eventId: number,
  search: ListSearch,
  language: string,
  alerts: (row: EventAtcTransferDto) => string[],
) {
  return queryOptions({
    queryKey: [...atcKey, 'transfers', eventId, search, language] as const,
    queryFn: async () => {
      const page = unwrap(
        await api.GET('/api/events/atc/transfers', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ eventId: String(eventId) }),
        }),
      );

      return { ...page, items: page.items.map((row): TransferRow => ({ ...row, alerts: alerts(row) })) };
    },
  });
}

/** «Approve»: the shift passes to the member named, or to the one the staff choose — needed for a request to nobody. */
export function useApproveTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      id,
      toVid,
      note,
    }: {
      id: number;
      toVid: number | null;
      note: string;
    }): Promise<void> =>
      unwrapEmpty(
        await api.POST('/api/events/atc/transfers/{id}/approve', {
          params: { path: { id } },
          body: { toVid, note: note.trim() === '' ? null : note.trim() },
        }),
      ),
    onSuccess: () => {
      void transfersChanged(queryClient);
    },
  });
}

/** «Refuse», with the note whoever asked reads in the mail: the shift stays theirs. */
export function useRefuseTransfer() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, note }: { id: number; note: string }): Promise<void> =>
      unwrapEmpty(
        await api.POST('/api/events/atc/transfers/{id}/refuse', {
          params: { path: { id } },
          body: { note: note.trim() },
        }),
      ),
    onSuccess: () => {
      void transfersChanged(queryClient);
    },
  });
}
