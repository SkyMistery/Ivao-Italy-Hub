import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { calendarKindColumns } from '../../features/admin/calendarKinds/list';
import { calendarKindsListQuery } from '../../features/admin/calendarKinds/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The words the whole division files its calendar under. Recipe 2 of design M0 §7.3, and the second
 * screen of the hub with no department in its address — the permissions were the first.
 *
 * It sits under `/staff/admin` and not under a department for the reason the vocabulary exists at
 * all: a kind is the same for everybody, decided centrally, and a coordinator picks from it rather
 * than inventing one (`decisions/2026-09-08-tipi-di-evento-di-divisione.md`).
 *
 * The guard is on the layout above; the entry only appears in the sidebar for whoever holds
 * `Calendar.ManageKinds`, which is a global permission.
 */
export const Route = createFileRoute('/_staff/staff/admin/calendar-kinds/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(calendarKindsListQuery(deps)),
  component: CalendarKindsPage,
});

function CalendarKindsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('calendarKinds.title')}
      description={t('calendarKinds.description')}
      breadcrumb={[{ label: t('admin.title') }, { label: t('calendarKinds.title') }]}
      actions={
        <Button asChild>
          <Link to="/staff/admin/calendar-kinds/$id" params={{ id: 'new' }}>
            <Plus aria-hidden className="mr-2 size-4" />
            {t('calendarKinds.create')}
          </Link>
        </Button>
      }
    >
      <DataList
        columns={calendarKindColumns}
        query={calendarKindsListQuery(search)}
        labels="calendarKinds"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/admin/calendar-kinds/$id" params={{ id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={
          <Button asChild>
            <Link to="/staff/admin/calendar-kinds/$id" params={{ id: 'new' }}>
              {t('calendarKinds.create')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
