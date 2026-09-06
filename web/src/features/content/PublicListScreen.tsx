import { H1, Label, Lead, Select } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { BlockView, blockDataQuery, newId, type ContentListData } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../shared/api/department';
import { NO_CHOICE } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';

/**
 * A public list of one kind of content: `/news` and `/documents`.
 *
 * ⚠️ There is no fetch here and no markup for a row, because the list a visitor reads **is** the
 * data block a page would embed (design M1 §1.2). `newsList` and `documentList` already know how to
 * ask, what a department may show and how a card is drawn; a second reader for the same rows would
 * be a second place for the two to disagree — and it would have needed an endpoint of its own,
 * which M1 has a budget of exactly one for, and spent it on the upload (design M1 §12).
 *
 * The filters are the search parameters of the route, handed to the block as its properties. The
 * shelves they offer come from the same answer the block is drawing, so choosing one costs no
 * round trip of its own.
 *
 * ⚠️ Both lists filter the same way, in the search parameters, and neither has a second grammar for
 * it. `/documents/{dept}` as a path was tried and taken out: it reserved nine slugs, it shadowed any
 * document called `ed`, and it gave one of the two screens a way of saying something the other one
 * says differently (design changelog 1.6).
 */

/** As many as a data block will ever answer with (`DataBlockScope.MaxItems`). */
const PAGE_SIZE = 50;

export interface PublicListFilters {
  category?: string | undefined;
  department?: Department | undefined;
}

export function PublicListScreen({
  type,
  titles,
  filters,
  onFilter,
  layout,
}: {
  /** Which data block draws this list: `newsList` or `documentList`. */
  type: string;
  /** Where the screen's own words live: `news` or `documents`. */
  titles: string;
  filters: PublicListFilters;
  onFilter: (patch: PublicListFilters) => void;
  /** What the block is told to look like. News read as cards, documents as shelves. */
  layout: Record<string, unknown>;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  // Built once and handed to both the block and the query below, so the two share a cache key and
  // the screen costs exactly one request (`encodeProps` hashes the JSON, so the order matters).
  const props: Record<string, unknown> = {
    ...(filters.department === undefined ? {} : { department: filters.department }),
    ...(filters.category === undefined ? {} : { category: filters.category }),
    limit: PAGE_SIZE,
    ...layout,
  };

  const answer = useQuery(blockDataQuery(type, props));
  const shelves = (answer.data as ContentListData | undefined)?.categories ?? [];

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-1">
        <H1>{t(`${titles}.public.title`)}</H1>
        <Lead>{t(`${titles}.public.description`)}</Lead>
      </header>

      <div className="flex flex-wrap items-end gap-4">
        <Filter
          id="category"
          label={t(`${titles}.public.filters.category`)}
          none={t(`${titles}.public.filters.allCategories`)}
          value={filters.category}
          onChange={(chosen) => onFilter({ ...filters, category: chosen })}
          items={shelves.map((shelf) => ({
            value: shelf.key ?? '',
            label: read(shelf.label) || (shelf.key ?? ''),
          }))}
        />

        <Filter
          id="department"
          label={t(`${titles}.public.filters.department`)}
          none={t(`${titles}.public.filters.allDepartments`)}
          value={filters.department}
          onChange={(chosen) => onFilter({ ...filters, department: chosen as Department | undefined })}
          items={DEPARTMENTS.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
        />
      </div>

      <BlockView
        block={{ id: LIST_BLOCK_ID, type, version: 1, props, renderMode: 'live', frozen: null, column: null }}
        staff={false}
      />
    </div>
  );
}

/**
 * A stable identifier for the one block on the page. It has to be stable across renders — React
 * keys a block by it — and it does not have to be unique across pages, because nothing here is ever
 * saved: this block belongs to a screen, not to a body somebody wrote.
 */
const LIST_BLOCK_ID = newId('b');

/**
 * One filter. A select with a way back to "everything", the same gesture an optional field of the
 * form generator has, because a filter you cannot clear is a filter that traps a visitor.
 */
function Filter({
  id,
  label,
  none,
  value,
  onChange,
  items,
}: {
  id: string;
  label: string;
  none: string;
  value: string | undefined;
  onChange: (value: string | undefined) => void;
  items: readonly { value: string; label: string }[];
}) {
  return (
    <div className="flex min-w-48 flex-col gap-1">
      <Label htmlFor={id}>{label}</Label>
      <Select
        // Measured, not assumed: Atmosphere's `Select` forwards `id` to the trigger, which is what
        // makes the label above actually name it. Without it the label points at nothing and the
        // control is a button a screen reader reads as "Every category" and nothing else — the
        // fifth contract of that library worth checking in a browser rather than reading.
        id={id}
        {...(value === undefined ? {} : { value })}
        onValueChange={(chosen) => onChange(chosen === NO_CHOICE ? undefined : chosen)}
        placeholder={none}
        items={[{ value: NO_CHOICE, label: none }, ...items]}
      />
    </div>
  );
}
