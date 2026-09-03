import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { linkColumns } from '../../features/links/list';
import { linksListQuery } from '../../features/links/queries';
import { deptParam } from '../../shared/api/department';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * Recipe 2 (design M0 §7.3): a list whose paging, sorting and searching are the typed search
 * parameters of the route, so the state of the screen is the URL. The loader fills the cache before
 * the component renders, which is why the table does not flash empty on a back button.
 *
 * There is no table markup in this file and no cell renderer: the columns are declared in
 * `features/links/list.ts` and drawn by `DataList`. That is the whole point of the phase.
 */
export const Route = createFileRoute('/_staff/staff/$dept/links')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(linksListQuery(params.dept, deps)),
  component: LinksPage,
});

function LinksPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('links.title')}
      description={t('links.description')}
      breadcrumb={[{ label: dept }, { label: t('links.title') }]}
      actions={
        <Button asChild>
          <Link to="/staff/$dept/links/$id" params={{ dept, id: 'new' }}>
            <Plus aria-hidden className="mr-2 size-4" />
            {t('links.create')}
          </Link>
        </Button>
      }
    >
      <DataList
        columns={linkColumns}
        query={linksListQuery(dept, search)}
        labels="links"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/links/$id" params={{ dept, id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={
          <Button asChild>
            <Link to="/staff/$dept/links/$id" params={{ dept, id: 'new' }}>
              {t('links.create')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
