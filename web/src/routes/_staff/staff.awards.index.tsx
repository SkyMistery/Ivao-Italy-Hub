import { Button, Label, Select } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { awardColumns } from '../../features/awards/list';
import { awardsListQuery, type AwardListDto } from '../../features/awards/queries';
import {
  holdsPermission,
  holdsPermissionAnywhere,
  reachableDepartments,
  writableDepartments,
  type Department,
} from '../../shared/api/bootstrap';
import { DataList, ListFilter, col, departmentListSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/** The catalogue is read with this, on any department; writing asks for the other, on the department of the row. */
const AWARDS_VIEW = 'Awards.View';
const AWARDS_EDIT = 'Awards.Edit';

/**
 * The catalogue of awards (M2, T4b): recipe 2 of design M0 §7.3, the shape of the links. Every award of
 * every department is here, because whoever assigns has to see them all; "edit" appears only where
 * the department is one this person writes in, and the server refuses the rest anyway.
 */
export const Route = createFileRoute('/_staff/staff/awards/')({
  validateSearch: departmentListSearchSchema,
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, AWARDS_VIEW)) {
      throw redirect({ to: '/staff/awards/queue' });
    }
  },
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(awardsListQuery(deps.department, deps)),
  component: AwardsPage,
});

function AwardsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;
  const reachable = reachableDepartments(bootstrap);
  const writable = writableDepartments(bootstrap, AWARDS_EDIT);

  const [chosen, setChosen] = useState<Department | undefined>(undefined);
  const fixed =
    search.department !== undefined && writable.includes(search.department) ? search.department : undefined;
  const target = fixed ?? (writable.length === 1 ? writable[0] : (chosen ?? writable[0]));

  const newAward =
    target === undefined ? null : (
      <Button asChild>
        <Link to="/staff/awards/$id" params={{ id: 'new' }} search={{ department: target }}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('awards.create')}
        </Link>
      </Button>
    );

  return (
    <PageShell
      title={t('awards.title')}
      description={t('awards.description')}
      breadcrumb={[{ label: t('awards.section') }, { label: t('awards.title') }]}
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
            {newAward}
          </div>
        )
      }
    >
      <DataList
        columns={
          search.department === undefined
            ? [
                ...awardColumns.slice(0, 2),
                col.department<AwardListDto>('ownerDepartment'),
                ...awardColumns.slice(2),
              ]
            : awardColumns
        }
        query={awardsListQuery(search.department, search)}
        labels="awards"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        toolbar={
          reachable.length > 1 ? (
            <ListFilter
              id="awardsDepartment"
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
          holdsPermission(bootstrap, AWARDS_EDIT, row.ownerDepartment) ? (
            <Button asChild variant="ghost" size="sm">
              <Link to="/staff/awards/$id" params={{ id: String(row.id) }}>
                {t('common.edit')}
              </Link>
            </Button>
          ) : null
        }
        {...(newAward === null ? {} : { emptyAction: newAward })}
      />
    </PageShell>
  );
}
