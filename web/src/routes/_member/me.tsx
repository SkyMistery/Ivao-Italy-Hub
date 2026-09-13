import { createFileRoute } from '@tanstack/react-router';

import { PERSONAL_DASHBOARDS, dashboardQuery } from '../../features/content/dashboards';
import { MePage } from '../../features/me/MePage';

export const Route = createFileRoute('/_member/me')({
  // A dashboard is not a page: it takes the whole width between header and footer (note
  // 2026-09-13-le-dashboard-a-tutto-schermo §3.2).
  staticData: { wide: true },
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(dashboardQuery(PERSONAL_DASHBOARDS.member)).catch(() => null),
  component: MePage,
});
