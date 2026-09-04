import { useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrapEmpty } from '../../../shared/api/client';
import { bootstrapKey } from '../../me/queries';

/**
 * Closing a module for changes, and opening it again.
 *
 * There is no query next to this mutation, and that is on purpose: which modules exist, which are
 * enabled and which are closed is part of `GET /api/me`, because the client needs it in order to
 * draw itself anyway. A second endpoint answering the same question would be a second thing to keep
 * in step (plan §16.7). So the screen reads the bootstrap, and this invalidates it.
 */
export function useSetModuleMaintenance() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ key, maintenance }: { key: string; maintenance: boolean }): Promise<void> =>
      unwrapEmpty(
        await api.PUT('/api/admin/modules/{key}/maintenance', {
          params: { path: { key } },
          body: { maintenance },
        }),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: bootstrapKey });
    },
  });
}
