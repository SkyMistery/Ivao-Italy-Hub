import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

import {
  settingsFromFormValues,
  type AircraftGroupFormValues,
  type AircraftProfileFormValues,
  type FlightOpsSettings,
  type SettingsFormValues,
  type TourFormValues,
  type TourFromTemplateFormValues,
  type TourSaveAsTemplateFormValues,
} from './schemas';

/**
 * Every call the screens of the tours make (M2, T5–T6), as query options and mutations: the aircraft data
 * and the tours through the CRUD engine, the settings through the core's settings of a module. What the core
 * already asks — the active awards, the files a picker offers — is the core's query, not a copy here.
 */

export type AircraftProfileDto = components['schemas']['AircraftProfileDto'];
export type AircraftProfilePage = components['schemas']['PagedResultOfAircraftProfileDto'];
export type AircraftGroupDto = components['schemas']['AircraftGroupDto'];
export type AircraftGroupPage = components['schemas']['PagedResultOfAircraftGroupDto'];
export type AircraftTypeDto = components['schemas']['AircraftTypeDto'];

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'flightops';

const profilesKey = ['flightops', 'aircraft-profiles'] as const;
const groupsKey = ['flightops', 'aircraft-groups'] as const;
const settingsKey = ['flightops', 'settings'] as const;
const toursKey = ['flightops', 'tours'] as const;

export type TourListDto = components['schemas']['TourListDto'];
export type TourPage = components['schemas']['PagedResultOfTourListDto'];
export type TourDetailDto = components['schemas']['TourDetailDto'];
export type TourReadyProblemsDto = components['schemas']['TourReadyProblemsDto'];
export type TourStatusAction = components['schemas']['TourStatusAction'];
type TourWriteDto = components['schemas']['TourWriteDto'];

// ---- aircraft types -------------------------------------------------------------------------------

/** The types that match what is typed: the field asks again as the text changes. */
export function aircraftTypesQuery(typed: string) {
  return queryOptions({
    queryKey: ['reference', 'aircraft-types', typed] as const,
    queryFn: async (): Promise<AircraftTypeDto[]> =>
      unwrap(await api.GET('/api/reference/aircraft-types', { params: { query: { q: typed } } })),
  });
}

// ---- profiles --------------------------------------------------------------------------------------

export function profilesListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: [...profilesKey, 'list', search] as const,
    queryFn: async (): Promise<AircraftProfilePage> =>
      unwrap(
        await api.GET('/api/flightops/aircraft-profiles', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export function profileQuery(id: number) {
  return queryOptions({
    queryKey: [...profilesKey, 'detail', id] as const,
    queryFn: async (): Promise<AircraftProfileDto> =>
      unwrap(
        await api.GET('/api/flightops/aircraft-profiles/{id}', { params: { path: { id: String(id) } } }),
      ),
  });
}

export function emptyProfile(department: Department): AircraftProfileFormValues {
  return {
    ownerDepartment: department,
    icaoType: '',
    cruiseTasKt: 450,
    note: '',
    rowVersion: NEW_ROW_VERSION,
  };
}

export function profileToFormValues(profile: AircraftProfileDto): AircraftProfileFormValues {
  return {
    ownerDepartment: profile.ownerDepartment,
    icaoType: profile.icaoType,
    cruiseTasKt: profile.cruiseTasKt,
    note: profile.note ?? '',
    rowVersion: profile.rowVersion,
  };
}

function profileBody(values: AircraftProfileFormValues) {
  return {
    ownerDepartment: values.ownerDepartment,
    icaoType: values.icaoType.trim().toUpperCase(),
    cruiseTasKt: values.cruiseTasKt,
    note: values.note.trim() === '' ? null : values.note.trim(),
    rowVersion: values.rowVersion,
  };
}

export function useSaveProfile(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AircraftProfileFormValues): Promise<AircraftProfileDto> =>
      id === null
        ? unwrap(await api.POST('/api/flightops/aircraft-profiles', { body: profileBody(values) }))
        : unwrap(
            await api.PUT('/api/flightops/aircraft-profiles/{id}', {
              params: { path: { id: String(id) } },
              body: profileBody(values),
            }),
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: profilesKey });
    },
  });
}

export function useDeleteProfile() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/flightops/aircraft-profiles/{id}', { params: { path: { id: String(id) } } }),
      ),
    // Not awaited: the screen that deleted still observes the row it deleted (see the links).
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: profilesKey });
    },
  });
}

// ---- groups ----------------------------------------------------------------------------------------

export function groupsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: [...groupsKey, 'list', search] as const,
    queryFn: async (): Promise<AircraftGroupPage> =>
      unwrap(
        await api.GET('/api/flightops/aircraft-groups', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export function groupQuery(id: number) {
  return queryOptions({
    queryKey: [...groupsKey, 'detail', id] as const,
    queryFn: async (): Promise<AircraftGroupDto> =>
      unwrap(await api.GET('/api/flightops/aircraft-groups/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function emptyGroup(department: Department, locales: readonly string[]): AircraftGroupFormValues {
  return {
    ownerDepartment: department,
    name: Object.fromEntries(locales.map((locale) => [locale, ''])),
    icaoTypes: [{ icao: '' }],
    rowVersion: NEW_ROW_VERSION,
  };
}

export function groupToFormValues(
  group: AircraftGroupDto,
  locales: readonly string[],
): AircraftGroupFormValues {
  return {
    ownerDepartment: group.ownerDepartment,
    name: Object.fromEntries(locales.map((locale) => [locale, group.name[locale] ?? ''])),
    // The form draws a list of objects: one entry per type.
    icaoTypes: group.icaoTypes.map((icao) => ({ icao })),
    rowVersion: group.rowVersion,
  };
}

function groupBody(values: AircraftGroupFormValues) {
  return {
    ownerDepartment: values.ownerDepartment,
    name: values.name,
    icaoTypes: values.icaoTypes.map((entry) => entry.icao.trim().toUpperCase()).filter((icao) => icao !== ''),
    rowVersion: values.rowVersion,
  };
}

export function useSaveGroup(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AircraftGroupFormValues): Promise<AircraftGroupDto> =>
      id === null
        ? unwrap(await api.POST('/api/flightops/aircraft-groups', { body: groupBody(values) }))
        : unwrap(
            await api.PUT('/api/flightops/aircraft-groups/{id}', {
              params: { path: { id: String(id) } },
              body: groupBody(values),
            }),
          ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: groupsKey });
    },
  });
}

export function useDeleteGroup() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(
        await api.DELETE('/api/flightops/aircraft-groups/{id}', { params: { path: { id: String(id) } } }),
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: groupsKey });
    },
  });
}

// ---- settings --------------------------------------------------------------------------------------

export function settingsQuery() {
  return queryOptions({
    queryKey: settingsKey,
    queryFn: async (): Promise<FlightOpsSettings> =>
      unwrap(
        await api.GET('/api/modules/{key}/settings', { params: { path: { key: MODULE_KEY } } }),
      ) as FlightOpsSettings,
  });
}

export function useSaveSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SettingsFormValues): Promise<FlightOpsSettings> =>
      unwrap(
        await api.PUT('/api/modules/{key}/settings', {
          params: { path: { key: MODULE_KEY } },
          body: settingsFromFormValues(values),
        }),
      ) as FlightOpsSettings,
    onSuccess: (saved) => {
      queryClient.setQueryData(settingsKey, saved);
    },
  });
}

// ---- tours -----------------------------------------------------------------------------------------

/**
 * The tours of the back office, templates included: the same resource, `filter[isTemplate]` telling them apart
 * (design M2 §1.10). `needsOwnDailyLimit` is the list the settings show when the division's limit cannot be switched
 * off, asked with the server's own rule (§3.7).
 */
export function toursListQuery(
  search: ListSearch,
  filters: { isTemplate?: boolean; needsOwnDailyLimit?: boolean } = {},
) {
  return queryOptions({
    queryKey: [...toursKey, 'list', search, filters] as const,
    queryFn: async (): Promise<TourPage> =>
      unwrap(
        await api.GET('/api/flightops/tours', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({
            ...(filters.isTemplate === undefined ? {} : { isTemplate: String(filters.isTemplate) }),
            ...(filters.needsOwnDailyLimit === true ? { needsOwnDailyLimit: 'true' } : {}),
          }),
        }),
      ),
  });
}

export function tourQuery(id: number) {
  return queryOptions({
    queryKey: [...toursKey, 'detail', id] as const,
    queryFn: async (): Promise<TourDetailDto> =>
      unwrap(await api.GET('/api/flightops/tours/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** What stands in the way of "ready", asked before anybody presses it and asked again after every save. */
export function tourReadyProblemsQuery(id: number) {
  return queryOptions({
    queryKey: [...toursKey, 'ready-problems', id] as const,
    queryFn: async (): Promise<TourReadyProblemsDto> =>
      unwrap(await api.GET('/api/flightops/tours/{id}/ready-problems', { params: { path: { id } } })),
  });
}

export function emptyTour(
  department: Department,
  locales: readonly string[],
  isTemplate: boolean,
): TourFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    ownerDepartment: department,
    isTemplate,
    kind: 'Sequential',
    title: blank,
    slug: '',
    summary: blank,
    showPreview: false,
    progression: 'FlyAhead',
    requiresProcedures: false,
    referenceAircraftIcao: '',
    rowVersion: NEW_ROW_VERSION,
  };
}

export function tourToFormValues(tour: TourDetailDto, locales: readonly string[]): TourFormValues {
  const spread = (value: Record<string, string>) =>
    Object.fromEntries(locales.map((locale) => [locale, value[locale] ?? '']));

  return {
    ownerDepartment: tour.ownerDepartment,
    isTemplate: tour.isTemplate,
    kind: tour.kind,
    title: spread(tour.title),
    slug: tour.slug ?? '',
    summary: spread(tour.summary),
    ...(tour.releaseAt === null ? {} : { releaseAt: tour.releaseAt }),
    ...(tour.closeAt === null ? {} : { closeAt: tour.closeAt }),
    showPreview: tour.showPreview,
    ...(tour.coverMediaId === null ? {} : { coverMediaId: tour.coverMediaId }),
    ...(tour.bannerMediaId === null ? {} : { bannerMediaId: tour.bannerMediaId }),
    progression: tour.progression,
    ...(tour.hubRotationOrder === null ? {} : { hubRotationOrder: tour.hubRotationOrder }),
    reportWindowDays: tour.reportWindowDays,
    ...(tour.dailyLegLimit === null ? {} : { dailyLegLimit: tour.dailyLegLimit }),
    requiresProcedures: tour.requiresProcedures,
    ...(tour.minPilotRating === null ? {} : { minPilotRating: tour.minPilotRating }),
    referenceAircraftIcao: tour.referenceAircraftIcao ?? '',
    ...(tour.awardId === null ? {} : { awardId: String(tour.awardId) }),
    rowVersion: tour.rowVersion,
  };
}

/** What the form holds, as the API expects it. The briefing is not sent: its editor is T6b's, and null keeps it. */
function tourBody(values: TourFormValues): TourWriteDto {
  const text = (value: string | undefined) =>
    value === undefined || value.trim() === '' ? null : value.trim();
  const award = text(values.awardId);

  return {
    ownerDepartment: values.ownerDepartment,
    isTemplate: values.isTemplate,
    slug: text(values.slug),
    kind: values.kind,
    title: values.title,
    summary: values.summary,
    briefing: null,
    coverMediaId: values.coverMediaId ?? null,
    bannerMediaId: values.bannerMediaId ?? null,
    showPreview: values.showPreview,
    releaseAt: text(values.releaseAt),
    closeAt: text(values.closeAt),
    reportWindowDays: values.reportWindowDays ?? null,
    progression: values.progression,
    hubRotationOrder: values.hubRotationOrder ?? null,
    requiresProcedures: values.requiresProcedures,
    dailyLegLimit: values.dailyLegLimit ?? null,
    minPilotRating: values.minPilotRating ?? null,
    referenceAircraftIcao: text(values.referenceAircraftIcao)?.toUpperCase() ?? null,
    awardId: award === null ? null : Number(award),
    rowVersion: values.rowVersion,
  };
}

export function useSaveTour(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: TourFormValues): Promise<TourDetailDto> =>
      id === null
        ? unwrap(await api.POST('/api/flightops/tours', { body: tourBody(values) }))
        : unwrap(
            await api.PUT('/api/flightops/tours/{id}', {
              params: { path: { id: String(id) } },
              body: tourBody(values),
            }),
          ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(tourQuery(saved.id).queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: toursKey });
    },
  });
}

/** Ready, back to draft, hide, show (design M2 §8.3). A refusal carries the problems, field by field. */
export function useTourStatus(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (action: TourStatusAction): Promise<TourDetailDto> =>
      unwrap(
        await api.POST('/api/flightops/tours/{id}/status', {
          params: { path: { id } },
          body: { action },
        }),
      ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(tourQuery(saved.id).queryKey, saved);
      await queryClient.invalidateQueries({ queryKey: toursKey });
    },
  });
}

export function useDeleteTour() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/flightops/tours/{id}', { params: { path: { id: String(id) } } })),
    // Not awaited: the screen that deleted still observes the row it deleted (see the profiles).
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: toursKey });
    },
  });
}

export function useTourFromTemplate() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: TourFromTemplateFormValues): Promise<TourDetailDto> =>
      unwrap(
        await api.POST('/api/flightops/tours/from-template/{templateId}', {
          params: { path: { templateId: Number(values.templateId) } },
          body: { title: values.title, slug: values.slug.trim() },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: toursKey });
    },
  });
}

export function useSaveTourAsTemplate(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: TourSaveAsTemplateFormValues): Promise<TourDetailDto> =>
      unwrap(
        await api.POST('/api/flightops/tours/{id}/save-as-template', {
          params: { path: { id } },
          body: { title: values.title },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: toursKey });
    },
  });
}
