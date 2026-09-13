import { createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { DashboardScreen } from '../../features/content/DashboardScreen';
import { dashboardQuery } from '../../features/content/dashboards';
import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The home of a department: what somebody sees arriving at `/staff/<dept>` (design M1 §14, note
 * 2026-09-05-dashboard-di-dipartimento).
 *
 * It is **blocks and not tiles**, and that is the whole decision: a dashboard is a page the
 * department writes, and this hub already has a way of writing pages. The base and the tools are
 * given — one row per department, seeded from a template — and the arrangement is the department's,
 * in the editor it already uses. Nothing here knows what is on it.
 *
 * What it shows is the **published** version, like every other reader of content: the draft is what
 * the coordinator is working on, and their colleagues keep seeing the last thing that was
 * published. The row is `Visibility.Department`, so the query filter is what decides who may read
 * it — including the people a grant reached, which is what `HubClaims.BuildIdentity` was corrected
 * for in this phase. The screen is the one `/staff` and `/me` use (`DashboardScreen`).
 */
export const Route = createFileRoute('/_staff/staff/$dept/')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: ({ context, params }) =>
    // Not `ensureQueryData` alone: a department whose dashboard has never been published has no
    // page to read, and that is a state to draw rather than an error to throw.
    // The slug is the department's own code, in lower case.
    context.queryClient.ensureQueryData(dashboardQuery(params.dept.toLowerCase())).catch(() => null),
  component: DepartmentDashboard,
});

function DepartmentDashboard() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();

  return (
    <DashboardScreen
      bootstrap={bootstrap}
      slug={dept.toLowerCase()}
      title={t('dashboard.title', { department: t(`departments.${dept}`) })}
      description={t('dashboard.description')}
      missing={t('dashboard.missing')}
    />
  );
}
