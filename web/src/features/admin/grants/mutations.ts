import { useMutation, useQueryClient } from '@tanstack/react-query';

import { bootstrapKey } from '../../me/queries';
import { api, unwrap, unwrapEmpty } from '../../../shared/api/client';

import { grantKey, grantsKey, superadminsKey, type GrantDetailDto, type GrantWriteDto } from './queries';
import type { GrantFormValues } from './schema';

/**
 * Writing a grant, and changing who administers the system. One place turns what the form holds
 * into what the API expects — a text box has an empty string where the contract has a `null`.
 *
 * Every one of these invalidates the bootstrap as well as its own list: a grant changes what its
 * holder may do, and if the holder is the person doing the granting, the menus on screen are
 * already out of date by the time the call returns.
 */

/** An empty box is "not set", never an empty string. */
function orNull(value: string | undefined): string | null {
  const trimmed = value?.trim() ?? '';
  return trimmed === '' ? null : trimmed;
}

export function toWriteDto(values: GrantFormValues): GrantWriteDto {
  return {
    vid: values.vid,
    kind: values.kind,
    value: values.value,
    // No department at all means the permission is held on every one of them.
    department: values.department ?? null,
    effect: values.effect,
    expiresAt: orNull(values.expiresAt),
    reason: orNull(values.reason),
    rowVersion: values.rowVersion,
  };
}

/** The form as a new grant starts it. */
export function emptyGrant(): GrantFormValues {
  return {
    vid: 0,
    kind: 'Permission',
    value: '',
    department: undefined,
    effect: 'Grant',
    expiresAt: undefined,
    reason: undefined,
    // No version yet: the server reads an empty one as "the row as it is now", which for a create
    // is the only thing it can mean.
    rowVersion: '',
  };
}

export function toFormValues(grant: GrantDetailDto): GrantFormValues {
  return {
    vid: grant.vid,
    kind: grant.kind,
    value: grant.value,
    department: grant.department ?? undefined,
    effect: grant.effect,
    expiresAt: grant.expiresAt ?? undefined,
    reason: grant.reason ?? undefined,
    rowVersion: grant.rowVersion,
  };
}

function useInvalidate() {
  const queryClient = useQueryClient();

  return async () => {
    await queryClient.invalidateQueries({ queryKey: grantsKey });
    await queryClient.invalidateQueries({ queryKey: bootstrapKey });
  };
}

export function useCreateGrant() {
  const invalidate = useInvalidate();

  return useMutation({
    mutationFn: async (values: GrantFormValues): Promise<GrantDetailDto> =>
      unwrap(await api.POST('/api/admin/grants', { body: toWriteDto(values) })),
    onSuccess: invalidate,
  });
}

export function useUpdateGrant(id: number) {
  const queryClient = useQueryClient();
  const invalidate = useInvalidate();

  return useMutation({
    mutationFn: async (values: GrantFormValues): Promise<GrantDetailDto> =>
      unwrap(
        await api.PUT('/api/admin/grants/{id}', {
          params: { path: { id: String(id) } },
          body: toWriteDto(values),
        }),
      ),
    onSuccess: async (grant) => {
      queryClient.setQueryData(grantKey(id), grant);
      await invalidate();
    },
  });
}

export function useDeleteGrant() {
  const invalidate = useInvalidate();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/admin/grants/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: invalidate,
  });
}

export function useAddSuperadmin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vid: number): Promise<void> =>
      unwrapEmpty(await api.POST('/api/admin/superadmins/{vid}', { params: { path: { vid } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: superadminsKey });
      await queryClient.invalidateQueries({ queryKey: bootstrapKey });
    },
  });
}

export function useRemoveSuperadmin() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vid: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/admin/superadmins/{vid}', { params: { path: { vid } } })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: superadminsKey });
      await queryClient.invalidateQueries({ queryKey: bootstrapKey });
    },
  });
}
