import { Badge, Button, Subtle } from '@ivao/atmosphere-react';
import { createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { useSetModuleMaintenance } from '../../features/admin/modules/mutations';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { DepartmentBadge, EmptyState, PageShell } from '../../shared/ui';

/**
 * The modules of this build, and the one switch there is on each: closed for changes, or open.
 *
 * The list is the bootstrap's, not a fetch of its own — `/api/me` already carries it, because the
 * client needs to know which modules are on in order to draw itself (plan §16.7). A module the
 * division switched off in `division.json` is shown too, greyed: it is compiled in and silent, and
 * saying so is the difference between "we do not have it" and "we turned it off".
 *
 * No table component here: three facts and a button per row is a list, and Atmosphere's `DataTable`
 * is for a paged, sorted, server side list. `DataList` would need a resource behind it, and there
 * is deliberately none.
 */
const MODULES_MANAGE = 'Modules.Manage';

export const Route = createFileRoute('/_staff/staff/admin/modules')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, MODULES_MANAGE)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: ModulesPage,
});

function ModulesPage() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const setMaintenance = useSetModuleMaintenance();

  const modules = bootstrap.modules;

  return (
    <PageShell
      title={t('modules.title')}
      description={t('modules.description')}
      breadcrumb={[{ label: t('admin.title') }, { label: t('modules.title') }]}
    >
      {modules.length === 0 ? (
        <EmptyState title={t('modules.empty.title')} description={t('modules.empty.description')} />
      ) : (
        <ul className="flex flex-col gap-3">
          {modules.map((module) => (
            <li
              key={module.key}
              className="bg-card text-card-foreground border-border flex flex-wrap items-center gap-3 rounded-lg border p-4"
            >
              <span className="font-medium">{module.key}</span>

              {module.department === null ? null : <DepartmentBadge department={module.department} />}

              {module.enabled ? null : <Badge variant="flat" text={t('modules.disabled')} />}

              {module.maintenance ? <Badge variant="leaked" text={t('modules.closed')} /> : null}

              <div className="ml-auto flex items-center gap-3">
                <Subtle>{module.maintenance ? t('modules.closedHint') : t('modules.openHint')}</Subtle>
                <Button
                  variant={module.maintenance ? 'primary' : 'secondary'}
                  disabled={!module.enabled || setMaintenance.isPending}
                  onClick={() => setMaintenance.mutate({ key: module.key, maintenance: !module.maintenance })}
                >
                  {module.maintenance ? t('modules.open') : t('modules.close')}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </PageShell>
  );
}
