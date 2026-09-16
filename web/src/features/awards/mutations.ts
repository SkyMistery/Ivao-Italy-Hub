import { useMutation, useQueryClient } from '@tanstack/react-query';

import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';

import {
  awardAssignmentsKey,
  awardSignalsKey,
  awardsKey,
  type AwardAssignmentDetailDto,
  type AwardAssignmentWriteDto,
  type AwardDetailDto,
  type AwardSignalDetailDto,
  type AwardSignalStatus,
  type AwardWriteDto,
} from './queries';
import type { AwardAssignmentFormValues, AwardFormValues } from './schema';

/**
 * Writing awards, assignments and lines of the queue, and the one place that turns what a form holds
 * into what the API expects (design M0 §7.5).
 */

/** A translated field with nothing written in it is absent, not an object full of empty strings. */
function trimLocalized(value: Record<string, string>): LocalizedString | null {
  const written = Object.entries(value).filter(([, text]) => text.trim().length > 0);
  return written.length === 0 ? null : Object.fromEntries(written);
}

function spread(value: LocalizedString | null, locales: readonly string[]): Record<string, string> {
  return Object.fromEntries(locales.map((locale) => [locale, value?.[locale] ?? '']));
}

// ---- the catalogue -------------------------------------------------------------------------------

export function toAwardWriteDto(values: AwardFormValues): AwardWriteDto {
  return {
    ownerDepartment: values.ownerDepartment,
    // Sent as it stands, so the server can say which language is missing.
    name: values.name,
    description: trimLocalized(values.description),
    criteria: trimLocalized(values.criteria),
    imageMediaId: values.imageMediaId ?? null,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

export function emptyAward(department: Department, locales: readonly string[]): AwardFormValues {
  return {
    ownerDepartment: department,
    name: spread(null, locales),
    description: spread(null, locales),
    criteria: spread(null, locales),
    isActive: true,
    rowVersion: NEW_ROW_VERSION,
  };
}

export function awardToFormValues(award: AwardDetailDto, locales: readonly string[]): AwardFormValues {
  return {
    ownerDepartment: award.ownerDepartment,
    name: spread(award.name, locales),
    description: spread(award.description, locales),
    criteria: spread(award.criteria, locales),
    ...(award.imageMediaId === null ? {} : { imageMediaId: award.imageMediaId }),
    isActive: award.isActive,
    rowVersion: award.rowVersion,
  };
}

export function useCreateAward() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AwardFormValues): Promise<AwardDetailDto> =>
      unwrap(await api.POST('/api/awards', { body: toAwardWriteDto(values) })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: awardsKey });
    },
  });
}

export function useUpdateAward(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AwardFormValues): Promise<AwardDetailDto> =>
      unwrap(
        await api.PUT('/api/awards/{id}', {
          params: { path: { id: String(id) } },
          body: toAwardWriteDto(values),
        }),
      ),
    onSuccess: async (award) => {
      queryClient.setQueryData([...awardsKey, 'detail', id], award);
      await queryClient.invalidateQueries({ queryKey: awardsKey });
    },
  });
}

export function useDeleteAward() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/awards/{id}', { params: { path: { id: String(id) } } })),
    // Not awaited: the screen that deleted is still observing the row it deleted (see the links).
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: awardsKey });
    },
  });
}

// ---- the register --------------------------------------------------------------------------------

export function toAssignmentWriteDto(values: AwardAssignmentFormValues): AwardAssignmentWriteDto {
  return {
    awardId: Number(values.awardId),
    vid: values.vid,
    reason: values.reason.trim(),
    signalId: values.signalId ?? null,
    rowVersion: values.rowVersion,
  };
}

/**
 * The form as a new assignment starts it: empty, or filled from the line of the queue it answers —
 * the member, the reason and the award the row proposed, which whoever assigns may still change.
 */
export function emptyAssignment(signal: AwardSignalDetailDto | null): AwardAssignmentFormValues {
  return {
    awardId: signal?.awardId === null || signal === null ? '' : String(signal.awardId),
    vid: signal?.vid ?? 0,
    reason: signal?.reason ?? '',
    ...(signal === null ? {} : { signalId: signal.id }),
    rowVersion: NEW_ROW_VERSION,
  };
}

export function assignmentToFormValues(assignment: AwardAssignmentDetailDto): AwardAssignmentFormValues {
  return {
    awardId: String(assignment.awardId),
    vid: assignment.vid,
    reason: assignment.reason,
    ...(assignment.signalId === null ? {} : { signalId: assignment.signalId }),
    rowVersion: assignment.rowVersion,
  };
}

export function useCreateAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AwardAssignmentFormValues): Promise<AwardAssignmentDetailDto> =>
      unwrap(await api.POST('/api/award-assignments', { body: toAssignmentWriteDto(values) })),
    // An assignment that answered the queue handled a line of it, in the same save.
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: awardAssignmentsKey });
      await queryClient.invalidateQueries({ queryKey: awardSignalsKey });
    },
  });
}

export function useUpdateAssignment(id: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: AwardAssignmentFormValues): Promise<AwardAssignmentDetailDto> =>
      unwrap(
        await api.PUT('/api/award-assignments/{id}', {
          params: { path: { id: String(id) } },
          body: toAssignmentWriteDto(values),
        }),
      ),
    onSuccess: async (assignment) => {
      queryClient.setQueryData([...awardAssignmentsKey, 'detail', id], assignment);
      await queryClient.invalidateQueries({ queryKey: awardAssignmentsKey });
    },
  });
}

/** Revoking: the row goes, and the audit log keeps what it was. */
export function useDeleteAssignment() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (id: number): Promise<void> =>
      unwrapEmpty(await api.DELETE('/api/award-assignments/{id}', { params: { path: { id: String(id) } } })),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: awardAssignmentsKey });
    },
  });
}

// ---- the queue -----------------------------------------------------------------------------------

/** Dismissing a line, or putting a dismissed one back. Handled is written by an assignment only. */
export function useMoveSignal() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({
      id,
      status,
    }: {
      id: number;
      status: AwardSignalStatus;
    }): Promise<AwardSignalDetailDto> =>
      unwrap(
        await api.PUT('/api/award-signals/{id}', { params: { path: { id: String(id) } }, body: { status } }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: awardSignalsKey });
    },
  });
}
