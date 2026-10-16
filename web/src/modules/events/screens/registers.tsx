import { Button, H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useParams, useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { Dialog, PageShell, personName } from '../../../shared/ui';
import {
  controllersRegisterQuery,
  pilotBookingsQuery,
  pilotsRegisterQuery,
  shiftRowsQuery,
  useDecideAttendance,
  useExcuseUnflown,
  type CheckedBookingRow,
  type ControllerRegisterRowDto,
  type EventAtcShiftRowDto,
  type PilotRegisterRowDto,
} from '../api';
import {
  attendanceSchema,
  decidableAttendances,
  unflownExcuseSchema,
  type AttendanceFormValues,
  type UnflownExcuseFormValues,
} from '../schemas';

/**
 * The registers in the staff's back office (design M4 §3.7, §4.5, §7.2, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 1,
 * 4, 5, 6 and 10): the controllers' — `/staff/events/controllers`, one row per controller with their counts and their penalty, and a
 * controller's shifts of every event with «Decide» — and the pilots' — `/staff/events/pilots`, one row per pilot with the bookings not flown
 * that count and whether they have the limits, and a pilot's bookings with «Take away». The outcomes of the shifts of one event, with the same
 * «Decide», are the section «Outcomes» of its ATC (`ShiftOutcomesSection`). Whether the reader may decide a row is the server's to say
 * (`decidable`, `excusable`: the one handler on the row, never their own); the lists send data, and the page writes the words.
 */

const CONTROLLERS = '/staff/events/controllers';
const PILOTS = '/staff/events/pilots';

const controllerColumns: readonly ColumnSpec<ControllerRegisterRowDto>[] = [
  col.person('controller'),
  col.number('attended'),
  col.number('noShows'),
  col.number('excused'),
  col.number('toDecide'),
  col.number('penalty'),
];

/** The register of the controllers (§4.5): who has something in it, by penalty, each opened on their shifts. */
export function ControllersRegisterPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({}));

  return (
    <PageShell
      title={t('events:registers.controllers.title')}
      description={t('events:registers.controllers.description')}
      breadcrumb={[{ label: t('events:nav.section') }, { label: t('events:registers.controllers.title') }]}
    >
      <DataList
        columns={controllerColumns}
        query={controllersRegisterQuery(search)}
        labels="events:registers.controllers"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:registers.controllers.empty')}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${CONTROLLERS}/${row.controller.vid}`}>
              {t('events:registers.open')}
            </RouterAnchor>
          </Button>
        )}
      />
    </PageShell>
  );
}

/** One controller's register: their counts, and their shifts of every event with how each went and «Decide» where the reader may. */
export function ControllerRegisterPage() {
  const { t } = useTranslation();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const vid = Number(params.vid);
  const row = useQuery(
    controllersRegisterQuery(listSearchSchema.parse({ q: String(vid), pageSize: 100 })),
  ).data?.items.find((candidate) => candidate.controller.vid === vid);
  const name = row === undefined ? String(vid) : personName(row.controller, t);
  const [search, setSearch] = useState<ListSearch>(() =>
    listSearchSchema.parse({ sort: 'fromUtc', dir: 'desc' }),
  );

  // The counts in plain sight above the shifts: under a breadcrumb the page's description is only the heading's title.
  const counts =
    row === undefined
      ? t('events:registers.controllers.nothing')
      : t('events:registers.controllers.counts', {
          attended: row.attended,
          noShows: row.noShows,
          excused: row.excused,
          toDecide: row.toDecide,
          penalty: row.penalty,
        });

  return (
    <PageShell
      title={name}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:registers.controllers.title'), to: CONTROLLERS },
        { label: name },
      ]}
    >
      <div className="flex flex-col gap-4">
        <p className="text-sm">{counts}</p>
        <ShiftDecisions
          query={shiftRowsQuery({ controllerVid: vid }, search)}
          search={search}
          onSearchChange={setSearch}
          withEvent
        />
      </div>
    </PageShell>
  );
}

const pilotColumns: readonly ColumnSpec<PilotRegisterRowDto>[] = [
  col.person('pilot'),
  col.number('counted'),
  col.number('excused'),
  col.boolean('limited'),
  col.date('limitedUntil'),
];

/** The register of the pilots (§3.7): who has a booking not flown in it, the ones with the limits first, each opened on their bookings. */
export function PilotsRegisterPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({}));

  return (
    <PageShell
      title={t('events:registers.pilots.title')}
      description={t('events:registers.pilots.description')}
      breadcrumb={[{ label: t('events:nav.section') }, { label: t('events:registers.pilots.title') }]}
    >
      <DataList
        columns={pilotColumns}
        query={pilotsRegisterQuery(search)}
        labels="events:registers.pilots"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:registers.pilots.empty')}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${PILOTS}/${row.pilot.vid}`}>{t('events:registers.open')}</RouterAnchor>
          </Button>
        )}
      />
    </PageShell>
  );
}

const pilotBookingColumns: readonly ColumnSpec<CheckedBookingRow>[] = [
  col.localized('eventTitle'),
  col.text('callsign'),
  col.text('departureIcao'),
  col.date('offBlockUtc'),
  col.text('arrivalIcao'),
  col.badge('outcome', 'events:stats.bookings'),
  col.person('unflownExcusedBy'),
  col.text('unflownExcusedNote'),
];

/** One pilot's register: whether they have the limits and until when, and their bookings with what the check found and «Take away». */
export function PilotRegisterPage() {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const vid = Number(params.vid);
  const row = useQuery(
    pilotsRegisterQuery(listSearchSchema.parse({ q: String(vid), pageSize: 100 })),
  ).data?.items.find((candidate) => candidate.pilot.vid === vid);
  const name = row === undefined ? String(vid) : personName(row.pilot, t);
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  // The counts in plain sight above the bookings, as the controller's page has them.
  const counts =
    row === undefined
      ? t('events:registers.pilots.nothing')
      : row.limitedUntil === null
        ? t('events:registers.pilots.counts', { counted: row.counted, excused: row.excused })
        : t('events:registers.pilots.countsLimited', {
            counted: row.counted,
            excused: row.excused,
            date: moment(row.limitedUntil),
          });

  return (
    <PageShell
      title={name}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:registers.pilots.title'), to: PILOTS },
        { label: name },
      ]}
    >
      <p className="mb-4 text-sm">{counts}</p>
      <DataList
        columns={pilotBookingColumns}
        query={pilotBookingsQuery(vid, search)}
        labels="events:registers.bookings"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:registers.bookings.empty')}
        actions={(booking) => (booking.excusable ? <ExcuseUnflown booking={booking} /> : null)}
      />
    </PageShell>
  );
}

/** «Take away»: the booking not flown out of the pilot's record, with the note the audit and the pilot read. */
function ExcuseUnflown({ booking }: { booking: CheckedBookingRow }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const excuse = useExcuseUnflown();
  const [open, setOpen] = useState(false);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        excuse.reset();
      }}
      trigger={
        <Button variant="ghost" size="sm">
          {t('events:registers.bookings.excuse')}
        </Button>
      }
      title={t('events:registers.bookings.excuseTitle', { callsign: booking.callsign ?? '' })}
      description={t('events:registers.bookings.excuseDescription')}
    >
      <SchemaForm<UnflownExcuseFormValues>
        schema={unflownExcuseSchema}
        defaults={{ note: '' }}
        locales={[]}
        labels="events:registers.bookings.decision"
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={async (values) => {
          await excuse.mutateAsync({ id: booking.id, note: values.note });
          setOpen(false);
        }}
        submitLabel={t('events:registers.bookings.excuse')}
      />
    </Dialog>
  );
}

/** The shifts of one event over with how each went, and «Decide» where the reader may: the section «Outcomes» of its ATC (reading 10). */
export function ShiftOutcomesSection({ eventId }: { eventId: number }) {
  const { t } = useTranslation();
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:registers.outcomes.title')}>
      <H2>{t('events:registers.outcomes.title')}</H2>
      <p className="text-muted-foreground text-sm">{t('events:registers.outcomes.description')}</p>
      <ShiftDecisions
        query={shiftRowsQuery({ eventId }, search)}
        search={search}
        onSearchChange={setSearch}
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
  col.text('attendanceNote'),
];

const shiftColumnsWithEvent: readonly ColumnSpec<EventAtcShiftRowDto>[] = [
  col.localized('eventTitle'),
  ...shiftColumns,
];

/** A generated list of shifts with how each went, and «Decide» on the rows the server says the reader may decide. */
function ShiftDecisions({
  query,
  search,
  onSearchChange,
  withEvent = false,
}: {
  query: ReturnType<typeof shiftRowsQuery>;
  search: ListSearch;
  onSearchChange: (update: (current: ListSearch) => ListSearch) => void;
  withEvent?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });

  return (
    <DataList
      columns={withEvent ? shiftColumnsWithEvent : shiftColumns}
      query={query}
      labels="events:registers.shifts"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => onSearchChange((current) => ({ ...current, ...patch }))}
      emptyDescription={t('events:registers.shifts.empty')}
      actions={(row) => (row.decidable ? <DecideAttendance shift={row} /> : null)}
    />
  );
}

/**
 * «Decide»: how a shift went, among the outcomes still open to it — a no-show proposed or a shift unknown to any, a no-show to excused alone
 * (the no-show taken away) — with a note, which excusing asks.
 */
function DecideAttendance({ shift }: { shift: EventAtcShiftRowDto }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const decide = useDecideAttendance();
  const [open, setOpen] = useState(false);
  const allowed = decidableAttendances(shift.attendance);
  const first = allowed[0];

  if (first === undefined) {
    return null;
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        decide.reset();
      }}
      trigger={
        <Button variant="ghost" size="sm">
          {t(
            shift.attendance === 'NoShow'
              ? 'events:registers.shifts.takeAway'
              : 'events:registers.shifts.decide',
          )}
        </Button>
      }
      title={t('events:registers.shifts.decideTitle', {
        callsign: shift.callsign ?? '',
        controller: personName(shift.controller, t),
      })}
      description={t(
        shift.attendance === 'NoShow'
          ? 'events:registers.shifts.takeAwayDescription'
          : 'events:registers.shifts.decideDescription',
      )}
    >
      <SchemaForm<AttendanceFormValues>
        schema={attendanceSchema(allowed)}
        defaults={{ attendance: first, note: '' }}
        locales={[]}
        labels="events:registers.shifts.decision"
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={async (values) => {
          await decide.mutateAsync({ id: shift.id, attendance: values.attendance, note: values.note });
          setOpen(false);
        }}
        submitLabel={t('events:registers.shifts.save')}
      />
    </Dialog>
  );
}
