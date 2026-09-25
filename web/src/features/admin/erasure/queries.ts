import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

/**
 * Erasing a person's data (T20b, note `2026-09-25-la-cancellazione-dei-dati-di-una-persona`): what it would do, and doing
 * it. Only a super administrator is answered; everybody else gets 403.
 */

export type ErasurePreviewDto = components['schemas']['ErasurePreviewDto'];
export type ErasureResultDto = components['schemas']['ErasureResultDto'];
export type ErasureLine = components['schemas']['ErasureLine'];

export const erasureKey = ['erasure'] as const;

export function erasurePreviewQuery(vid: number) {
  return queryOptions({
    queryKey: [...erasureKey, vid] as const,
    queryFn: async (): Promise<ErasurePreviewDto> =>
      unwrap(await api.GET('/api/admin/erasure/{vid}', { params: { path: { vid } } })),
  });
}

export function useErase() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (vid: number): Promise<ErasureResultDto> =>
      unwrap(await api.POST('/api/admin/erasure/{vid}', { params: { path: { vid } } })),
    // What the preview said is not true any more; asked again, it says there is nothing left.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: erasureKey }),
  });
}
