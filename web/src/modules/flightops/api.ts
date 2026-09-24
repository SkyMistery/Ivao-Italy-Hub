import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import type { components } from '../../shared/api/schema';
import {
  listQuerySerializer,
  listSearchSchema,
  toQuery,
  type ListSearch,
  type Page,
} from '../../shared/list';

import {
  settingsFromFormValues,
  type AircraftGroupFormValues,
  type BanFormValues,
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
  type CheckChoice,
  type CheckKey,
  type CopyRulesFormValues,
  type LegIssueFormValues,
  type RuleFormValues,
  type TourErrorFormValues,
  CHECK_KEYS,
  CONSTRAINT_PARAMETERS,
  GOAL_PARAMETERS,
  checkParameters,
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

// ---- rules and errors (T9) --------------------------------------------------------------------------

export type TourRuleDto = components['schemas']['TourRuleDto'];
export type TourRuleListDto = components['schemas']['TourRuleListDto'];
export type TourErrorDto = components['schemas']['TourErrorDto'];
export type TourErrorListDto = components['schemas']['TourErrorListDto'];
export type EffectiveRuleDto = components['schemas']['EffectiveRuleDto'];
export type CopyRulesResultDto = components['schemas']['CopyRulesResultDto'];

const rulesKey = ['flightops', 'rules'] as const;
const errorsKey = ['flightops', 'errors'] as const;

/** Whose rules: the general ones, or a tour's (`filter[tour]`). */
export function rulesListQuery(search: ListSearch, tour: number | 'general' = 'general') {
  return queryOptions({
    queryKey: [...rulesKey, 'list', tour, search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/rules', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({ tour: String(tour) }),
        }),
      ),
  });
}

/** Every general rule, for the tab of a tour: a regulation has dozens, never pages of them. */
export function generalRulesQuery() {
  return rulesListQuery(allOfATour, 'general');
}

export function tourRulesQuery(tourId: number, search: ListSearch = allOfATour) {
  return rulesListQuery(search, tourId);
}

export function ruleQuery(id: number) {
  return queryOptions({
    queryKey: [...rulesKey, 'detail', id] as const,
    queryFn: async (): Promise<TourRuleDto> =>
      unwrap(await api.GET('/api/flightops/rules/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** The rules as they hold on a tour (design M2 §5.2), composed by the server. */
export function effectiveRulesQuery(tourId: number) {
  return queryOptions({
    queryKey: [...rulesKey, 'effective', tourId] as const,
    queryFn: async (): Promise<EffectiveRuleDto[]> =>
      unwrap(
        await api.GET('/api/flightops/tours/{id}/effective-rules', {
          params: { path: { id: tourId } },
        }),
      ),
  });
}

function checkChoice(key: string | null | undefined): CheckChoice {
  return (CHECK_KEYS as readonly string[]).includes(key ?? '') ? (key as CheckKey) : 'none';
}

/**
 * A new rule: a general one, a tour's own, or — with the rule it amends — an amendment, which starts from that rule's
 * code, title and text and from no parameter of its own: what it leaves empty stays the amended rule's.
 */
export function emptyRule(
  locales: readonly string[],
  { tourId, amends }: { tourId?: number; amends?: TourRuleDto } = {},
): RuleFormValues {
  const check = checkChoice(amends?.checkKey);
  const shape = checkParameters(check);

  return {
    ...(tourId === undefined ? {} : { tourId }),
    ...(amends === undefined ? {} : { amendsRuleId: amends.id }),
    checkKey: check,
    code: amends?.code ?? '',
    title: Object.fromEntries(locales.map((locale) => [locale, amends?.title[locale] ?? ''])),
    text: Object.fromEntries(locales.map((locale) => [locale, amends?.text[locale] ?? ''])),
    ...(Object.keys(shape).length === 0 ? {} : { parameters: parametersToFormValues(shape, {}) }),
    errorIds: [],
    sort: amends?.sort ?? 0,
    retired: false,
    rowVersion: NEW_ROW_VERSION,
  };
}

export function ruleToFormValues(rule: TourRuleDto, locales: readonly string[]): RuleFormValues {
  const check = checkChoice(rule.checkKey);
  const shape = checkParameters(check);

  return {
    ...(rule.tourId === null ? {} : { tourId: rule.tourId }),
    ...(rule.amendsRuleId === null ? {} : { amendsRuleId: rule.amendsRuleId }),
    checkKey: check,
    code: rule.code,
    title: Object.fromEntries(locales.map((locale) => [locale, rule.title[locale] ?? ''])),
    text: Object.fromEntries(locales.map((locale) => [locale, rule.text[locale] ?? ''])),
    ...(Object.keys(shape).length === 0
      ? {}
      : { parameters: parametersToFormValues(shape, rule.parameters) }),
    errorIds: rule.errorIds.map(String),
    sort: rule.sort,
    retired: rule.retired,
    rowVersion: rule.rowVersion,
  };
}

/** After a rule is written: the lists of rules, and the rules in force of every tour, which it may change. */
function useRulesSaved() {
  const queryClient = useQueryClient();

  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: rulesKey }),
      queryClient.invalidateQueries({ queryKey: errorsKey }),
    ]);
  };
}

export function useSaveRule(id: number | null) {
  const saved = useRulesSaved();

  return useMutation({
    mutationFn: async (values: RuleFormValues): Promise<TourRuleDto> => {
      const body = {
        tourId: values.tourId ?? null,
        amendsRuleId: values.amendsRuleId ?? null,
        checkKey: values.checkKey === 'none' ? null : values.checkKey,
        code: values.code.trim().toUpperCase(),
        title: values.title,
        text: values.text,
        parameters: parametersBody(checkParameters(values.checkKey), values.parameters),
        errorIds: values.errorIds.map(Number),
        sort: values.sort,
        retired: values.retired,
        rowVersion: values.rowVersion,
      };
      return id === null
        ? unwrap(await api.POST('/api/flightops/rules', { body }))
        : unwrap(await api.PUT('/api/flightops/rules/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: saved,
  });
}

export function useDeleteRule() {
  const saved = useRulesSaved();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/flightops/rules/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: saved,
  });
}

/** "Copy the rules of another tour": the answer says how many were added and which codes the tour already had. */
export function useCopyRules(tourId: number) {
  const saved = useRulesSaved();

  return useMutation({
    mutationFn: async (values: CopyRulesFormValues): Promise<CopyRulesResultDto> =>
      unwrap(
        await api.POST('/api/flightops/tours/{id}/copy-rules', {
          params: { path: { id: tourId } },
          body: { sourceTourId: Number(values.sourceTourId) },
        }),
      ),
    onSuccess: saved,
  });
}

export function errorsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: [...errorsKey, 'list', search] as const,
    queryFn: async () =>
      unwrap(
        await api.GET('/api/flightops/errors', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

/** Every error, for the choice of a rule's: a catalogue of a few dozen. */
export function allErrorsQuery() {
  return errorsListQuery(listSearchSchema.parse({ pageSize: 100 }));
}

export function errorQuery(id: number) {
  return queryOptions({
    queryKey: [...errorsKey, 'detail', id] as const,
    queryFn: async (): Promise<TourErrorDto> =>
      unwrap(await api.GET('/api/flightops/errors/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function emptyError(locales: readonly string[]): TourErrorFormValues {
  const blank = Object.fromEntries(locales.map((locale) => [locale, '']));

  return {
    checkKey: 'none',
    name: blank,
    description: blank,
    examples: blank,
    category: 'Info',
    isPublic: false,
    retired: false,
    rowVersion: NEW_ROW_VERSION,
  };
}

export function errorToFormValues(error: TourErrorDto, locales: readonly string[]): TourErrorFormValues {
  const read = (value: Record<string, string>) =>
    Object.fromEntries(locales.map((locale) => [locale, value[locale] ?? '']));

  return {
    checkKey: checkChoice(error.checkKey),
    name: read(error.name),
    description: read(error.description),
    examples: read(error.examples),
    category: error.category,
    ...(error.yearlyMax === null ? {} : { yearlyMax: error.yearlyMax }),
    isPublic: error.isPublic,
    retired: error.retired,
    rowVersion: error.rowVersion,
  };
}

export function useSaveError(id: number | null) {
  const saved = useRulesSaved();

  return useMutation({
    mutationFn: async (values: TourErrorFormValues): Promise<TourErrorDto> => {
      const body = {
        checkKey: values.checkKey === 'none' ? null : values.checkKey,
        name: values.name,
        description: values.description,
        examples: values.examples ?? {},
        category: values.category,
        yearlyMax: values.category === 'Warning' ? (values.yearlyMax ?? null) : null,
        isPublic: values.isPublic,
        retired: values.retired,
        rowVersion: values.rowVersion,
      };
      return id === null
        ? unwrap(await api.POST('/api/flightops/errors', { body }))
        : unwrap(await api.PUT('/api/flightops/errors/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: saved,
  });
}

export function useDeleteError() {
  const saved = useRulesSaved();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/flightops/errors/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: saved,
  });
}

// ---- the public side (T10) --------------------------------------------------------------------------

export type PublicTourCardDto = components['schemas']['PublicTourCardDto'];
export type PublicTourDto = components['schemas']['PublicTourDto'];
export type PublicLegDto = components['schemas']['PublicLegDto'];

const publicKey = ['flightops', 'public'] as const;

/** The cards of `/tours`: what the public sees now, in the server's order. Anonymous, like the page. */
export const publicToursQuery = queryOptions({
  queryKey: [...publicKey, 'tours'] as const,
  queryFn: async (): Promise<PublicTourCardDto[]> => unwrap(await api.GET('/api/flightops/tours/public')),
});

/**
 * One tour by its address. A tour nobody outside the staff may see answers 404, and the query answers `null` for it:
 * a module route has no `errorComponent` of its own — the manifest declares a component and a permission — so the
 * screen decides, which is where "not found" is a page and not an exception.
 */
export function publicTourQuery(slug: string) {
  return queryOptions({
    queryKey: [...publicKey, 'tour', slug] as const,
    queryFn: async (): Promise<PublicTourDto | null> => {
      const answer = await api.GET('/api/flightops/tours/public/{slug}', { params: { path: { slug } } });

      return answer.response.status === 404 ? null : unwrap(answer);
    },
  });
}

// ---- the pilot's reports (T11b) ---------------------------------------------------------------------

export type MyTourDto = components['schemas']['MyTourDto'];
export type LegProgress = components['schemas']['LegProgress'];
export type PirepDto = components['schemas']['PirepDto'];
export type PirepStatus = components['schemas']['PirepStatus'];
export type TrackerSessionDto = components['schemas']['TrackerSessionDto'];
export type PirepWriteDto = components['schemas']['PirepWriteDto'];
export type AtcProposalDto = components['schemas']['AtcProposalDto'];
export type AtcContactDto = components['schemas']['AtcContactDto'];
export type AtcContactWriteDto = components['schemas']['AtcContactWriteDto'];
export type AtcExemptionWriteDto = components['schemas']['AtcExemptionWriteDto'];
export type ExemptionKind = components['schemas']['ExemptionKind'];

const reportsKey = ['flightops', 'reports'] as const;

/**
 * Where the signed in pilot is in a tour: the colour of every leg, what may be reported now, the next, their reports and,
 * when they may send none, why. Everything is the server's answer (`TourRules`): the browser computes none of it.
 */
export function myTourQuery(tourId: number) {
  return queryOptions({
    queryKey: [...reportsKey, 'mine', tourId] as const,
    queryFn: async (): Promise<MyTourDto> =>
      unwrap(await api.GET('/api/flightops/tours/{tourId}/reports/mine', { params: { path: { tourId } } })),
  });
}

/** Where the flights are searched: between a leg's airports, between two airports (after a diversion), or anywhere. */
export type SessionSearch =
  { legId: number } | { departure: string; arrival: string } | Record<string, never>;

/**
 * The pilot's sessions of the tracker the tour may take. A 503 is «the tracker did not answer», which is not «no flight»:
 * it reaches the screen as an `ApiError` with that status, and the screen says so.
 */
export function trackerSessionsQuery(tourId: number, search: SessionSearch) {
  return queryOptions({
    queryKey: [...reportsKey, 'sessions', tourId, search] as const,
    queryFn: async (): Promise<TrackerSessionDto[]> =>
      unwrap(
        await api.GET('/api/flightops/tours/{tourId}/reports/sessions', {
          params: { path: { tourId }, query: search },
        }),
      ),
    // A flight landed a minute ago should appear when the pilot comes back to the tab.
    staleTime: 0,
    retry: false,
  });
}

/**
 * The controllers online along the chosen flights (design M2 §3.3), proposed while the pilot fills the form in. An archive
 * the division does not have is `available: false`, an answer and not an error.
 */
export function atcProposalQuery(
  tourId: number,
  sessionIds: readonly number[],
  diversionIcao: string | null,
) {
  return queryOptions({
    queryKey: [...reportsKey, 'atc', tourId, sessionIds, diversionIcao] as const,
    queryFn: async (): Promise<AtcProposalDto> =>
      unwrap(
        await api.GET('/api/flightops/tours/{tourId}/reports/atc', {
          params: {
            path: { tourId },
            query: { sessionIds: [...sessionIds], ...(diversionIcao === null ? {} : { diversionIcao }) },
          },
        }),
      ),
    retry: false,
  });
}

/** One of the pilot's own reports, to correct it. */
export function reportQuery(id: number) {
  return queryOptions({
    queryKey: [...reportsKey, 'one', id] as const,
    queryFn: async (): Promise<PirepDto> =>
      unwrap(await api.GET('/api/flightops/reports/{id}', { params: { path: { id } } })),
  });
}

function useReportsChanged() {
  const queryClient = useQueryClient();

  return async () => {
    await queryClient.invalidateQueries({ queryKey: reportsKey });
  };
}

/** Sends a report, or sends again one «to modify» when `correcting` is given; the refusals arrive field by field. */
export function useSendReport(tourId: number, correcting: number | null) {
  const changed = useReportsChanged();

  return useMutation({
    mutationFn: async (body: PirepWriteDto): Promise<PirepDto> =>
      correcting === null
        ? unwrap(
            await api.POST('/api/flightops/tours/{tourId}/reports', { params: { path: { tourId } }, body }),
          )
        : unwrap(
            await api.PUT('/api/flightops/reports/{id}', { params: { path: { id: correcting } }, body }),
          ),
    onSuccess: changed,
  });
}

/**
 * Disputes a rejection (§3.8, T14b): the report is flagged and the thread with the department opens; what comes back carries
 * the thread, which the page links to.
 */
export function useDisputeReport() {
  const changed = useReportsChanged();

  return useMutation({
    mutationFn: async ({
      report,
      text,
    }: {
      report: Pick<PirepDto, 'id' | 'rowVersion'>;
      text: string;
    }): Promise<PirepDto> =>
      unwrap(
        await api.POST('/api/flightops/reports/{id}/dispute', {
          params: { path: { id: report.id } },
          body: { text, rowVersion: report.rowVersion },
        }),
      ),
    onSuccess: changed,
  });
}

/** One object a clarification cites, as the core's form takes it: the module and `pirep:12`, `leg:3`, `rule:4:9`. */
export interface ClarificationReference {
  readonly sourceModule: 'flightops';
  readonly sourceId: string;
}

/**
 * Asks the department to explain (§3.10, T14b): a thread of the core's contacts, of the kind `clarification`, citing the
 * tour's objects. The module's resolver on the server checks each is the pilot's to cite.
 */
export function useAskClarification() {
  return useMutation({
    mutationFn: async (message: {
      department: Department;
      subject: string;
      body: string;
      references: readonly ClarificationReference[];
    }): Promise<{ id: number }> =>
      unwrap(
        await api.POST('/api/contacts', {
          body: {
            department: message.department,
            subject: message.subject.trim(),
            body: message.body.trim(),
            kind: 'clarification',
            references: [...message.references],
          },
        }),
      ),
  });
}

/** Reports a problem on a leg (§3.11, T14b): it reaches the mailbox of the tour's department. */
export function useReportLegIssue(tourId: number) {
  return useMutation({
    mutationFn: async ({ legId, body }: { legId: number; body: string }): Promise<void> =>
      unwrapEmpty(
        await api.POST('/api/flightops/tours/{tourId}/legs/{legId}/issues', {
          params: { path: { tourId, legId } },
          body: { body },
        }),
      ),
  });
}

/** Withdraws a report still in the queue: its leg may be flown again and its flight is free. */
export function useWithdrawReport() {
  const changed = useReportsChanged();

  return useMutation({
    mutationFn: async (report: Pick<PirepDto, 'id' | 'rowVersion'>): Promise<PirepDto> =>
      unwrap(
        await api.POST('/api/flightops/reports/{id}/withdraw', {
          params: { path: { id: report.id } },
          body: { rowVersion: report.rowVersion },
        }),
      ),
    onSuccess: changed,
  });
}

// ---- the validation (T13b) ------------------------------------------------------------------------

export type ReviewQueueRowDto = components['schemas']['ReviewQueueRowDto'];
export type ReviewDto = components['schemas']['ReviewDto'];
export type ReviewErrorDto = components['schemas']['ReviewErrorDto'];
export type ReviewFlightDto = components['schemas']['ReviewFlightDto'];
export type ReviewPlanDto = components['schemas']['ReviewPlanDto'];
export type ReviewTrackDto = components['schemas']['ReviewTrackDto'];
export type ReviewWeatherDto = components['schemas']['ReviewWeatherDto'];
export type WeatherBulletinDto = components['schemas']['WeatherBulletinDto'];
export type ReviewEventDto = components['schemas']['ReviewEventDto'];
export type ReviewDisputeDto = components['schemas']['ReviewDisputeDto'];
export type ReviewCheckDto = components['schemas']['ReviewCheckDto'];
export type CheckOutcome = components['schemas']['CheckOutcome'];
export type EvidenceLine = components['schemas']['EvidenceLine'];
export type SuggestionDto = components['schemas']['SuggestionDto'];
export type MemberDto = components['schemas']['MemberDto'];
export type ReviewDecisionDto = components['schemas']['ReviewDecisionDto'];

/**
 * A row of the queue as the list draws it: the server's row, with the people and the route written out as the columns of
 * the generic list read them — a cell draws a value, not an object.
 */
export interface ReviewQueueRow extends ReviewQueueRowDto {
  readonly route: string;
  readonly pilotName: string;
  readonly assignedToName: string | null;
  /** `Open` while the rejection is disputed (T14b): the column draws it as a word, and nothing otherwise. */
  readonly dispute: 'Open' | null;
  /**
   * What the checks propose (T17), as a word: none failed, or failed with the outcome their suggested errors lead to; nothing
   * until they ran.
   */
  readonly checks: 'Clean' | 'Accept' | 'Reject' | null;
}

/** The checks of a row of the queue as one word (T17): the server counts and proposes, the list only names it. */
export function queueChecks(
  row: Pick<ReviewQueueRowDto, 'failedChecks' | 'checkSuggestion'>,
): ReviewQueueRow['checks'] {
  if (row.checkSuggestion === null) {
    return null;
  }

  return row.failedChecks === 0 ? 'Clean' : row.checkSuggestion === 'Rejected' ? 'Reject' : 'Accept';
}

/** A member as the staff reads them: the name the hub has, and the VID that always is. */
export function memberName(member: MemberDto): string {
  return member.name === null || member.name === '' ? String(member.vid) : `${member.name} (${member.vid})`;
}

const reviewKey = ['flightops', 'review'] as const;

/**
 * The queue (design M2 §4.1): every report not withdrawn, to anybody who may validate one tour; `tourId` narrows it to a
 * tour, `open: false` shows the decided ones. The order is the list's `sort` — `queuedAt` or `tourId`, the validator's
 * preference — and the server keeps the date inside each tour.
 */
export function reviewQueueQuery(
  search: ListSearch,
  filters: { tourId?: number; open: boolean; disputed?: boolean },
) {
  return queryOptions({
    queryKey: [...reviewKey, 'queue', search, filters] as const,
    queryFn: async (): Promise<Page<ReviewQueueRow>> => {
      const page = unwrap(
        await api.GET('/api/flightops/review/queue', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({
            open: String(filters.open),
            ...(filters.tourId === undefined ? {} : { tourId: String(filters.tourId) }),
            ...(filters.disputed === true ? { disputed: 'true' } : {}),
          }),
        }),
      );

      return {
        ...page,
        items: page.items.map((row) => ({
          ...row,
          route: `${row.departureIcao} → ${row.arrivalIcao}`,
          pilotName: memberName(row.pilot),
          assignedToName: row.assignedTo === null ? null : memberName(row.assignedTo),
          dispute: row.isDisputed ? ('Open' as const) : null,
          checks: queueChecks(row),
        })),
      };
    },
  });
}

/** The validation page of one report (§4.3): everything the decision needs, and what the reader may do on it now. */
export function reviewQuery(id: number) {
  return queryOptions({
    queryKey: [...reviewKey, 'one', id] as const,
    queryFn: async (): Promise<ReviewDto> =>
      unwrap(await api.GET('/api/flightops/review/{id}', { params: { path: { id } } })),
  });
}

/** The tracks of its flights, asked apart: they weigh, and only the map reads them. */
export function reviewTracksQuery(id: number) {
  return queryOptions({
    queryKey: [...reviewKey, 'tracks', id] as const,
    queryFn: async (): Promise<ReviewTrackDto[]> =>
      unwrap(await api.GET('/api/flightops/review/{id}/tracks', { params: { path: { id } } })),
    staleTime: Infinity,
  });
}

/**
 * What the system proposes for the errors being ticked (§4.3), asked of the server as they change: the rule — a dangerous
 * error, a warning over its yearly maximum — is written once, there.
 */
export function reviewSuggestionQuery(id: number, errorIds: readonly number[]) {
  const sorted = [...errorIds].sort((left, right) => left - right);

  return queryOptions({
    queryKey: [...reviewKey, 'suggestion', id, sorted] as const,
    queryFn: async (): Promise<SuggestionDto> =>
      unwrap(
        await api.GET('/api/flightops/review/{id}/suggestion', {
          params: { path: { id }, query: { errorIds: sorted } },
        }),
      ),
  });
}

/** The four steps of a review, each answering with the page as it is afterwards. */
export type ReviewStep =
  | { step: 'take' | 'release'; rowVersion: string }
  | { step: 'decide'; decision: ReviewDecisionDto }
  | { step: 'reopen'; reason: string; rowVersion: string }
  | { step: 'dispute'; upheld: boolean; answer: string; rowVersion: string };

export function useReviewStep(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (step: ReviewStep): Promise<ReviewDto> => {
      const path = { params: { path: { id } } };
      switch (step.step) {
        case 'take':
          return unwrap(
            await api.POST('/api/flightops/review/{id}/take', {
              ...path,
              body: { rowVersion: step.rowVersion },
            }),
          );
        case 'release':
          return unwrap(
            await api.POST('/api/flightops/review/{id}/release', {
              ...path,
              body: { rowVersion: step.rowVersion },
            }),
          );
        case 'decide':
          return unwrap(
            await api.POST('/api/flightops/review/{id}/decide', { ...path, body: step.decision }),
          );
        case 'reopen':
          return unwrap(
            await api.POST('/api/flightops/review/{id}/reopen', {
              ...path,
              body: { reason: step.reason, rowVersion: step.rowVersion },
            }),
          );
        case 'dispute':
          return unwrap(
            await api.POST('/api/flightops/review/{id}/dispute', {
              ...path,
              body: { upheld: step.upheld, answer: step.answer, rowVersion: step.rowVersion },
            }),
          );
      }
    },
    onSuccess: async (page) => {
      queryClient.setQueryData(reviewQuery(id).queryKey, page);
      await queryClient.invalidateQueries({ queryKey: [...reviewKey, 'queue'] });
    },
  });
}

// ---- the issues on the legs (T14b) ----------------------------------------------------------------

export type LegIssueDto = components['schemas']['LegIssueDto'];
export type LegIssueStatus = components['schemas']['LegIssueStatus'];

/** A row of the issues as the list draws it: the pilot and the leg written out, as a cell draws a value. */
export interface LegIssueRow extends LegIssueDto {
  readonly pilotName: string;
  readonly leg: string;
}

const legIssuesKey = ['flightops', 'legIssues'] as const;

export function legIssuesListQuery(search: ListSearch, filters: { open: boolean }) {
  return queryOptions({
    queryKey: [...legIssuesKey, 'list', search, filters] as const,
    queryFn: async (): Promise<Page<LegIssueRow>> => {
      const page = unwrap(
        await api.GET('/api/flightops/leg-issues', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(filters.open ? { status: 'Open' } : {}),
        }),
      );

      return {
        ...page,
        items: page.items.map((row) => ({
          ...row,
          pilotName: memberName(row.pilot),
          leg: row.legNumber === null ? (row.route ?? '') : `${row.legNumber} · ${row.route ?? ''}`,
        })),
      };
    },
  });
}

export function legIssueQuery(id: number) {
  return queryOptions({
    queryKey: [...legIssuesKey, 'one', id] as const,
    queryFn: async (): Promise<LegIssueDto> =>
      unwrap(await api.GET('/api/flightops/leg-issues/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function legIssueToFormValues(issue: LegIssueDto): LegIssueFormValues {
  return { status: issue.status, staffNote: issue.staffNote ?? '' };
}

/** Closes an issue, or opens it again, with a note for the rest of the staff. */
export function useSaveLegIssue(issue: LegIssueDto) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: LegIssueFormValues): Promise<LegIssueDto> =>
      unwrap(
        await api.PUT('/api/flightops/leg-issues/{id}', {
          params: { path: { id: String(issue.id) } },
          body: {
            status: values.status,
            staffNote: values.staffNote.trim() === '' ? null : values.staffNote.trim(),
            rowVersion: issue.rowVersion,
          },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: legIssuesKey });
    },
  });
}

// ---- the people of the tours (T15b) -------------------------------------------------------------

export type ValidatorsDto = components['schemas']['ValidatorsDto'];
export type ValidatorDto = components['schemas']['ValidatorDto'];
export type PilotPageDto = components['schemas']['PilotPageDto'];
export type BanDto = components['schemas']['BanDto'];
export type MyToursDto = components['schemas']['MyToursDto'];
export type StartedTourDto = components['schemas']['StartedTourDto'];
export type ProgressUnit = components['schemas']['ProgressUnit'];

const peopleKey = ['flightops', 'people'] as const;

/** The statistics of the validators in a calendar year, and who is enabled on what (design M2 §8.7). */
export function validatorsQuery(year: number) {
  return queryOptions({
    queryKey: [...peopleKey, 'validators', year] as const,
    queryFn: async (): Promise<ValidatorsDto> =>
      unwrap(await api.GET('/api/flightops/validators', { params: { query: { year } } })),
  });
}

/**
 * «Add a validator» and «remove»: a grant of the core written for the tours (T15a). A null tour is every tour. Whoever
 * receives it signs in again to have it — the core's stamp —, so nothing here is optimistic.
 */
export function useValidatorGrant() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (change: { add: boolean; vid: number; tourId: number | null }): Promise<void> => {
      if (change.add) {
        unwrapEmpty(
          await api.POST('/api/flightops/validators', {
            body: { vid: change.vid, tourId: change.tourId },
          }),
        );
        return;
      }

      unwrapEmpty(
        await api.DELETE('/api/flightops/validators/{vid}', {
          params: {
            path: { vid: change.vid },
            query: change.tourId === null ? {} : { tourId: change.tourId },
          },
        }),
      );
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: [...peopleKey, 'validators'] });
    },
  });
}

/** One pilot as the tours' staff read them, in a calendar year and ever; null when the hub knows nothing of them. */
export function pilotQuery(vid: number, year: number) {
  return queryOptions({
    queryKey: [...peopleKey, 'pilot', vid, year] as const,
    queryFn: async (): Promise<PilotPageDto | null> => {
      const result = await api.GET('/api/flightops/pilots/{vid}', {
        params: { path: { vid }, query: { year } },
      });
      return result.response.status === 404 ? null : unwrap(result);
    },
  });
}

/** A row of the bans as the list draws it: the pilot and the tour written out, as a cell draws a value. */
export interface BanRow extends BanDto {
  readonly pilotName: string;
  /** Every tour, or the one the next column names: a cell draws a word, not the absence of a tour. */
  readonly reach: 'All' | 'One';
}

const bansKey = ['flightops', 'bans'] as const;

export function bansListQuery(search: ListSearch, filters: { vid?: number } = {}) {
  return queryOptions({
    queryKey: [...bansKey, 'list', search, filters] as const,
    queryFn: async (): Promise<Page<BanRow>> => {
      const page = unwrap(
        await api.GET('/api/flightops/bans', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(filters.vid === undefined ? {} : { vid: String(filters.vid) }),
        }),
      );

      return {
        ...page,
        items: page.items.map((row) => ({
          ...row,
          pilotName: memberName(row.pilot),
          reach: row.tourId === null ? ('All' as const) : ('One' as const),
        })),
      };
    },
  });
}

export function banQuery(id: number) {
  return queryOptions({
    queryKey: [...bansKey, 'one', id] as const,
    queryFn: async (): Promise<BanDto> =>
      unwrap(await api.GET('/api/flightops/bans/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** A new ban starts now, on every tour, for good; `vid` is the pilot's page it was opened from, when it was. */
export function emptyBan(now: Date, vid?: number): BanFormValues {
  return {
    ...(vid === undefined ? {} : { vid }),
    startsAt: `${now.toISOString().slice(0, 16)}:00Z`,
    reason: '',
    rowVersion: NEW_ROW_VERSION,
  };
}

export function banToFormValues(ban: BanDto): BanFormValues {
  return {
    vid: ban.pilot.vid,
    ...(ban.tourId === null ? {} : { tourId: String(ban.tourId) }),
    startsAt: ban.startsAt,
    ...(ban.endsAt === null ? {} : { endsAt: ban.endsAt }),
    reason: ban.reason,
    rowVersion: ban.rowVersion,
  };
}

/** Writes a ban. Never deleted: lifting one early is moving its end (§3.9). */
export function useSaveBan(id: number | null) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: BanFormValues): Promise<BanDto> => {
      const body = {
        vid: values.vid ?? 0,
        tourId: values.tourId === undefined || values.tourId === '' ? null : Number(values.tourId),
        startsAt: values.startsAt ?? '',
        endsAt: values.endsAt ?? null,
        reason: values.reason,
        rowVersion: values.rowVersion,
      };

      return id === null
        ? unwrap(await api.POST('/api/flightops/bans', { body }))
        : unwrap(await api.PUT('/api/flightops/bans/{id}', { params: { path: { id: String(id) } }, body }));
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: bansKey });
      await queryClient.invalidateQueries({ queryKey: [...peopleKey, 'pilot'] });
    },
  });
}

/**
 * The signed-in pilot's tours (note 2026-09-24-le-pagine-delle-persone §3.1): the same answer the block `flightops.myTours`
 * gives, read by the cards of `/tours` to draw the pilot's progress on top of a card that is the same for everybody.
 */
export const myToursQuery = queryOptions({
  queryKey: ['flightops', 'myTours'] as const,
  queryFn: async (): Promise<MyToursDto> => unwrap(await api.GET('/api/flightops/my-tours')),
});
