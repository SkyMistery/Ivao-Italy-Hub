import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { blockDataKey, type Body } from '../../blocks';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

import type {
  AirportFormValues,
  CancelFormValues,
  EventFormValues,
  EventsSearch,
  EventsSettings,
  KindPreset,
  RouteFormValues,
  SettingsFormValues,
  SlotFormValues,
  SlotLoadFormValues,
} from './schemas';

/**
 * Every call the screens of the events make (M4): the settings through the core's settings of a module (E2); the events, their
 * airports, their routes and their slots through the CRUD engine, and the endpoints written by hand beside it — cancelling and the
 * presets of the kinds (E3a), publishing (E3b), loading the slots of an event from a table and deleting its free ones (E5),
 * generating its private slots (E7); and the page of an event, the one read of the site (E4), which lists its public slots (E5) and
 * its private ones (E7). The list of `/events` is the block `events.eventList`, read through the endpoint every block is read through.
 * The bookings (E6b): a pilot's own — read, made, made for a whole rotation, made for a private slot with its flight (E7), withdrawn —
 * through the verbs of E6a and E7, and the staff's list of the bookings of an event with «take away».
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
          body: values,
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
    wholeDivision: event.wholeDivision,
    organizer: event.organizer,
    externalUrl: event.externalUrl ?? '',
    title: spread(event.title),
    slug: event.slug,
    summary: spread(event.summary),
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
 * leaves room for; the booked ones stay. A refusal — no private slots, no capacity — comes back as one sentence.
 */
export function useGeneratePrivateSlots(eventId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (): Promise<PrivateSlotsGeneratedDto> =>
      unwrap(await api.POST('/api/events/events/{id}/slots/generate', { params: { path: { id: eventId } } })),
    onSuccess: async () => {
      await slotsChanged(queryClient);
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
