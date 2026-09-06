import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

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

/** One kind of notification and whether this member wants it; the server declares which exist. */
export type NotificationPreferenceDto = components['schemas']['NotificationPreferenceDto'];

export const notificationPreferencesKey = ['me', 'notifications'] as const;

export const notificationPreferencesQuery = queryOptions({
  queryKey: notificationPreferencesKey,
  queryFn: async (): Promise<NotificationPreferenceDto[]> => unwrap(await api.GET('/api/me/notifications')),
});

/** Switching one on or off. It saves itself: there is no form around it to submit. */
export function useSaveNotificationPreference() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (preference: NotificationPreferenceDto): Promise<NotificationPreferenceDto> =>
      unwrap(await api.PUT('/api/me/notifications', { body: preference })),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: notificationPreferencesKey });
    },
  });
}
