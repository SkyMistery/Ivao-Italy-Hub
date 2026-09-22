import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, listSearchSchema, toQuery, type ListSearch } from '../../shared/list';

import {
  settingsFromFormValues,
  type AircraftGroupFormValues,
  type AircraftProfileFormValues,
  type CallsignRuleFormValues,
  type FlightOpsSettings,
  type HubFormValues,
  type OpenGoalKind,
  type ParameterKind,
  type ParameterValues,
  type RotationFormValues,
  type TourConstraintFormValues,
  type SettingsFormValues,
  type TourFormValues,
  type TourFromTemplateFormValues,
  type TourSaveAsTemplateFormValues,
  CONSTRAINT_PARAMETERS,
  GOAL_PARAMETERS,
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
export type AllowedAircraft = components['schemas']['AllowedAircraft'];
export type LegDto = components['schemas']['LegDto'];
export type TourLegsDto = components['schemas']['TourLegsDto'];
export type LegWriteDto = components['schemas']['LegWriteDto'];
export type LegRemovalDto = components['schemas']['LegRemovalDto'];
export type LegImportRowDto = components['schemas']['LegImportRowDto'];
export type LegImportRequest = components['schemas']['LegImportRequest'];
export type LegImportPreviewDto = components['schemas']['LegImportPreviewDto'];
export type LegImportLineDto = components['schemas']['LegImportLineDto'];
export type AirportDto = components['schemas']['AirportDto'];
export type HubDto = components['schemas']['HubDto'];
export type RotationDto = components['schemas']['RotationDto'];
export type RotationListDto = components['schemas']['RotationListDto'];
export type CallsignRuleDto = components['schemas']['CallsignRuleDto'];
export type CallsignRuleListDto = components['schemas']['CallsignRuleListDto'];
export type TourConstraintDto = components['schemas']['TourConstraintDto'];
export type TourConstraintListDto = components['schemas']['TourConstraintListDto'];

// ---- aircraft types -------------------------------------------------------------------------------

/** The types that match what is typed: the field asks again as the text changes. */
export function aircraftTypesQuery(typed: string) {
  return queryOptions({
    queryKey: ['reference', 'aircraft-types', typed] as const,
    queryFn: async (): Promise<AircraftTypeDto[]> =>
      unwrap(await api.GET('/api/reference/aircraft-types', { params: { query: { q: typed } } })),
  });
}

/** The airports that match what is typed, by ICAO, IATA or name: the cell asks again as the text changes. */
export function airportsQuery(typed: string) {
  return queryOptions({
    queryKey: ['reference', 'airports', typed] as const,
    queryFn: async (): Promise<AirportDto[]> =>
      unwrap(await api.GET('/api/reference/airports', { params: { query: { q: typed } } })),
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
  filters: { isTemplate?: boolean; needsOwnDailyLimit?: boolean; parent?: number } = {},
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
            // The subtours of a container; without it, the server lists the tours that are not subtours (T7b).
            ...(filters.parent === undefined ? {} : { parent: String(filters.parent) }),
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

/** A new tour, template, or subtour of a container — whose dates, left empty, are the container's. */
export function emptyTour(
  department: Department,
  locales: readonly string[],
  isTemplate: boolean,
  parentTourId?: number,
): TourFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    ownerDepartment: department,
    isTemplate,
    ...(parentTourId === undefined ? {} : { parentTourId }),
    kind: 'Sequential',
    title: blank,
    slug: '',
    summary: blank,
    showPreview: false,
    progression: 'FlyAhead',
    requiresProcedures: false,
    referenceAircraftIcao: '',
    allowedAircraft: [],
    allowedGroups: [],
    rowVersion: NEW_ROW_VERSION,
  };
}

export function tourToFormValues(tour: TourDetailDto, locales: readonly string[]): TourFormValues {
  const spread = (value: Record<string, string>) =>
    Object.fromEntries(locales.map((locale) => [locale, value[locale] ?? '']));

  return {
    ownerDepartment: tour.ownerDepartment,
    isTemplate: tour.isTemplate,
    ...(tour.parentTourId === null ? {} : { parentTourId: tour.parentTourId }),
    kind: tour.kind,
    title: spread(tour.title),
    slug: tour.slug ?? '',
    summary: spread(tour.summary),
    // A subtour's date taken from its container is shown empty, and stays the container's when saved as it is.
    ...(tour.releaseAt === null || tour.releaseFromParent ? {} : { releaseAt: tour.releaseAt }),
    ...(tour.closeAt === null || tour.closeFromParent ? {} : { closeAt: tour.closeAt }),
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
    ...(tour.requiredNm === null ? {} : { requiredNm: tour.requiredNm }),
    ...(tour.requiredSubtours === null ? {} : { requiredSubtours: tour.requiredSubtours }),
    // The form repeats objects: one entry per type, one per group.
    allowedAircraft: tour.allowedAircraft.types.map((icao) => ({ icao })),
    allowedGroups: tour.allowedAircraft.groupIds.map((id) => ({ groupId: String(id) })),
    ...(tour.awardId === null ? {} : { awardId: String(tour.awardId) }),
    rowVersion: tour.rowVersion,
  };
}

/** The aircraft a tour admits, out of the two lists the form repeats: empty entries dropped, codes upper case. */
export function allowedFromFormValues(
  types: readonly { icao: string }[],
  groups: readonly { groupId?: string | undefined }[],
): AllowedAircraft {
  return {
    types: types.map((entry) => entry.icao.trim().toUpperCase()).filter((icao) => icao !== ''),
    groupIds: groups
      .map((entry) => entry.groupId?.trim() ?? '')
      .filter((id) => id !== '')
      .map(Number),
  };
}

/** The goal of an Open tour as its tab sends it (T7c): the kind, and the parameters as the form holds them. */
export interface OpenGoalChange {
  readonly openGoal: OpenGoalKind;
  readonly parameters: ParameterValues;
}

/**
 * What the form holds, as the API expects it. The briefing is sent only by its own tab (T6b), and the goal of an Open
 * tour only by its own (T7c): the settings form leaves them out, and null keeps them as they are.
 */
function tourBody(values: TourFormValues, briefing: Body | null, goal: OpenGoalChange | null): TourWriteDto {
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
    briefing,
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
    requiredNm: values.requiredNm ?? null,
    allowedAircraft: allowedFromFormValues(values.allowedAircraft, values.allowedGroups),
    awardId: award === null ? null : Number(award),
    rowVersion: values.rowVersion,
    parentTourId: values.parentTourId ?? null,
    requiredSubtours: values.requiredSubtours ?? null,
    openGoal: goal?.openGoal ?? null,
    openGoalParameters:
      goal === null ? null : parametersBody(GOAL_PARAMETERS[goal.openGoal], goal.parameters),
  };
}

/**
 * Saves a tour: the settings, and the briefing or the goal when their tab sends them. One `PUT` for all, so they travel
 * with the row's version like every other field.
 */
export function useSaveTour(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      values,
      briefing = null,
      goal = null,
    }: {
      values: TourFormValues;
      briefing?: Body | null;
      goal?: OpenGoalChange | null;
    }): Promise<TourDetailDto> =>
      id === null
        ? unwrap(await api.POST('/api/flightops/tours', { body: tourBody(values, briefing, goal) }))
        : unwrap(
            await api.PUT('/api/flightops/tours/{id}', {
              params: { path: { id: String(id) } },
              body: tourBody(values, briefing, goal),
            }),
          ),
    onSuccess: async (saved) => {
      queryClient.setQueryData(tourQuery(saved.id).queryKey, saved);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: toursKey }),
        queryClient.invalidateQueries({ queryKey: tourReadyProblemsQuery(saved.id).queryKey }),
      ]);
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

// ---- legs ------------------------------------------------------------------------------------------

/**
 * Every leg of a tour, retired ones included, with the totals of those still flown (design M2 §8.4). Every write of
 * the editor answers with the same grid, renumbered by the server, and that answer replaces this one: the editor never
 * guesses a number.
 */
export function tourLegsQuery(tourId: number) {
  return queryOptions({
    queryKey: [...toursKey, 'legs', tourId] as const,
    queryFn: async (): Promise<TourLegsDto> =>
      unwrap(await api.GET('/api/flightops/tours/{tourId}/legs', { params: { path: { tourId } } })),
  });
}

/** What removing a leg would do: deleted, retired, or retired with its rotation. Asked before anybody confirms. */
export async function legRemoval(tourId: number, legId: number): Promise<LegRemovalDto> {
  return unwrap(
    await api.GET('/api/flightops/tours/{tourId}/legs/{legId}/removal', {
      params: { path: { tourId, legId } },
    }),
  );
}

/** What an import would do, computed by the server and written nowhere (T8): the answer carries the fingerprint to apply. */
export async function legImportPreview(
  tourId: number,
  request: LegImportRequest,
): Promise<LegImportPreviewDto> {
  return unwrap(
    await api.POST('/api/flightops/tours/{tourId}/legs/import/preview', {
      params: { path: { tourId } },
      body: request,
    }),
  );
}

export type LegChange =
  | { kind: 'create'; leg: LegWriteDto; after: number | null }
  | { kind: 'update'; legId: number; leg: LegWriteDto }
  | { kind: 'remove'; legId: number; reason: string | null; rowVersion: string }
  | { kind: 'restore'; legId: number; reason: string; rowVersion: string }
  | { kind: 'import'; request: LegImportRequest };

/**
 * One write of the leg editor, whichever it is. The answer is the whole grid, put straight into the cache; the tour's
 * problems of "ready" are asked again, because a leg is part of what they look at.
 */
export function useLegChange(tourId: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (change: LegChange): Promise<TourLegsDto> => {
      switch (change.kind) {
        case 'create':
          return unwrap(
            await api.POST('/api/flightops/tours/{tourId}/legs', {
              params: { path: { tourId }, query: change.after === null ? {} : { after: change.after } },
              body: change.leg,
            }),
          );
        case 'update':
          return unwrap(
            await api.PUT('/api/flightops/tours/{tourId}/legs/{legId}', {
              params: { path: { tourId, legId: change.legId } },
              body: change.leg,
            }),
          );
        case 'remove':
          return unwrap(
            await api.POST('/api/flightops/tours/{tourId}/legs/{legId}/remove', {
              params: { path: { tourId, legId: change.legId } },
              body: { reason: change.reason, rowVersion: change.rowVersion },
            }),
          );
        case 'restore':
          return unwrap(
            await api.POST('/api/flightops/tours/{tourId}/legs/{legId}/restore', {
              params: { path: { tourId, legId: change.legId } },
              body: { reason: change.reason, rowVersion: change.rowVersion },
            }),
          );
        case 'import':
          return unwrap(
            await api.POST('/api/flightops/tours/{tourId}/legs/import', {
              params: { path: { tourId } },
              body: change.request,
            }),
          );
      }
    },
    onSuccess: async (grid) => {
      queryClient.setQueryData(tourLegsQuery(tourId).queryKey, grid);
      await queryClient.invalidateQueries({ queryKey: tourReadyProblemsQuery(tourId).queryKey });
    },
  });
}

// ---- the shape of a tour: hubs, rotations, callsign constraints (T7b) ------------------------------

const hubsKey = ['flightops', 'hubs'] as const;
const rotationsKey = ['flightops', 'rotations'] as const;
const callsignRulesKey = ['flightops', 'callsign-rules'] as const;
const constraintsKey = ['flightops', 'tour-constraints'] as const;

/** Every row of a tour's tab on one page: a tour has a handful of hubs, rotations and constraints, never pages of them. */
const allOfATour: ListSearch = listSearchSchema.parse({ pageSize: 100 });

/** The subtours of a container (design M2 §2.7): the tours whose parent it is. */
export function subtoursQuery(containerId: number, search: ListSearch) {
  return toursListQuery(search, { parent: containerId });
}

export function hubsQuery(tourId: number, search: ListSearch = allOfATour) {
  return queryOptions({
    queryKey: [...hubsKey, 'list', tourId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/hubs', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ tourId: String(tourId) }),
        }),
      ),
  });
}

export function hubQuery(id: number) {
  return queryOptions({
    queryKey: [...hubsKey, 'detail', id] as const,
    queryFn: async (): Promise<HubDto> =>
      unwrap(await api.GET('/api/flightops/hubs/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function rotationsQuery(tourId: number, search: ListSearch = allOfATour) {
  return queryOptions({
    queryKey: [...rotationsKey, 'list', tourId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/rotations', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ tourId: String(tourId) }),
        }),
      ),
  });
}

export function rotationQuery(id: number) {
  return queryOptions({
    queryKey: [...rotationsKey, 'detail', id] as const,
    queryFn: async (): Promise<RotationDto> =>
      unwrap(await api.GET('/api/flightops/rotations/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function callsignRulesQuery(tourId: number, search: ListSearch = allOfATour) {
  return queryOptions({
    queryKey: [...callsignRulesKey, 'list', tourId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/callsign-rules', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ tourId: String(tourId) }),
        }),
      ),
  });
}

export function callsignRuleQuery(id: number) {
  return queryOptions({
    queryKey: [...callsignRulesKey, 'detail', id] as const,
    queryFn: async (): Promise<CallsignRuleDto> =>
      unwrap(await api.GET('/api/flightops/callsign-rules/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function emptyHub(tourId: number, sort: number): HubFormValues {
  return { tourId, icao: '', sort, rowVersion: NEW_ROW_VERSION };
}

export function hubToFormValues(hub: HubDto): HubFormValues {
  return { tourId: hub.tourId, icao: hub.icao, sort: hub.sort, rowVersion: hub.rowVersion };
}

export function emptyRotation(tourId: number, sort: number): RotationFormValues {
  return { tourId, hubId: '', sort, size: '2', rowVersion: NEW_ROW_VERSION };
}

export function rotationToFormValues(rotation: RotationDto): RotationFormValues {
  return {
    tourId: rotation.tourId,
    hubId: String(rotation.hubId),
    sort: rotation.sort,
    size: String(rotation.size) as RotationFormValues['size'],
    rowVersion: rotation.rowVersion,
  };
}

export function emptyCallsignRule(tourId: number): CallsignRuleFormValues {
  return { tourId, mode: 'Allow', match: 'Airline', value: '', rowVersion: NEW_ROW_VERSION };
}

export function callsignRuleToFormValues(rule: CallsignRuleDto): CallsignRuleFormValues {
  return {
    tourId: rule.tourId,
    mode: rule.mode,
    match: rule.match,
    value: rule.value,
    ...(rule.legId === null ? {} : { legId: String(rule.legId) }),
    rowVersion: rule.rowVersion,
  };
}

/**
 * After a row of a tour's shape is written, what reads it is asked again: its list, the problems of "ready" of its
 * tour, and — for a rotation — the grid of the legs, which offers the rotations.
 */
function useShapeSaved(key: readonly string[]) {
  const queryClient = useQueryClient();

  return async (tourId: number) => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: key }),
      queryClient.invalidateQueries({ queryKey: tourReadyProblemsQuery(tourId).queryKey }),
    ]);
  };
}

export function useSaveHub(id: number | null) {
  const saved = useShapeSaved(hubsKey);

  return useMutation({
    mutationFn: async (values: HubFormValues): Promise<HubDto> => {
      const body = { ...values, icao: values.icao.trim().toUpperCase() };
      return id === null
        ? unwrap(await api.POST('/api/flightops/hubs', { body }))
        : unwrap(await api.PUT('/api/flightops/hubs/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: (hub) => saved(hub.tourId),
  });
}

export function useSaveRotation(id: number | null) {
  const saved = useShapeSaved(rotationsKey);

  return useMutation({
    mutationFn: async (values: RotationFormValues): Promise<RotationDto> => {
      const body = { ...values, hubId: Number(values.hubId), size: Number(values.size) };
      return id === null
        ? unwrap(await api.POST('/api/flightops/rotations', { body }))
        : unwrap(
            await api.PUT('/api/flightops/rotations/{id}', { params: { path: { id: String(id) } }, body }),
          );
    },
    onSuccess: (rotation) => saved(rotation.tourId),
  });
}

export function useSaveCallsignRule(id: number | null) {
  const saved = useShapeSaved(callsignRulesKey);

  return useMutation({
    mutationFn: async (values: CallsignRuleFormValues): Promise<CallsignRuleDto> => {
      const legId = values.legId?.trim() ?? '';
      const body = {
        tourId: values.tourId,
        mode: values.mode,
        match: values.match,
        value: values.value.trim().toUpperCase(),
        legId: legId === '' ? null : Number(legId),
        rowVersion: values.rowVersion,
      };
      return id === null
        ? unwrap(await api.POST('/api/flightops/callsign-rules', { body }))
        : unwrap(
            await api.PUT('/api/flightops/callsign-rules/{id}', {
              params: { path: { id: String(id) } },
              body,
            }),
          );
    },
    onSuccess: (rule) => saved(rule.tourId),
  });
}

// ---- the Open tour: parameters, filters and sequence rules (T7c) -------------------------------------

/**
 * The parameters as the API expects them, out of the form's: a list of `{ icao }` or `{ code }` becomes its codes, upper
 * case and without the empty ones; an empty number is left out. Everything else (the bounds, whether a code exists) is
 * the server's to say, on the field.
 */
export function parametersBody(
  shape: Readonly<Record<string, ParameterKind>>,
  values: ParameterValues | undefined,
): Record<string, unknown> {
  const body: Record<string, unknown> = {};

  for (const [name, kind] of Object.entries(shape)) {
    const value = values?.[name];

    switch (kind) {
      case 'airports':
      case 'countries':
      case 'firs': {
        const entries = Array.isArray(value) ? (value as Record<string, unknown>[]) : [];
        const key = kind === 'airports' ? 'icao' : 'code';
        body[name] = entries
          .map((entry) => (typeof entry[key] === 'string' ? entry[key] : '').trim().toUpperCase())
          .filter((code) => code !== '');
        break;
      }
      case 'airport':
        body[name] = typeof value === 'string' ? value.trim().toUpperCase() : '';
        break;
      case 'whole':
      case 'wholeOptional':
        if (typeof value === 'number') {
          body[name] = value;
        }
        break;
      case 'rules':
      case 'categories':
        if (value !== undefined) {
          body[name] = value;
        }
        break;
    }
  }

  return body;
}

/** The parameters as the form holds them, out of what the API stored; a list is a list of objects to repeat. */
export function parametersToFormValues(
  shape: Readonly<Record<string, ParameterKind>>,
  stored: unknown,
): ParameterValues {
  const source = typeof stored === 'object' && stored !== null ? (stored as Record<string, unknown>) : {};
  const values: ParameterValues = {};

  for (const [name, kind] of Object.entries(shape)) {
    const value = source[name];
    const codes = Array.isArray(value)
      ? value.filter((code): code is string => typeof code === 'string')
      : [];

    switch (kind) {
      case 'airports':
        values[name] = codes.map((icao) => ({ icao }));
        break;
      case 'countries':
      case 'firs':
        values[name] = codes.map((code) => ({ code }));
        break;
      case 'airport':
        values[name] = typeof value === 'string' ? value : '';
        break;
      case 'categories':
        values[name] = codes;
        break;
      case 'rules':
        values[name] = value === 'I' ? 'I' : 'V';
        break;
      case 'whole':
      case 'wholeOptional':
        if (typeof value === 'number') {
          values[name] = value;
        }
        break;
    }
  }

  return values;
}

export function constraintsQuery(tourId: number, search: ListSearch = allOfATour) {
  return queryOptions({
    queryKey: [...constraintsKey, 'list', tourId, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/tour-constraints', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ tourId: String(tourId) }),
        }),
      ),
  });
}

export function constraintQuery(id: number) {
  return queryOptions({
    queryKey: [...constraintsKey, 'detail', id] as const,
    queryFn: async (): Promise<TourConstraintDto> =>
      unwrap(await api.GET('/api/flightops/tour-constraints/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function emptyConstraint(
  tourId: number,
  kind: TourConstraintFormValues['kind'],
): TourConstraintFormValues {
  const shape = CONSTRAINT_PARAMETERS[kind];

  return {
    tourId,
    kind,
    ...(Object.keys(shape).length === 0 ? {} : { parameters: parametersToFormValues(shape, {}) }),
    rowVersion: NEW_ROW_VERSION,
  };
}

export function constraintToFormValues(constraint: TourConstraintDto): TourConstraintFormValues {
  const shape = CONSTRAINT_PARAMETERS[constraint.kind];

  return {
    tourId: constraint.tourId,
    kind: constraint.kind,
    ...(Object.keys(shape).length === 0
      ? {}
      : { parameters: parametersToFormValues(shape, constraint.parameters) }),
    rowVersion: constraint.rowVersion,
  };
}

export function useSaveConstraint(id: number | null) {
  const saved = useShapeSaved(constraintsKey);

  return useMutation({
    mutationFn: async (values: TourConstraintFormValues): Promise<TourConstraintDto> => {
      const body = {
        tourId: values.tourId,
        kind: values.kind,
        parameters: parametersBody(CONSTRAINT_PARAMETERS[values.kind], values.parameters),
        rowVersion: values.rowVersion,
      };
      return id === null
        ? unwrap(await api.POST('/api/flightops/tour-constraints', { body }))
        : unwrap(
            await api.PUT('/api/flightops/tour-constraints/{id}', {
              params: { path: { id: String(id) } },
              body,
            }),
          );
    },
    onSuccess: (constraint) => saved(constraint.tourId),
  });
}

/** Deleting a hub, a rotation or a constraint: the server refuses a hub with rotations and a rotation with legs. */
export function useDeleteShapeRow(
  resource: 'hubs' | 'rotations' | 'callsign-rules' | 'tour-constraints',
  tourId: number,
) {
  const saved = useShapeSaved(['flightops', resource]);

  return useMutation({
    mutationFn: async (id: number): Promise<void> => {
      const path = { params: { path: { id: String(id) } } };
      switch (resource) {
        case 'hubs':
          return unwrapEmpty(await api.DELETE('/api/flightops/hubs/{id}', path));
        case 'rotations':
          return unwrapEmpty(await api.DELETE('/api/flightops/rotations/{id}', path));
        case 'callsign-rules':
          return unwrapEmpty(await api.DELETE('/api/flightops/callsign-rules/{id}', path));
        case 'tour-constraints':
          return unwrapEmpty(await api.DELETE('/api/flightops/tour-constraints/{id}', path));
      }
    },
    onSuccess: () => saved(tourId),
  });
}
