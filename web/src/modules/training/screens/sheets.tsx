import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  lastSheetItemQuery,
  ratingsQuery,
  sheetItemQuery,
  sheetItemsListQuery,
  useDeleteSheetItem,
  useSaveSheetItem,
  type SheetItemListDto,
} from '../api';
import {
  RATING_KINDS,
  emptySheetItem,
  fromRatingChoice,
  ratingChoice,
  sheetItemFormSearchSchema,
  sheetItemSchema,
  sheetItemToFormValues,
  sheetItemsSearchSchema,
  type RatingKind,
  type SheetItemsSearch,
} from '../schemas';

import { ratingOptions } from './ratings';

/**
 * The evaluation sheet (design M3 §1.4, §4.2): the items a trainer marks after a session, per ladder and rating — practice,
 * graded from one to five, and theory, done, not done or to improve —, each with its title in every language of the
 * division. List and form generated; `Training.ManageSheets` reads and writes. The list is narrowed to the sheet of one
 * rating by its two filters, and a new item starts on that sheet, after its last item.
 */

export const SHEETS = '/staff/training/sheets';

const columns: readonly ColumnSpec<SheetItemListDto>[] = [
  col.number('sort', { sortable: true }),
  col.localized('title'),
  col.text('ratingShortName'),
  col.badge('section', 'training:sheets'),
  col.boolean('isActive'),
  col.date('updatedAt', { sortable: true }),
];

/** The address of the sheet of this ladder and rating, as far as they are known: what the list is narrowed to. */
function sheetQuery(sheet: {
  readonly kind?: RatingKind | undefined;
  readonly rating?: number | undefined;
}): string {
  if (sheet.kind === undefined) {
    return '';
  }

  return sheet.rating === undefined
    ? `?kind=${sheet.kind}`
    : `?kind=${sheet.kind}&rating=${String(sheet.rating)}`;
}

function sheetHref(sheet: Parameters<typeof sheetQuery>[0]): string {
  return `${SHEETS}${sheetQuery(sheet)}`;
}

/** A new item, on the sheet the list is narrowed to. */
function NewItemButton({ search }: { search: SheetItemsSearch }) {
  const { t } = useTranslation();

  return (
    <Button asChild>
      <RouterAnchor href={`${SHEETS}/new${sheetQuery(search)}`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('training:sheets.create')}
      </RouterAnchor>
    </Button>
  );
}

export function SheetItemsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const search = sheetItemsSearchSchema.parse(useSearch({ strict: false }));
  const ratings = useQuery(ratingsQuery()).data ?? [];

  const onSearchChange = (patch: Partial<SheetItemsSearch>) =>
    void navigate({
      search: ((previous: SheetItemsSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  // A rating is on one ladder: the ladder chosen narrows the ratings offered, and a rating chosen chooses its ladder.
  const offered = ratings.filter((rating) => search.kind === undefined || rating.kind === search.kind);
  const chosen =
    search.kind === undefined || search.rating === undefined
      ? undefined
      : ratingChoice(search.kind, search.rating);

  return (
    <PageShell
      title={t('training:sheets.title')}
      description={t('training:sheets.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:sheets.title') }]}
      actions={<NewItemButton search={search} />}
    >
      <DataList
        columns={columns}
        query={sheetItemsListQuery(search)}
        labels="training:sheets"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        toolbar={
          <>
            <ListFilter
              id="sheet-items-kind"
              label={t('training:sheets.filters.kind')}
              none={t('training:sheets.filters.anyKind')}
              value={search.kind}
              onChange={(kind) =>
                onSearchChange({ kind: kind as RatingKind | undefined, rating: undefined, page: 1 })
              }
              items={RATING_KINDS.map((kind) => ({ value: kind, label: t(`training:kinds.${kind}`) }))}
            />
            <ListFilter
              id="sheet-items-rating"
              label={t('training:sheets.filters.rating')}
              none={t('training:sheets.filters.anyRating')}
              value={chosen}
              onChange={(value) =>
                onSearchChange(
                  value === undefined
                    ? { rating: undefined, page: 1 }
                    : { ...fromRatingChoice(value), page: 1 },
                )
              }
              items={ratingOptions(offered, t)}
              className="min-w-72"
            />
          </>
        }
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${SHEETS}/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        )}
        emptyAction={<NewItemButton search={search} />}
      />
    </PageShell>
  );
}

export function SheetItemForm() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const isNew = id === 'new';
  const from = sheetItemFormSearchSchema.parse(useSearch({ strict: false }));
  const locales = bootstrap.division.locales;

  // A new item asked from the sheet of a rating starts on it, after its last item.
  const sheet =
    isNew && from.kind !== undefined && from.rating !== undefined
      ? { kind: from.kind, rating: from.rating }
      : null;

  const item = useQuery({ ...sheetItemQuery(Number(id)), enabled: !isNew }).data ?? null;
  const ratings = useQuery(ratingsQuery()).data;
  const last = useQuery(lastSheetItemQuery(sheet));
  const save = useSaveSheetItem(isNew ? null : Number(id));
  const remove = useDeleteSheetItem();

  // The last item as the sheet holds it now, not as the cache kept it: the form reads its values once, and somebody else
  // may have added an item since the cache was filled.
  if ((!isNew && item === null) || ratings === undefined || (sheet !== null && !last.isFetchedAfterMount)) {
    return null;
  }

  const title = isNew ? t('training:sheets.create') : t('training:sheets.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);
  const back = item === null ? sheetHref(sheet ?? {}) : sheetHref(item);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('training:nav.section') },
        { label: t('training:sheets.title'), to: back },
        { label: title },
      ]}
      actions={
        item === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('training:sheets.delete.title')}
            description={t('training:sheets.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(item.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          // Keyed on the version, as the issues of the tours are: an item somebody else saved since the cache was filled is
          // drawn again when the row arrives, rather than sent back stale and answered 409.
          key={item?.rowVersion ?? 'new'}
          schema={sheetItemSchema(ratingOptions(ratings, t))}
          defaults={
            item === null
              ? emptySheetItem(
                  sheet === null ? '' : ratingChoice(sheet.kind, sheet.rating),
                  (last.data?.items[0]?.sort ?? 0) + 1,
                  locales,
                )
              : sheetItemToFormValues(item, locales)
          }
          locales={locales}
          labels="training:sheets"
          onSubmit={async (values) => {
            const saved = await save.mutateAsync(values);
            void navigate({ href: sheetHref(saved) });
          }}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
