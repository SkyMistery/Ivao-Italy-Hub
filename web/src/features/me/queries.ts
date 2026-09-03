import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';

/**
 * Every feature exposes its calls as query options and mutations; components never fetch by hand.
 */
export const bootstrapKey = ['bootstrap'] as const;

export const bootstrapQuery = queryOptions({
  queryKey: bootstrapKey,
  staleTime: 60_000,
  queryFn: async (): Promise<Bootstrap> => unwrap(await api.GET('/api/me')),
});

/** Signs out and drops the cached bootstrap, so the shell redraws as anonymous. */
export function useLogout() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      await api.POST('/auth/logout');
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: bootstrapKey });
    },
  });
}
