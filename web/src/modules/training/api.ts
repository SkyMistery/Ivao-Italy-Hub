import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';

import { api, unwrap } from '../../shared/api/client';
import type { components } from '../../shared/api/schema';

import { settingsFromFormValues, type SettingsFormValues, type TrainingSettings } from './schemas';

/**
 * Every call the screens of the training make (M3, A4): the settings through the core's settings of a module, and what they
 * are chosen from — the ratings and the positions the division trains, which the module asks of the core.
 */

export type TrainingRatingDto = components['schemas']['TrainingRatingDto'];
export type TrainingPositionDto = components['schemas']['TrainingPositionDto'];

/** The key the module is known by on the server, in `/api/modules/{key}/settings`. */
export const MODULE_KEY = 'training';

const settingsKey = ['training', 'settings'] as const;

export function settingsQuery() {
  return queryOptions({
    queryKey: settingsKey,
    queryFn: async (): Promise<TrainingSettings> =>
      unwrap(
        await api.GET('/api/modules/{key}/settings', { params: { path: { key: MODULE_KEY } } }),
      ) as TrainingSettings,
  });
}

export function useSaveSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (values: SettingsFormValues): Promise<TrainingSettings> =>
      unwrap(
        await api.PUT('/api/modules/{key}/settings', {
          params: { path: { key: MODULE_KEY } },
          body: settingsFromFormValues(values),
        }),
      ) as TrainingSettings,
    onSuccess: (saved) => {
      queryClient.setQueryData(settingsKey, saved);
    },
  });
}

/** The ratings with a practical training, ladder by ladder (design M3 §1.7). They change with a release, not with a day. */
export function ratingsQuery() {
  return queryOptions({
    queryKey: ['training', 'ratings'] as const,
    queryFn: async (): Promise<TrainingRatingDto[]> => unwrap(await api.GET('/api/training/ratings')),
    staleTime: Infinity,
  });
}

/** The positions of the division those ratings are trained on, from the reference data of the night. */
export function positionsQuery() {
  return queryOptions({
    queryKey: ['training', 'positions'] as const,
    queryFn: async (): Promise<TrainingPositionDto[]> => unwrap(await api.GET('/api/training/positions')),
  });
}
