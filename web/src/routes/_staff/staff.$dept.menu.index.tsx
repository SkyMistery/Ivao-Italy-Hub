import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { menuColumns } from '../../features/menu/list';
import { menuListQuery } from '../../features/menu/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The menu of the site, as a list like any other.
 *
 * This screen is the answer to the question M1 exists to ask: the navigation of a division site is
 * editorial, so it is rows somebody edits here and not an array in the code — take an entry out and
 * it leaves the site on the next request, with nothing recompiled (design M1 §8.1).
 *
 * The department and its guard are on the layout above.
 */
export const Route = createFileRoute('/_staff/staff/$dept/menu/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(menuListQuery(deps)),
  component: MenuPage,
});

function MenuPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  const create = (
    <Button asChild>
      <Link to="/staff/$dept/menu/$id" params={{ dept, id: 'new' }}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('menu.create')}
      </Link>
    </Button>
  );

  return (
    <PageShell
      title={t('menu.title')}
      description={t('menu.description')}
      breadcrumb={[{ label: dept }, { label: t('menu.title') }]}
      actions={create}
    >
      <DataList
        columns={menuColumns}
        query={menuListQuery(search)}
        labels="menu"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/menu/$id" params={{ dept, id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={create}
      />
    </PageShell>
  );
}
