import { H1, Label, Lead, Select } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { blockDataQuery } from '../../blocks';
import type { CalendarKind, Department } from '../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../shared/api/department';
import { NO_CHOICE } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import {
  CALENDAR_SCREEN_VIEWS,
  CalendarView,
  calendarWindow,
  type CalendarItem,
  type CalendarViewMode,
} from '../../shared/ui';

/**
 * `/calendar`: the one calendar of the division, as a visitor reads it.
 *
 * ⚠️ Like the public news and documents, there is **no fetch of its own here**: what it reads is the
 * `calendar` data block, asked live through the endpoint any block uses. A second reader of
 * `cms_calendar_entries` would be a second place to decide what a department may show — and it would
 * have cost the endpoint M1 spent on the upload (design M1 §4 and §12).
 *
 * What a *screen* asks for that a *block* does not is a **window**: a grid showing September is
 * showing September, not "the next thirty-one days". The provider takes `from` and `to` for exactly
 * that, and they are deliberately absent from the block's own schema — a saved body that pinned a
 * page to one month would go stale the day after it was published.
 */
export interface PublicCalendarFilters {
  view?: CalendarViewMode | undefined;
  department?: Department | undefined;
  kind?: string | undefined;
  /** The month or week the grid is drawn around, as `YYYY-MM-DD`. Absent means today. */
  on?: string | undefined;
}

/** Never more rows than a data block will answer with (`DataBlockScope.MaxItems`). */
const PAGE_SIZE = 50;

export function PublicCalendarScreen({
  filters,
  onFilter,
  timezone,
  vocabulary,
}: {
  filters: PublicCalendarFilters;
  onFilter: (patch: PublicCalendarFilters) => void;
  timezone: string;
  /** The division's kinds, from `/api/me`: what to call one, and the colour of its chip. */
  vocabulary: readonly CalendarKind[];
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const view = filters.view ?? 'month';
  const anchor = readAnchor(filters.on);
  const { from, to } = calendarWindow(view, anchor);

  const props: Record<string, unknown> = {
    ...(filters.department === undefined ? {} : { department: filters.department }),
    ...(filters.kind === undefined ? {} : { kinds: [{ kind: filters.kind }] }),
    from,
    to,
    limit: PAGE_SIZE,
  };

  const answer = useQuery(blockDataQuery('calendar', props));
  const items = (answer.data as { items?: CalendarItem[] } | undefined)?.items ?? [];

  // The kinds a visitor may filter by are the ones the answer actually holds. They are free strings
  // that the staff and the modules write, so there is no vocabulary to read them from — and a list
  // of every kind that ever existed would offer choices that match nothing.
  const kinds = [...new Set(items.map((item) => item.kind).filter((kind) => kind !== undefined))];

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-1">
        <H1>{t('calendar.public.title')}</H1>
        <Lead>{t('calendar.public.description')}</Lead>
      </header>

      <div className="flex flex-wrap items-end gap-4">
        <div className="flex min-w-40 flex-col gap-1">
          <Label htmlFor="view">{t('calendar.public.filters.view')}</Label>
          <Select
            id="view"
            value={view}
            onValueChange={(chosen) => onFilter({ ...filters, view: chosen as CalendarViewMode })}
            items={CALENDAR_SCREEN_VIEWS.map((mode) => ({
              value: mode,
              label: t(`calendar.public.views.${mode}`),
            }))}
          />
        </div>

        <Filter
          id="department"
          label={t('calendar.public.filters.department')}
          none={t('calendar.public.filters.allDepartments')}
          value={filters.department}
          onChange={(chosen) => onFilter({ ...filters, department: chosen as Department | undefined })}
          items={DEPARTMENTS.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
        />

        <Filter
          id="kind"
          label={t('calendar.public.filters.kind')}
          none={t('calendar.public.filters.allKinds')}
          value={filters.kind}
          onChange={(chosen) => onFilter({ ...filters, kind: chosen })}
          // The words come from the vocabulary, so a visitor reads "Riunione" rather than
          // `meeting`; a kind that is not in it — one a module projected — keeps its key.
          items={kinds.map((kind) => ({
            value: kind,
            label: read(vocabulary.find((word) => word.key === kind)?.label) || kind,
          }))}
        />
      </div>

      <CalendarView
        items={items}
        view={view}
        anchor={anchor}
        // Where the four views are is in the address, so a visitor can send the view they are
        // looking at to somebody else. The agenda is not one of the four: it reads forwards from
        // now and has nothing to navigate, and it is what a block inside a page shows.
        onAnchorChange={
          view === 'agenda'
            ? undefined
            : (next) => onFilter({ ...filters, on: next.toISOString().slice(0, 10) })
        }
        timezone={timezone}
        kinds={vocabulary}
        empty={t('calendar.public.empty')}
      />
    </div>
  );
}

/** The day the grid is drawn around: what the address says, or today. */
function readAnchor(on: string | undefined): Date {
  if (on === undefined) {
    return new Date();
  }

  const parsed = new Date(`${on}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}

/** One filter, with a way back to "everything": a filter you cannot clear traps a visitor. */
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
    <div className="flex min-w-40 flex-col gap-1">
      <Label htmlFor={id}>{label}</Label>
      <Select
        id={id}
        {...(value === undefined ? {} : { value })}
        onValueChange={(chosen) => onChange(chosen === NO_CHOICE ? undefined : chosen)}
        placeholder={none}
        items={[{ value: NO_CHOICE, label: none }, ...items]}
      />
    </div>
  );
}
