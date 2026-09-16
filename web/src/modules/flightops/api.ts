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
} from './schemas';

/**
 * Every call the screens of the tours make (M2, T5), as query options and mutations: the aircraft data
 * through the CRUD engine, the settings through the core's settings of a module.
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
