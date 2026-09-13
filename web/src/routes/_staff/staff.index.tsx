import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { DashboardScreen } from '../../features/content/DashboardScreen';
import { PERSONAL_DASHBOARDS, dashboardQuery } from '../../features/content/dashboards';

/**
 * `/staff` is the dashboard of whoever is looking (note 2026-09-13-le-dashboard-a-tutto-schermo
 * §3.5.6): the seeded row `staff`, which the web team composes and which reads the same for
 * everybody — each block on it answers for the person asking. Until D3 it was a door that opened on
 * the first department; the departments are now one of its tiles, and the sidebar still leads to
 * each of them.
 */
export const Route = createFileRoute('/_staff/staff/')({
  loader: ({ context }) =>
    context.queryClient.ensureQueryData(dashboardQuery(PERSONAL_DASHBOARDS.staff)).catch(() => null),
  component: StaffDashboard,
});

function StaffDashboard() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();

  return (
    <DashboardScreen
      bootstrap={bootstrap}
      slug={PERSONAL_DASHBOARDS.staff}
      missing={t('dashboard.personalMissing')}
    />
  );
}
