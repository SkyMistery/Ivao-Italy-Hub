import { createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { ConnectionCheckPanel } from '../../features/admin/connections/ConnectionCheckPanel';
import { PageShell } from '../../shared/ui';

/**
 * Whether the hub reaches its databases, tested on demand (note `2026-10-16-la-prova-dei-collegamenti`). Only a super
 * administrator: the server answers 403 to anybody else, and the guard here keeps the page from opening on a refusal.
 */
export const Route = createFileRoute('/_staff/staff/admin/connections')({
  beforeLoad: ({ context }) => {
    if (context.bootstrap.user?.isSuperadmin !== true) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: ConnectionsPage,
});

function ConnectionsPage() {
  const { t } = useTranslation();

  return (
    <PageShell
      title={t('connections.title')}
      description={t('connections.description')}
      breadcrumb={[{ label: t('admin.title') }, { label: t('connections.title') }]}
    >
      <ConnectionCheckPanel />
    </PageShell>
  );
}
