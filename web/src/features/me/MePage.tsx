import { useQuery } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { DashboardScreen } from '../content/DashboardScreen';
import { PERSONAL_DASHBOARDS } from '../content/dashboards';

import { NotificationPreferences } from './NotificationPreferences';
import { bootstrapQuery } from './queries';

/**
 * The member dashboard: the seeded row `me`, which the web team composes and which reads the same
 * for everybody — each block on it answers for the person looking (note
 * 2026-09-13-le-dashboard-a-tutto-schermo §3.1). It starts with the greeting alone; the modules
 * bring the rest as blocks.
 *
 * The notification preferences stay under it: they are a form of this person's, not a tile.
 */
export function MePage() {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);

  if (bootstrap === undefined) {
    return null;
  }

  return (
    <div className="flex flex-col gap-8">
      <DashboardScreen
        bootstrap={bootstrap}
        slug={PERSONAL_DASHBOARDS.member}
        missing={t('dashboard.personalMissing')}
      />
      {/* The member's threads with the departments (M2, T14a): a place of theirs, like the preferences under it. */}
      <p>
        <Link to="/me/contacts" className="text-primary underline">
          {t('contacts.mine.link')}
        </Link>
      </p>
      <NotificationPreferences />
    </div>
  );
}
