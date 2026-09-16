import { queryOptions } from '@tanstack/react-query';

import type { Department } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';
import { listQuerySerializer, toQuery, type ListSearch } from '../../shared/list';

/**
 * Every call the awards screens make, as query options (design M0 §7.4). Three resources of the CRUD
 * engine (M2, T4b): the catalogue, departmental like the links; the register and the queue, global
 * behind `Awards.Assign`.
 */

export type AwardListDto = components['schemas']['AwardListDto'];
export type AwardDetailDto = components['schemas']['AwardDetailDto'];
export type AwardWriteDto = components['schemas']['AwardWriteDto'];
export type AwardPage = components['schemas']['PagedResultOfAwardListDto'];

export type AwardAssignmentListDto = components['schemas']['AwardAssignmentListDto'];
export type AwardAssignmentDetailDto = components['schemas']['AwardAssignmentDetailDto'];
export type AwardAssignmentWriteDto = components['schemas']['AwardAssignmentWriteDto'];
export type AwardAssignmentPage = components['schemas']['PagedResultOfAwardAssignmentListDto'];

export type AwardSignalListDto = components['schemas']['AwardSignalListDto'];
export type AwardSignalDetailDto = components['schemas']['AwardSignalDetailDto'];
export type AwardSignalStatus = components['schemas']['AwardSignalStatus'];
export type AwardSignalPage = components['schemas']['PagedResultOfAwardSignalListDto'];

export const AWARD_SIGNAL_STATUSES = [
  'Pending',
  'Dismissed',
  'Handled',
] as const satisfies readonly AwardSignalStatus[];

export const awardsKey = ['awards'] as const;
export const awardAssignmentsKey = ['award-assignments'] as const;
export const awardSignalsKey = ['award-signals'] as const;

/** One page of the catalogue: of one department when it is given, otherwise every award. */
export function awardsListQuery(department: Department | undefined, search: ListSearch) {
  return queryOptions({
    queryKey: [...awardsKey, 'list', department, search] as const,
    queryFn: async (): Promise<AwardPage> =>
      unwrap(
        await api.GET('/api/awards', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(
            department === undefined ? {} : { ownerDepartment: department },
          ),
        }),
      ),
  });
}

/**
 * The awards somebody may choose when assigning: the active ones, whichever department wrote them.
 * A page of a hundred, the ceiling of the list engine — a division with more active awards than that
 * would need the field to ask the server as it is typed, as the menu's addresses do.
 */
export function activeAwardsQuery() {
  return queryOptions({
    queryKey: [...awardsKey, 'active'] as const,
    queryFn: async (): Promise<AwardPage> =>
      unwrap(
        await api.GET('/api/awards', {
          params: { query: { page: 1, pageSize: 100 } },
          querySerializer: listQuerySerializer({ isActive: 'true' }),
        }),
      ),
  });
}

export function awardQuery(id: number) {
  return queryOptions({
    queryKey: [...awardsKey, 'detail', id] as const,
    queryFn: async (): Promise<AwardDetailDto> =>
      unwrap(await api.GET('/api/awards/{id}', { params: { path: { id: String(id) } } })),
  });
}

export function awardAssignmentsListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: [...awardAssignmentsKey, 'list', search] as const,
    queryFn: async (): Promise<AwardAssignmentPage> =>
      unwrap(
        await api.GET('/api/award-assignments', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export function awardAssignmentQuery(id: number) {
  return queryOptions({
    queryKey: [...awardAssignmentsKey, 'detail', id] as const,
    queryFn: async (): Promise<AwardAssignmentDetailDto> =>
      unwrap(await api.GET('/api/award-assignments/{id}', { params: { path: { id: String(id) } } })),
  });
}

/** The queue, waiting lines by default: the server applies that filter unless the status is named. */
export function awardSignalsListQuery(status: AwardSignalStatus | undefined, search: ListSearch) {
  return queryOptions({
    queryKey: [...awardSignalsKey, 'list', status, search] as const,
    queryFn: async (): Promise<AwardSignalPage> =>
      unwrap(
        await api.GET('/api/award-signals', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer(status === undefined ? {} : { status }),
        }),
      ),
  });
}

export function awardSignalQuery(id: number) {
  return queryOptions({
    queryKey: [...awardSignalsKey, 'detail', id] as const,
    queryFn: async (): Promise<AwardSignalDetailDto> =>
      unwrap(await api.GET('/api/award-signals/{id}', { params: { path: { id: String(id) } } })),
  });
}
