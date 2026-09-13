import { Label, Select } from '@ivao/atmosphere-react';
import { Plus } from 'lucide-react';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import {
  holdsPermission,
  reachableDepartments,
  writableDepartments,
  type Bootstrap,
  type Department,
} from '../../shared/api/bootstrap';
import { DataList, ListFilter, col, type ColumnSpec } from '../../shared/list';
import { PageShell } from '../../shared/ui';

import { CONTENT_SCREEN_KINDS, ROW_KINDS, type ContentSearch, type RowKind } from './contentSearch';
import { CONTENT_KINDS, TEMPLATE_COLUMNS } from './kinds';
import { contentListQuery, templateListQuery, type ContentListDto } from './queries';
import { MANAGE_TEMPLATES } from './templateRules';
import { TemplatePicker } from './TemplatePicker';

/** Written by a coordinator of the department; read by the permission the server asks for a write. */
const CONTENT_EDIT = 'Content.Edit';

/** Who may leave a page at the top of the site (note 2026-09-13-contenuti-centralizzati, 3.7). */
const CONTENT_APPROVE = 'Content.Approve';

/**
 * `/staff/content`: the pages, the news, the documents and the templates of every department a
 * member reaches, on one screen (note 2026-09-13-contenuti-centralizzati, 3.1). The kind and the
 * department are filters in the address; the entries of a department in the sidebar open this same
 * screen with its department already chosen.
 *
 * There is no table here and there is no fetch: the columns are declared in `kinds.ts` and drawn by
 * `DataList`, and paging, sorting and searching are the typed search parameters of the route
 * (design M0 §7.3, recipe 2). Which rows the list holds is the server's to say — the departments of
 * the reader — so a department left out of the address is the whole back office, not a leak.
 *
 * The links are handed in rather than built here. A route path is a literal the router checks at
 * build time, so it belongs in the file that owns the route.
 */
export function ContentListScreen({
  bootstrap,
  search,
  onSearchChange,
  onCreatedFromTemplate,
  createLink,
  rowAction,
}: {
  bootstrap: Bootstrap;
  search: ContentSearch;
  onSearchChange: (patch: Partial<ContentSearch>) => void;
  onCreatedFromTemplate: (id: number) => void;
  /** The "new" button, as a link to the editor of a row of this kind in this department. */
  createLink: (target: { department: Department; kind: RowKind; template: boolean }) => ReactNode;
  rowAction: (row: ContentListDto) => ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const division = bootstrap.division;

  const rowKind: RowKind | null = search.kind === 'Template' ? null : search.kind;
  const isTemplates = rowKind === null;
  const titles = rowKind === null ? 'templates' : CONTENT_KINDS[rowKind].titles;

  const reachable = reachableDepartments(bootstrap);
  const writable = writableDepartments(bootstrap, isTemplates ? MANAGE_TEMPLATES : CONTENT_EDIT);

  // Where a new row goes. The department chosen in the address when there is one this person may
  // write in; otherwise the only one they may write in; otherwise they say, beside the button.
  const [chosen, setChosen] = useState<Department | undefined>(undefined);
  const fixed =
    search.department !== undefined && writable.includes(search.department) ? search.department : undefined;
  const target = fixed ?? (writable.length === 1 ? writable[0] : (chosen ?? writable[0]));
  const asksWhere = fixed === undefined && writable.length > 1;

  // Which kind a new template is for: it decides which fields the form draws, so it is chosen first.
  const [templateKind, setTemplateKind] = useState<RowKind>('Page');

  // The department is a column only when there is more than one to tell apart.
  const departmentColumn: readonly ColumnSpec<ContentListDto>[] =
    reachable.length > 1 && search.department === undefined
      ? [col.department<ContentListDto>('ownerDepartment')]
      : [];
  const baseColumns = rowKind === null ? TEMPLATE_COLUMNS : CONTENT_KINDS[rowKind].columns;
  const columns = [...baseColumns.slice(0, 1), ...departmentColumn, ...baseColumns.slice(1)];

  const newRow =
    target === undefined
      ? null
      : createLink({
          department: target,
          kind: rowKind ?? templateKind,
          template: isTemplates,
        });

  const create =
    target === undefined ? null : (
      <div className="flex flex-wrap items-end gap-2">
        {asksWhere ? (
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
        {isTemplates ? (
          <div className="flex flex-col gap-1">
            {/* ⚠️ A real label and not an `aria-label`: Atmosphere's `Select` drops the attribute. */}
            <Label htmlFor="templateKind">{t('content.fields.kind')}</Label>
            <Select
              id="templateKind"
              value={templateKind}
              onValueChange={(value) => setTemplateKind(value as RowKind)}
              items={ROW_KINDS.map((value) => ({ value, label: t(`content.options.kind.${value}`) }))}
            />
          </div>
        ) : null}
        {newRow}
      </div>
    );

  // What the two lists below have in common: everything but the question they ask.
  const list = {
    columns,
    labels: 'content',
    locale: i18n.language,
    defaultLocale: division.defaultLocale,
    timezone: division.timezone,
    search,
    onSearchChange,
    actions: rowAction,
    toolbar: (
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex min-w-48 flex-col gap-1">
          <Label htmlFor="contentKind">{t('backOffice.filters.kind')}</Label>
          <Select
            id="contentKind"
            value={search.kind}
            onValueChange={(value) => onSearchChange({ kind: value as ContentSearch['kind'], page: 1 })}
            items={CONTENT_SCREEN_KINDS.map((value) => ({ value, label: t(`backOffice.kinds.${value}`) }))}
          />
        </div>
        {reachable.length > 1 ? (
          <ListFilter
            id="contentDepartment"
            label={t('backOffice.filters.department')}
            none={t('backOffice.filters.allDepartments')}
            value={search.department}
            onChange={(value) => onSearchChange({ department: value as Department | undefined, page: 1 })}
            items={reachable.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
          />
        ) : null}
      </div>
    ),
    // The button alone: the choices beside it are already at the top, and two controls with one id
    // would be two controls one label cannot name.
    ...(newRow === null ? {} : { emptyAction: newRow }),
  };

  return (
    <PageShell
      title={t(`${titles}.title`)}
      description={t(`${titles}.description`)}
      breadcrumb={[
        { label: t('backOffice.content') },
        ...(search.department === undefined ? [] : [{ label: t(`departments.${search.department}`) }]),
        { label: t(`${titles}.title`) },
      ]}
      actions={create}
    >
      <div className="flex flex-col gap-6">
        {/* Every staff member may read every template, whoever owns it, so this offers something
            outside the web team as well (design M1 §9.4). The page it makes is of the department a
            new row would go to. */}
        {rowKind === null || target === undefined ? null : (
          <TemplatePicker
            department={target}
            kind={rowKind}
            mayBeAtTheTop={holdsPermission(bootstrap, CONTENT_APPROVE, target)}
            onCreated={onCreatedFromTemplate}
          />
        )}

        {/* Two lists and not one with a branch inside the query, because a template list and a list
            of one kind are different questions to the cache and different shapes to the compiler;
            the key makes them two lists to React as well. */}
        {rowKind === null ? (
          <DataList key="templates" query={templateListQuery(search.department, search)} {...list} />
        ) : (
          <DataList key="rows" query={contentListQuery(search.department, search, rowKind)} {...list} />
        )}
      </div>
    </PageShell>
  );
}

/** The icon and wording of the "new" button, so the route files do not each spell it out. */
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
