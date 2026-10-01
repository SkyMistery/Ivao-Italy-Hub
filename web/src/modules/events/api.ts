import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';

import type { EventsSettings, SettingsFormValues } from './schemas';

/**
 * Every call the screens of the events make (M4). E2, the skeleton: the settings, through the core's settings of a module.
 */

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'events';

const settingsKey = ['events', 'settings'] as const;

export function settingsQuery() {
  return queryOptions({
    queryKey: settingsKey,
    queryFn: async (): Promise<EventsSettings> =>
      unwrap(
        await api.GET('/api/modules/{key}/settings', { params: { path: { key: MODULE_KEY } } }),
      ) as EventsSettings,
  });
}

export function useSaveSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SettingsFormValues): Promise<EventsSettings> =>
      unwrap(
        await api.PUT('/api/modules/{key}/settings', {
          params: { path: { key: MODULE_KEY } },
          body: values,
        }),
      ) as EventsSettings,
    onSuccess: (saved) => {
      queryClient.setQueryData(settingsKey, saved);
    },
  });
}
