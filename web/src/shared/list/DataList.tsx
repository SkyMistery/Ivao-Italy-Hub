import {
  DataTable,
  DataTableColumnHeader,
  Input,
  Pagination,
  Subtle,
  type DataTableProps,
} from '@ivao/atmosphere-react';
import { useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { Search } from 'lucide-react';
import { useEffect, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { resolveLocalized } from '../i18n/localized';
import { DepartmentBadge, EmptyState, StatusBadge } from '../ui';
import type { ColumnSpec } from './columns';
import type { ListSearch } from './search';

/**
 * The list engine. Paging, sorting and searching are the search parameters of the route and
 * nothing else, so a reload, a back button and a shared link all land on the same page (design M0
 * §7.3 and §7.5). The table itself is Atmosphere's `DataTable` in server side mode: nothing is
 * filtered or sorted in the browser, because the browser only ever holds one page.
 *
 * A screen hands it a list of column descriptions and the query for a page. It writes no cell.
 *
 * The pagination is drawn here rather than by `DataTable` for one reason: Atmosphere's own
 * pagination writes "Rows per page" and "Page 1 of 3" in English, and no screen of this hub carries
 * an untranslated sentence. Same for the column visibility menu, which is why it is off.
 */

/** A page of rows, in the shape every list of the hub answers with (`PagedResult<T>`). */
export interface Page<TRow> {
  items: TRow[];
  page: number;
  pageSize: number;
  total: number;
}

/** The column type, reached through Atmosphere's own props rather than a second dependency. */
type Column<TRow> = DataTableProps<TRow>['columns'][number];

export function DataList<TRow, TKey extends readonly unknown[]>({
  columns,
  query,
  search,
  onSearchChange,
  labels,
  locale,
  defaultLocale,
  timezone,
  actions,
  toolbar,
  emptyAction,
}: {
  columns: readonly ColumnSpec<TRow>[];
  /** The page, as query options: the route loader has already put it in the cache. */
  query: UseQueryOptions<Page<TRow>, Error, Page<TRow>, TKey>;
  /**
   * The search parameters of the route, and the way to change them. They are passed in rather than
   * read off a route object so that the typing recipe 2 exists for survives the trip: a generic
   * component that reached into the router would have to widen them to `unknown`
   * (`web/src/routes/README.md`).
   */
  search: ListSearch;
  onSearchChange: (patch: Partial<ListSearch>) => void;
  /** i18n prefix; a column header is `<labels>.fields.<field>`. */
  labels: string;
  locale: string;
  defaultLocale: string;
  timezone: string;
  /** Drawn at the end of every row. */
  actions?: (row: TRow) => ReactNode;
  /** Drawn next to the search box. */
  toolbar?: ReactNode;
  /** Offered when the list is empty and nothing is being searched for. */
  emptyAction?: ReactNode;
}) {
  const { t } = useTranslation();
  const { data, isPending } = useQuery(query);

  const total = data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / search.pageSize));

  const table: Column<TRow>[] = columns.map((column) => ({
    id: column.field,
    accessorFn: (row: TRow) => (row as Record<string, unknown>)[column.field],
    enableSorting: column.sortable,
    enableHiding: false,
    header: ({ column: instance }) =>
      column.sortable ? (
        <DataTableColumnHeader column={instance} title={t(`${labels}.fields.${column.field}`)} />
      ) : (
        <span>{t(`${labels}.fields.${column.field}`)}</span>
      ),
    cell: ({ row }) => (
      <Cell
        column={column}
        row={row.original}
        locale={locale}
        defaultLocale={defaultLocale}
        timezone={timezone}
      />
    ),
  }));

  if (actions !== undefined) {
    table.push({
      id: 'actions',
      enableSorting: false,
      enableHiding: false,
      header: () => <span className="sr-only">{t('list.actions')}</span>,
      cell: ({ row }) => <div className="flex justify-end gap-1">{actions(row.original)}</div>,
    });
  }

  const sorting = search.sort === undefined ? [] : [{ id: search.sort, desc: search.dir === 'desc' }];

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <SearchBox
          // Keyed on the parameter so that a back button, which changes the route and not this
          // component, restarts it on the term the URL now carries.
          key={search.q ?? ''}
          initial={search.q ?? ''}
          placeholder={t('list.search')}
          onCommit={(q) => onSearchChange({ q: q === '' ? undefined : q, page: 1 })}
        />
        {toolbar}
      </div>

      {!isPending && total === 0 ? (
        <EmptyState
          title={search.q === undefined ? t('list.empty.title') : t('list.empty.noMatch')}
          {...(search.q === undefined
            ? { description: t('list.empty.description'), action: emptyAction }
            : {})}
        />
      ) : (
        <DataTable
          data={data?.items ?? []}
          columns={table}
          isLoading={isPending}
          displayPagination={false}
          displayViewOptions={false}
          manualPagination
          manualSorting
          manualFiltering
          rowCount={total}
          noResultsMessage={t('list.empty.noMatch')}
          state={{ sorting, pagination: { pageIndex: search.page - 1, pageSize: search.pageSize } }}
          onSortingChange={(updater) => {
            const next = typeof updater === 'function' ? updater(sorting) : updater;
            const first = next[0];
            onSearchChange(
              first === undefined
                ? { sort: undefined, dir: 'asc', page: 1 }
                : { sort: first.id, dir: first.desc ? 'desc' : 'asc', page: 1 },
            );
          }}
        />
      )}

      {total === 0 ? null : (
        <div className="flex flex-wrap items-center justify-between gap-3">
          <Subtle>{t('list.total', { count: total })}</Subtle>
          <Pagination
            totalPages={pageCount}
            activePageIdx={search.page - 1}
            onPageChange={(index) => onSearchChange({ page: index + 1 })}
          />
        </div>
      )}
    </div>
  );
}

/**
 * The search box waits for the typing to stop. Every keystroke is a round trip to the database
 * otherwise, and a coordinator typing a title would make a dozen of them.
 */
function SearchBox({
  initial,
  placeholder,
  onCommit,
}: {
  initial: string;
  placeholder: string;
  onCommit: (value: string) => void;
}) {
  const [draft, setDraft] = useState(initial);

  useEffect(() => {
    if (draft === initial) {
      return undefined;
    }

    const timer = setTimeout(() => onCommit(draft), 300);
    return () => clearTimeout(timer);
  }, [draft, initial, onCommit]);

  return (
    <div className="relative max-w-xs flex-1">
      <Search aria-hidden className="text-muted-foreground absolute top-2.5 left-2 size-4" />
      <Input
        type="search"
        className="pl-8"
        aria-label={placeholder}
        placeholder={placeholder}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
      />
    </div>
  );
}

function Cell<TRow>({
  column,
  row,
  locale,
  defaultLocale,
  timezone,
}: {
  column: ColumnSpec<TRow>;
  row: TRow;
  locale: string;
  defaultLocale: string;
  timezone: string;
}) {
  const { t } = useTranslation();
  const value = (row as Record<string, unknown>)[column.field];

  switch (column.kind) {
    case 'localized':
      return <>{resolveLocalized(value as Record<string, string> | null, locale, defaultLocale)}</>;

    case 'boolean':
      return <StatusBadge active={value === true} />;

    case 'department':
      return typeof value === 'string' ? <DepartmentBadge department={value} /> : null;

    case 'badge':
      return typeof value === 'string' ? (
        <span className="text-sm">{t(`${column.labels}.options.${column.field}.${value}`)}</span>
      ) : null;

    case 'date':
      return typeof value === 'string' ? (
        <DateCell value={value} locale={locale} timezone={timezone} />
      ) : null;

    case 'number':
      return <span className="tabular-nums">{typeof value === 'number' ? value : ''}</span>;

    case 'text':
      return <>{typeof value === 'string' ? value : ''}</>;
  }
}

/**
 * UTC and the time zone of the division, both, because a hub is read by people flying in one and
 * organising in the other (docs/UI-GUIDELINES.md).
 */
function DateCell({ value, locale, timezone }: { value: string; locale: string; timezone: string }) {
  const instant = new Date(value);
  const format = (zone: string) =>
    new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'short', timeZone: zone }).format(
      instant,
    );

  return (
    <span className="flex flex-col leading-tight">
      <span className="tabular-nums">{format('UTC')} UTC</span>
      <Subtle className="tabular-nums">{format(timezone)}</Subtle>
    </span>
  );
}
