import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { contentColumns } from '../../features/content/list';
import { contentListQuery } from '../../features/content/queries';
import { TemplatePicker } from '../../features/content/TemplatePicker';
import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * Recipe 2 (design M0 §7.3), the second list of the hub: paging, sorting and searching are the
 * typed search parameters of the route, and there is no table markup here — the columns are
 * declared in `features/content/list.ts` and drawn by `DataList`.
 *
 * Templates are not in this list. The server keeps them out unless a caller asks for them, and the
 * one caller that does is the picker below (`CrudOptions.DefaultFilters`).
 */
export const Route = createFileRoute('/_staff/staff/$dept/content')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(contentListQuery(params.dept, deps)),
  component: ContentPage,
});

function ContentPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('content.title')}
      description={t('content.description')}
      breadcrumb={[{ label: dept }, { label: t('content.title') }]}
      actions={
        <Button asChild>
          <Link to="/staff/$dept/content/$id" params={{ dept, id: 'new' }}>
            <Plus aria-hidden className="mr-2 size-4" />
            {t('content.create')}
          </Link>
        </Button>
      }
    >
      <div className="flex flex-col gap-6">
        <TemplatePicker
          department={dept}
          onCreated={(id) =>
            void navigate({ to: '/staff/$dept/content/$id', params: { dept, id: String(id) } })
          }
        />

        <DataList
          columns={contentColumns}
          query={contentListQuery(dept, search)}
          labels="content"
          locale={i18n.language}
          defaultLocale={division.defaultLocale}
          timezone={division.timezone}
          search={search}
          onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
          actions={(row) => (
            <Button asChild variant="ghost" size="sm">
              <Link to="/staff/$dept/content/$id" params={{ dept, id: String(row.id) }}>
                {t('common.edit')}
              </Link>
            </Button>
          )}
          emptyAction={
            <Button asChild>
              <Link to="/staff/$dept/content/$id" params={{ dept, id: 'new' }}>
                {t('content.create')}
              </Link>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
