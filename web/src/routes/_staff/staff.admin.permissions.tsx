import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { grantColumns } from '../../features/admin/grants/list';
import { grantsListQuery } from '../../features/admin/grants/queries';
import { SuperadminPanel } from '../../features/admin/grants/SuperadminPanel';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * Who holds what, by name. Recipe 2 of design M0 §7.3 with one thing missing on purpose: there is
 * no department in the address, because a grant belongs to no department. It is the first screen of
 * the hub on the CRUD engine's global mode, and it looks exactly like the departmental ones — which
 * is the point of having one engine.
 */
const PERMISSIONS_MANAGE = 'Permissions.Manage';

export const Route = createFileRoute('/_staff/staff/admin/permissions')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, PERMISSIONS_MANAGE)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(grantsListQuery(deps)),
  component: PermissionsPage,
});

function PermissionsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('grants.title')}
      description={t('grants.description')}
      breadcrumb={[{ label: t('admin.title') }, { label: t('grants.title') }]}
      actions={
        <Button asChild>
          <Link to="/staff/admin/permissions/$id" params={{ id: 'new' }}>
            <Plus aria-hidden className="mr-2 size-4" />
            {t('grants.create')}
          </Link>
        </Button>
      }
    >
      <DataList
        columns={grantColumns}
        query={grantsListQuery(search)}
        labels="grants"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/admin/permissions/$id" params={{ id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={
          <Button asChild>
            <Link to="/staff/admin/permissions/$id" params={{ id: 'new' }}>
              {t('grants.create')}
            </Link>
          </Button>
        }
      />

      {bootstrap.user?.isSuperadmin === true ? <SuperadminPanel locales={division.locales} /> : null}
    </PageShell>
  );
}
