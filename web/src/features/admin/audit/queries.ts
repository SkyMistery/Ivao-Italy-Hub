import { queryOptions } from '@tanstack/react-query';

import { api, unwrap } from '../../../shared/api/client';
import type { components } from '../../../shared/api/schema';
import { col, listQuerySerializer, toQuery, type ColumnSpec, type ListSearch } from '../../../shared/list';

/**
 * The audit log, read only. The rows are written by the save changes interceptor and by nothing
 * else, which is why the resource has no write at all: `ReadOnly = true` on the server maps the two
 * reads and stops there (design M0 §3.9).
 */

export type AuditListDto = components['schemas']['AuditListDto'];
export type AuditPage = components['schemas']['PagedResultOfAuditListDto'];

export const auditKey = ['audit'] as const;

export function auditListKey(search: ListSearch) {
  return [...auditKey, 'list', search] as const;
}

export function auditListQuery(search: ListSearch) {
  return queryOptions({
    queryKey: auditListKey(search),
    queryFn: async (): Promise<AuditPage> =>
      unwrap(
        await api.GET('/api/admin/audit', {
          params: { query: toQuery(search) },
          querySerializer: listQuerySerializer({}),
        }),
      ),
  });
}

export const auditColumns: readonly ColumnSpec<AuditListDto>[] = [
  col.date('at', { sortable: true }),
  col.number('vid', { sortable: true }),
  col.text('action'),
  col.text('entity', { sortable: true }),
  col.text('entityId'),
  col.boolean('isSuperadmin'),
  col.text('ip'),
];
