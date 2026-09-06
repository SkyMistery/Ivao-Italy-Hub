import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { calendarColumns } from '../../features/calendar/list';
import { calendarListQuery } from '../../features/calendar/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The calendar of one department: the entries the staff wrote and the entries a module projected,
 * in the one table they share (plan §9.5).
 *
 * ⚠️ A projected entry is shown and **cannot be edited** — it mirrors a row somebody else owns, and
 * a change to it would be undone at the next save of that row. The `isProjection` column says so,
 * and the row's own edit link is not offered: a button that always answers 403 teaches people to
 * ignore buttons (design M1 §4).
 *
 * The department and its guard are on the layout above.
 */
export const Route = createFileRoute('/_staff/staff/$dept/calendar/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(calendarListQuery(params.dept, deps)),
  component: CalendarPage,
});

function CalendarPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  const create = (
    <Button asChild>
      <Link to="/staff/$dept/calendar/$id" params={{ dept, id: 'new' }}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('calendar.create')}
      </Link>
    </Button>
  );

  return (
    <PageShell
      title={t('calendar.title')}
      description={t('calendar.description')}
      breadcrumb={[{ label: dept }, { label: t('calendar.title') }]}
      actions={create}
    >
      <DataList
        columns={calendarColumns}
        query={calendarListQuery(dept, search)}
        labels="calendar"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) =>
          row.isProjection ? (
            <span className="text-muted-foreground text-sm">{t('calendar.projected')}</span>
          ) : (
            <Button asChild variant="ghost" size="sm">
              <Link to="/staff/$dept/calendar/$id" params={{ dept, id: String(row.id) }}>
                {t('common.edit')}
              </Link>
            </Button>
          )
        }
        emptyAction={create}
      />
    </PageShell>
  );
}
