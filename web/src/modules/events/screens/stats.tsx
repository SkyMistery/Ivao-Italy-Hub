import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { holdsPermission } from '../../../shared/api/bootstrap';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { Notice } from '../../../shared/ui';
import {
  checkedBookingsQuery,
  eventStatsQuery,
  shiftOutcomesQuery,
  useRecalculateStats,
  type CheckedBookingRow,
  type EventAtcShiftRowDto,
  type EventDetailDto,
} from '../api';
import { EVENT_ATC_VIEW, EVENT_BOOKINGS_VIEW } from '../permissions';
import { statsSchema, statsToFormValues } from '../schemas';

/**
 * The tab «Statistics» of an event over (design M4 §1.10, §5.1, §7.2, E13a; note 2026-10-16-le-verifiche-dopo-l-evento, readings 8–10): the
 * numbers in a generated form that saves nothing — the words of each are the page's —, when they were worked out and whether the checks after
 * the event are done, «Recalculate» for whoever writes the event, and two generated lists with what the checks found: the bookings — flown,
 * not flown, still to check — to whoever reads the bookings, and the shifts — how each went, and who covered the position next to a no-show
 * proposed — to whoever reads the ATC. Who flew without booking is a number only: nobody of them is listed.
 */
export function StatsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const stats = useQuery(eventStatsQuery(event.id));
  const recalculate = useRecalculateStats(event.id);
  const refusal = describeProblem(recalculate.error, t, i18n.language);

  if (stats.isPending) {
    return null;
  }

  const row = stats.data ?? null;
  const slots = !event.wholeDivision && (event.publicSlots || event.privateSlots);

  return (
    <div className="flex flex-col gap-6">
      {row === null ? (
        <Notice tone="info" title={t('events:stats.none')} />
      ) : (
        <Notice
          tone="info"
          title={t('events:stats.computedAt', { date: moment(row.computedAt) })}
          description={
            event.wholeDivision
              ? t('events:stats.wholeDivision')
              : row.checksDoneAt === null
                ? t('events:stats.checksPending')
                : t('events:stats.checksDone', { date: moment(row.checksDoneAt) })
          }
        />
      )}
      {editable ? (
        <div className="flex flex-col items-start gap-2">
          <Button variant="secondary" disabled={recalculate.isPending} onClick={() => recalculate.mutate()}>
            {t('events:stats.recalculate')}
          </Button>
          {refusal === null ? null : <Notice tone="error" title={refusal} />}
        </div>
      ) : null}
      {row === null ? null : (
        <SchemaForm
          // Drawn again when the numbers change, so a recalculation shows at once.
          key={row.computedAt}
          schema={statsSchema}
          defaults={statsToFormValues(row)}
          locales={[]}
          labels="events:stats"
        />
      )}
      {slots && holdsPermission(bootstrap, EVENT_BOOKINGS_VIEW, event.ownerDepartment) ? (
        <CheckedBookings event={event} />
      ) : null}
      {event.hasRoster && holdsPermission(bootstrap, EVENT_ATC_VIEW, event.ownerDepartment) ? (
        <ShiftOutcomes event={event} />
      ) : null}
    </div>
  );
}

const bookingColumns: readonly ColumnSpec<CheckedBookingRow>[] = [
  col.person('pilot'),
  col.text('callsign'),
  col.text('departureIcao'),
  col.date('offBlockUtc'),
  col.text('arrivalIcao'),
  col.badge('outcome', 'events:stats.bookings'),
  col.date('flownAt'),
];

/** The bookings of the event with what the check found: the outcome is a word of the page, the times the core's. */
function CheckedBookings({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <section className="flex flex-col gap-2">
      <h3 className="text-lg font-semibold">{t('events:stats.bookings.title')}</h3>
      <DataList
        columns={bookingColumns}
        query={checkedBookingsQuery(event.id, search)}
        labels="events:stats.bookings"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:stats.bookings.empty')}
      />
    </section>
  );
}

const shiftColumns: readonly ColumnSpec<EventAtcShiftRowDto>[] = [
  col.text('callsign'),
  col.person('controller'),
  col.date('fromUtc', { sortable: true }),
  col.date('toUtc'),
  col.badge('attendance', 'events:stats.shifts'),
  col.person('coveredBy'),
];

/** The shifts of the event with how each went, and who covered the position next to a no-show the check proposed. */
function ShiftOutcomes({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <section className="flex flex-col gap-2">
      <h3 className="text-lg font-semibold">{t('events:stats.shifts.title')}</h3>
      <DataList
        columns={shiftColumns}
        query={shiftOutcomesQuery(event.id, search)}
        labels="events:stats.shifts"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:stats.shifts.empty')}
      />
    </section>
  );
}
