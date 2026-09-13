import type { Department } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

import { publicContentQuery } from './queries';

/**
 * The slugs of the two personal dashboards, seeded from `seed/content-pages/me.json` and
 * `staff.json` (note 2026-09-13-le-dashboard-a-tutto-schermo §3.1). A department's dashboard has the
 * department's own code instead, and none of the nine is spelled like these two.
 */
export const PERSONAL_DASHBOARDS = { member: 'me', staff: 'staff' } as const;

/** One dashboard, as visitors read it: the published version of the row. */
export function dashboardQuery(slug: string) {
  return publicContentQuery('Dashboard', slug);
}

/** Where a dashboard is read, which is where its editor goes back to. */
export function dashboardAddress(slug: string, department: Department): string {
  if (slug === PERSONAL_DASHBOARDS.member) {
    return '/me';
  }

  return slug === PERSONAL_DASHBOARDS.staff ? '/staff' : `/staff/${deptParam.format(department)}`;
}
