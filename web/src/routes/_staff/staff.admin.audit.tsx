import { createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { auditColumns, auditListQuery } from '../../features/admin/audit/queries';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * What happened, newest first. Recipe 2 again, on a resource with no write at all: the rows are
 * written by the save changes interceptor and by nothing else, and the server maps only the reads.
 *
 * `dir: 'desc'` is the default of this route rather than of the engine. An audit log is read from
 * the top; every other list of the hub is read from the beginning.
 */
const AUDIT_VIEW = 'Audit.View';

/**
 * The five parameters of every list, with one default changed. The engine orders ascending unless
 * asked otherwise, and that is right for every other resource; this one is read from the top.
 */
const auditSearchSchema = listSearchSchema.extend({
  dir: z.enum(['asc', 'desc']).default('desc'),
});

export const Route = createFileRoute('/_staff/staff/admin/audit')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, AUDIT_VIEW)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  validateSearch: auditSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(auditListQuery(deps)),
  component: AuditPage,
});

function AuditPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('audit.title')}
      description={t('audit.description')}
      breadcrumb={[{ label: t('admin.title') }, { label: t('audit.title') }]}
    >
      <DataList
        columns={auditColumns}
        query={auditListQuery(search)}
        labels="audit"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
      />
    </PageShell>
  );
}
