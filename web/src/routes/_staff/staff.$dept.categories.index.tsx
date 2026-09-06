import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { categoryColumns } from '../../features/categories/list';
import { categoryListQuery } from '../../features/categories/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The vocabulary of one department: the shelves its news and its documents are filed under.
 *
 * It exists so that the category of a content row is a choice and not free text — without it two
 * editors write "Guides" and "guides" and the public site shows two shelves where there is one
 * (design M1 §3.4). The seed is empty on purpose: which shelves a division has is not something
 * code should know.
 *
 * The department and its guard are on the layout above.
 */
export const Route = createFileRoute('/_staff/staff/$dept/categories/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(categoryListQuery(params.dept, deps)),
  component: CategoriesPage,
});

function CategoriesPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  const create = (
    <Button asChild>
      <Link to="/staff/$dept/categories/$id" params={{ dept, id: 'new' }}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('categories.create')}
      </Link>
    </Button>
  );

  return (
    <PageShell
      title={t('categories.title')}
      description={t('categories.description')}
      breadcrumb={[{ label: dept }, { label: t('categories.title') }]}
      actions={create}
    >
      <DataList
        columns={categoryColumns}
        query={categoryListQuery(dept, search)}
        labels="categories"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/categories/$id" params={{ dept, id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
        emptyAction={create}
      />
    </PageShell>
  );
}
