import { useMutation } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';

/**
 * Whether the hub reaches its two databases (note `2026-10-16-la-prova-dei-collegamenti`): a test somebody runs, so a
 * mutation and not a query — nothing is cached, nothing runs until the button is pressed. Only a super administrator is
 * answered; everybody else gets 403.
 */

export type ConnectionCheckDto = components['schemas']['ConnectionCheckResponse'];
export type HubDatabaseCheck = components['schemas']['HubDatabaseCheck'];
export type ArchiveCheck = components['schemas']['ArchiveCheck'];

export function useConnectionCheck() {
  return useMutation({
    mutationFn: async (): Promise<ConnectionCheckDto> =>
      unwrap(await api.POST('/api/admin/diagnostics/connections')),
  });
}
