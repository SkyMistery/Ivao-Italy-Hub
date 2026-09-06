import { Plus } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import type { Bootstrap, Department } from '../../shared/api/bootstrap';
import { DataList, type ListSearch } from '../../shared/list';
import { PageShell } from '../../shared/ui';

import type { ContentKindConfig } from './kinds';
import { contentListQuery, type ContentListDto } from './queries';
import { TemplatePicker } from './TemplatePicker';

/**
 * The list of one kind of content in one department. Pages, news and documents are this screen
 * three times over with a different `kind` and a different set of columns, which is what design
 * M1 §3.2 means by "two configurations of a list and not two screens".
 *
 * There is no table here and there is no fetch: the columns are declared in `kinds.ts` and drawn by
 * `DataList`, and paging, sorting and searching are the typed search parameters of the route above
 * (design M0 §7.3, recipe 2).
 *
 * The links are handed in rather than built here. A route path is a literal the router checks at
 * build time, so it belongs in the file that owns the route: a component taking `to` as a string
 * would be a component that turns a mistyped address into a runtime surprise.
 */
export function ContentListScreen({
  config,
  bootstrap,
  department,
  search,
  onSearchChange,
  onCreatedFromTemplate,
  createButton,
  rowAction,
}: {
  config: ContentKindConfig;
  bootstrap: Bootstrap;
  department: Department;
  search: ListSearch;
  onSearchChange: (patch: Partial<ListSearch>) => void;
  onCreatedFromTemplate: (id: number) => void;
  createButton: ReactNode;
  rowAction: (row: ContentListDto) => ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const division = bootstrap.division;

  return (
    <PageShell
      title={t(`${config.titles}.title`)}
      description={t(`${config.titles}.description`)}
      breadcrumb={[{ label: department }, { label: t(`${config.titles}.title`) }]}
      actions={createButton}
    >
      <div className="flex flex-col gap-6">
        {/* Every staff member may read every template, whoever owns it, so this offers something
            outside the web team as well (design M1 §9.4). */}
        <TemplatePicker department={department} kind={config.kind} onCreated={onCreatedFromTemplate} />

        <DataList
          columns={config.columns}
          query={contentListQuery(department, search, config.kind)}
          labels="content"
          locale={i18n.language}
          defaultLocale={division.defaultLocale}
          timezone={division.timezone}
          search={search}
          onSearchChange={onSearchChange}
          actions={rowAction}
          emptyAction={createButton}
        />
      </div>
    </PageShell>
  );
}

/** The icon and wording of the "new" button, so the three route files do not each spell it out. */
export function CreateLabel({ titles }: { titles: string }) {
  const { t } = useTranslation();

  return (
    <>
      <Plus aria-hidden className="mr-2 size-4" />
      {t(`${titles}.create`)}
    </>
  );
}

/** The wording of the row action, which is the same everywhere and reads better named once. */
export function EditLabel() {
  const { t } = useTranslation();

  return <>{t('common.edit')}</>;
}
