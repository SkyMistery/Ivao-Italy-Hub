import { Button, Label, Select } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { linkColumns } from '../../features/links/list';
import { linksListQuery, type LinkListDto } from '../../features/links/queries';
import {
  holdsPermission,
  reachableDepartments,
  writableDepartments,
  type Department,
} from '../../shared/api/bootstrap';
import { DataList, ListFilter, col, departmentListSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/** What writing a link asks for, on the department of the row. */
const LINKS_EDIT = 'Links.Edit';

/**
 * Recipe 2 (design M0 §7.3): a list whose paging, sorting, searching and department are the typed
 * search parameters of the route, so the state of the screen is the URL. The loader fills the cache
 * before the component renders, which is why the table does not flash empty on a back button.
 *
 * There is no table markup in this file and no cell renderer: the columns are declared in
 * `features/links/list.ts` and drawn by `DataList`.
 *
 * The list holds every link this person may read (note 2026-09-13-contenuti-centralizzati, 3.4): a
 * public link of another department is there to be used, and is not theirs to change — so the row
 * offers "edit" only where the permission is, and the server refuses the rest anyway.
 */
export const Route = createFileRoute('/_staff/staff/links/')({
  validateSearch: departmentListSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(linksListQuery(deps.department, deps)),
  component: LinksPage,
});

function LinksPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;
  const reachable = reachableDepartments(bootstrap);
  const writable = writableDepartments(bootstrap, LINKS_EDIT);

  // Where a new link goes: the department of the filter when it is one this person writes in, the
  // only one they write in, or the one they choose beside the button.
  const [chosen, setChosen] = useState<Department | undefined>(undefined);
  const fixed =
    search.department !== undefined && writable.includes(search.department) ? search.department : undefined;
  const target = fixed ?? (writable.length === 1 ? writable[0] : (chosen ?? writable[0]));

  const newLink =
    target === undefined ? null : (
      <Button asChild>
        <Link to="/staff/links/$id" params={{ id: 'new' }} search={{ department: target }}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('links.create')}
        </Link>
      </Button>
    );

  return (
    <PageShell
      title={t('links.title')}
      description={t('links.description')}
      breadcrumb={[{ label: t('backOffice.content') }, { label: t('links.title') }]}
      actions={
        target === undefined ? undefined : (
          <div className="flex flex-wrap items-end gap-2">
            {fixed === undefined && writable.length > 1 ? (
              <div className="flex flex-col gap-1">
                <Label htmlFor="createIn">{t('backOffice.createIn')}</Label>
                <Select
                  id="createIn"
                  value={target}
                  onValueChange={(value) => setChosen(value as Department)}
                  items={writable.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
                />
              </div>
            ) : null}
            {newLink}
          </div>
        )
      }
    >
      <DataList
        columns={
          reachable.length > 1 && search.department === undefined
            ? [
                ...linkColumns.slice(0, 1),
                col.department<LinkListDto>('ownerDepartment'),
                ...linkColumns.slice(1),
              ]
            : linkColumns
        }
        query={linksListQuery(search.department, search)}
        labels="links"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        toolbar={
          reachable.length > 1 ? (
            <ListFilter
              id="linksDepartment"
              label={t('backOffice.filters.department')}
              none={t('backOffice.filters.allDepartments')}
              value={search.department}
              onChange={(value) =>
                void navigate({
                  search: (previous) => ({
                    ...previous,
                    department: value as Department | undefined,
                    page: 1,
                  }),
                })
              }
              items={reachable.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
            />
          ) : undefined
        }
        actions={(row) =>
          holdsPermission(bootstrap, LINKS_EDIT, row.ownerDepartment) ? (
            <Button asChild variant="ghost" size="sm">
              <Link to="/staff/links/$id" params={{ id: String(row.id) }}>
                {t('common.edit')}
              </Link>
            </Button>
          ) : null
        }
        {...(newLink === null ? {} : { emptyAction: newLink })}
      />
    </PageShell>
  );
}
