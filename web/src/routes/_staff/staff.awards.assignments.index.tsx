import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { awardAssignmentColumns } from '../../features/awards/list';
import { awardAssignmentsListQuery } from '../../features/awards/queries';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/** Global: the register belongs to whoever assigns. */
const AWARDS_ASSIGN = 'Awards.Assign';

/**
 * The register of awards (M2, T4b): who received which, why, and who decided. An assignment is mostly
 * born from the queue; this screen also takes one written by hand, for an award nobody signalled.
 * The member sees their awards on their IVAO profile, never here (Carmine, 16 September 2026).
 */
export const Route = createFileRoute('/_staff/staff/awards/assignments/')({
  validateSearch: listSearchSchema,
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, AWARDS_ASSIGN)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(awardAssignmentsListQuery(deps)),
  component: AwardAssignmentsPage,
});

function AwardAssignmentsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  const newAssignment = (
    <Button asChild>
      <Link to="/staff/awards/assignments/$id" params={{ id: 'new' }} search={{}}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('awardAssignments.create')}
      </Link>
    </Button>
  );

  return (
    <PageShell
      title={t('awardAssignments.title')}
      description={t('awardAssignments.description')}
      breadcrumb={[{ label: t('awards.section') }, { label: t('awardAssignments.title') }]}
      actions={newAssignment}
    >
      <DataList
        columns={awardAssignmentColumns}
        query={awardAssignmentsListQuery(search)}
        labels="awardAssignments"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/awards/assignments/$id" params={{ id: String(row.id) }} search={{}}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={newAssignment}
      />
    </PageShell>
  );
}
