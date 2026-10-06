import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Body } from '../../blocks';
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
  SettingsFormValues,
} from './schemas';

/**
 * Every call the screens of the events make (M4): the settings through the core's settings of a module (E2); the events and
 * their airports through the CRUD engine, and the three endpoints written by hand beside it — cancelling and the presets of
 * the kinds (E3a), publishing (E3b).
 */

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'events';

const settingsKey = ['events', 'settings'] as const;
const eventsKey = ['events', 'events'] as const;
const airportsKey = ['events', 'airports'] as const;

export type EventListDto = components['schemas']['EventListDto'];
export type EventDetailDto = components['schemas']['EventDetailDto'];
export type EventAirportDto = components['schemas']['EventAirportDto'];
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
