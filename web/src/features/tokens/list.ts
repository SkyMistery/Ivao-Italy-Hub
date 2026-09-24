import { col, type ColumnSpec } from '../../shared/list';

import type { PersonalTokenDto } from './queries';

/**
 * The columns of `/me/tokens` (M2, T19a). The audience is shown as it is written, like a permission's name: it is an
 * identifier (`flightops.agent`), the same in every language. The prefix is what tells two tokens apart.
 */
export const tokenColumns: readonly ColumnSpec<PersonalTokenDto>[] = [
  col.text('name', { sortable: true }),
  col.text('audience'),
  col.text('prefix'),
  col.date('createdAt', { sortable: true }),
  col.date('expiresAt', { sortable: true }),
  col.date('lastUsedAt', { sortable: true }),
];
