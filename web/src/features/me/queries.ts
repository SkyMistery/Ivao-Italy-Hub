import { queryOptions, useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useRouter, type AnyRouter } from '@tanstack/react-router';

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

/**
 * The one thing to do when the session has changed under the application's feet: signed out here,
 * or expired and found out through a 401.
 *
 * ⚠️ Dropping the cached `/api/me` answer is only half of it, and for a while it was all that
 * happened — so the shell went on drawing the name of somebody who had just signed out. No screen
 * reads that query: the root route loads the bootstrap once in `beforeLoad` and hands it down as
 * router **context**, and that is what the header, the sidebar and every guard read. A query cache
 * does not re-run a `beforeLoad`; only `router.invalidate()` does, which re-runs the guards too.
 *
 * The answer is removed rather than invalidated because `ensureQueryData` hands back what is in
 * the cache: invalidating marks it stale and the root would still be given the old payload.
 */
export async function sessionChanged(queryClient: QueryClient, router: AnyRouter): Promise<void> {
  queryClient.removeQueries({ queryKey: bootstrapKey });
  await router.invalidate();
}

/** Signs out and redraws the shell as anonymous, with no reload of the page. */
export function useLogout() {
  const queryClient = useQueryClient();
  const router = useRouter();

  return useMutation({
    mutationFn: async () => {
      await api.POST('/auth/logout');
    },
    onSuccess: async () => {
      // Home first, and only then the redraw. A back office screen sits behind a guard that sends
      // anybody without a session to `/auth/login`, so redrawing where we stand would answer a
      // click on "sign out" with the login of IVAO — which still holds its own session and would
      // sign them straight back in.
      await router.navigate({ to: '/' });
      await sessionChanged(queryClient, router);
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
