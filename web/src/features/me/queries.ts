import { queryOptions, useMutation, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useRouter, type AnyRouter } from '@tanstack/react-router';

import type { Bootstrap } from '../../shared/api/bootstrap';
import { api, unwrap, unwrapEmpty } from '../../shared/api/client';
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
  // Everything, not only the bootstrap (issue #332, item 9): the personal blocks, the `/me` dashboard, the threads and the
  // tokens of the person who signed out are not the next person's to see, even for a moment on a shared computer.
  queryClient.clear();
  await router.invalidate();
}

/**
 * The bootstrap again, after a write of the back office that changes it — a module closed, a calendar kind, a grant, a super
 * administrator (issue #332, item 3). Invalidating the query only marked an answer nobody observes as stale: the root reads it
 * once in `beforeLoad`, so the badge, the menu and the forms kept the old one until the second navigation. Removed and the
 * router invalidated, as `sessionChanged` does for the session.
 */
export async function bootstrapChanged(queryClient: QueryClient, router: AnyRouter): Promise<void> {
  queryClient.removeQueries({ queryKey: bootstrapKey });
  await router.invalidate();
}

/** `bootstrapChanged`, bound to the application's query client and router. */
export function useBootstrapChanged(): () => Promise<void> {
  const queryClient = useQueryClient();
  const router = useRouter();
  return () => bootstrapChanged(queryClient, router);
}

/** Signs out and redraws the shell as anonymous, with no reload of the page. */
export function useLogout() {
  const queryClient = useQueryClient();
  const router = useRouter();

  return useMutation({
    // Unwrapped (issue #332, item 9): a refused sign-out is an error, and never runs the redraw as anonymous.
    mutationFn: async (): Promise<void> => unwrapEmpty(await api.POST('/auth/logout')),
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

/** The member's own link with Discord, and whether this installation offers one. */
export type DiscordLinkDto = components['schemas']['DiscordLinkDto'];

export const discordLinkKey = ['me', 'discord'] as const;

export const discordLinkQuery = queryOptions({
  queryKey: discordLinkKey,
  queryFn: async (): Promise<DiscordLinkDto> => unwrap(await api.GET('/api/me/discord')),
});

/** Forgets the link. Linking is not here: it is a round trip through Discord, started by a plain link. */
export function useUnlinkDiscord() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (): Promise<void> => {
      unwrapEmpty(await api.DELETE('/api/me/discord'));
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: discordLinkKey });
    },
  });
}

/**
 * One preference of the member asking, declared by a module (`IModule.Preferences`, M2 T4b); `null` when they never chose,
 * and the screen that asks has the default. Not in the bootstrap: a screen that needs one asks for it (T13b, the order of
 * the validators' queue, is the first).
 */
export function preferenceQuery(key: string) {
  return queryOptions({
    queryKey: ['me', 'preferences', key] as const,
    queryFn: async (): Promise<unknown> =>
      unwrap(await api.GET('/api/me/preferences/{key}', { params: { path: { key } } })).value ?? null,
  });
}

/** Keeps a preference on the member, so they find it on any computer. The value is whatever the module declared. */
export function useSavePreference(key: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (value: unknown): Promise<unknown> =>
      unwrap(await api.PUT('/api/me/preferences/{key}', { params: { path: { key } }, body: { value } }))
        .value ?? null,
    onSuccess: (saved) => {
      queryClient.setQueryData(['me', 'preferences', key], saved);
    },
  });
}
