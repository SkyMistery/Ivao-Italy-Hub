import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  atcEventQuery,
  atcEventsQuery,
  atcPositionQuery,
  atcPositionToFormValues,
  atcPositionsQuery,
  divisionPositionsQuery,
  useDeleteAtcPosition,
  useSaveAtcPosition,
  type AtcEventDto,
  type EventAtcPositionDto,
} from '../api';
import { atcPositionSchema, emptyAtcPosition, type AtcPositionFormValues } from '../schemas';

import { ATC_SECTION, atcHome, isClosed } from './atcPaths';
import { Candidates } from './candidates';
import { ShiftOutcomesSection } from './registers';
import { NetworkBookingsSection } from './networkBookings';
import { RosterSection } from './roster';
import { TransfersSection } from './transfers';

/**
 * The ATC of the events in the staff's back office (design M4 §4.1, §6.3, §7.2, E11a; note 2026-10-12-le-postazioni-e-la-disponibilita):
 * the positions an event opens, as a generated list and a generated form, and on each position the controllers available in its hours.
 *
 * Who writes the positions does not always read the event: the staff of a FIR hold the area of the ATC and not the events', so they
 * reach it from the section «Events ATC» — the events with a roster, their heads read from the ATC's own endpoint — and an event's ATC
 * page; whoever reads the event has the same panel in a tab of its page. Which positions a reader sees, and writes, is the server's to
 * say: the staff of a FIR, their FIR's.
 */

const ATC = ATC_SECTION;

const eventColumns: readonly ColumnSpec<AtcEventDto>[] = [
  col.localized('title'),
  col.badge('state', 'events:events'),
  col.date('startsAtUtc'),
  col.date('closesAtUtc'),
];

/** «Events ATC» (§6.3): the events with a roster still to end, the soonest first, each opened on its ATC. */
export function AtcEventsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({}));

  return (
    <PageShell
      title={t('events:atc.title')}
      description={t('events:atc.description')}
      breadcrumb={[{ label: t('events:nav.section') }, { label: t('events:atc.title') }]}
    >
      <DataList
        columns={eventColumns}
        query={atcEventsQuery(search)}
        labels="events:atc.events"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:atc.empty')}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={atcHome(bootstrap, row.id)}>{t('events:atc.open')}</RouterAnchor>
          </Button>
        )}
      />
    </PageShell>
  );
}

/** The ATC of one event on a page of its own, for whoever does not read the event: its head, and its positions. */
export function AtcEventPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const id = Number(useParams({ strict: false }).id);
  const event = useQuery(atcEventQuery(id)).data;

  if (event === undefined) {
    return null;
  }

  const title = read(event.title) || event.slug;

  return (
    <PageShell
      title={title}
      note={t(`events:events.options.state.${event.state}`)}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:atc.title'), to: ATC },
        { label: title },
      ]}
    >
      <AtcPanel eventId={id} />
    </PageShell>
  );
}

const positionColumns: readonly ColumnSpec<EventAtcPositionDto>[] = [
  col.text('callsign', { sortable: true }),
  col.text('name'),
  col.text('fir'),
  col.date('fromUtc', { sortable: true }),
  col.date('toUtc'),
  col.number('candidates'),
];

/**
 * The positions of an event, by the hour they open, with how many controllers are available on each, and above them when the
 * availabilities close and how long a shift is. Whoever may open a position on the event has «New position» — the server says so of the
 * event (`canAddPositions`, #427): the staff of a FIR, on a position of their FIR —; each position opens on its page, with its candidates.
 */
export function AtcPanel({ eventId }: { eventId: number }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const event = useQuery(atcEventQuery(eventId)).data;

  if (event === undefined) {
    return null;
  }

  const base = `${ATC}/${eventId}/positions`;
  const closed = isClosed(event);
  const editable = !closed && event.canAddPositions;
  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:atc.positions.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <div className="flex flex-col gap-4">
      <p className="text-muted-foreground text-sm">
        {t('events:atc.head', { date: moment(event.closesAtUtc), minutes: event.shiftMinutes })}
      </p>
      {closed ? (
        <Notice tone="info" title={t('events:atc.closedEvent')} />
      ) : editable ? null : (
        <Notice tone="info" title={t('events:atc.readOnly')} />
      )}
      <DataList
        columns={positionColumns}
        query={atcPositionsQuery(eventId, search)}
        labels="events:atc.positions"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:atc.positions.empty')}
        {...(create === null ? {} : { toolbar: create, emptyAction: create })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/${row.id}`}>{t('events:atc.open')}</RouterAnchor>
          </Button>
        )}
      />
      <RosterSection event={event} />
      {/* Who booked the positions on the network (E15b): of an event that takes place, never of one cancelled. */}
      {event.state === 'Cancelled' ? null : <NetworkBookingsSection eventId={event.id} />}
      <TransfersSection event={event} />
      {/* After the event, how its shifts went, decided by the staff (E13b): never on one cancelled, whose shifts nobody held. */}
      {event.state !== 'Cancelled' && isClosed(event) ? <ShiftOutcomesSection eventId={event.id} /> : null}
    </div>
  );
}

/**
 * One position of an event: the event in the breadcrumb, the generated form — the callsign among the positions of the division —,
 * «delete» for one that exists, and under it who is available on it. Whoever reads the ATC and does not write it reads the form and the
 * candidates. Whether the reader writes it is the server's answer on the position (`editable`), or on the event for a new one
 * (`canAddPositions`, #427): the staff of a FIR write the positions of their FIR and read the others. Back to where the ATC of the event
 * lives for the reader.
 */
export function AtcPositionForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.positionId ?? 'new') === 'new' ? null : Number(params.positionId);

  const event = useQuery(atcEventQuery(eventId)).data;
  const position = useQuery({ ...atcPositionQuery(id ?? 0), enabled: id !== null }).data ?? null;
  // An event cancelled or over takes no position (the server refuses it): the page offers no «Save» and says why.
  const editable =
    event !== undefined &&
    !isClosed(event) &&
    (id === null ? event.canAddPositions : position?.editable === true);
  const positions = useQuery({ ...divisionPositionsQuery(eventId), enabled: editable }).data;
  const save = useSaveAtcPosition(id);
  const remove = useDeleteAtcPosition();

  if (event === undefined || (id !== null && position === null) || (editable && positions === undefined)) {
    return null;
  }

  const back = atcHome(bootstrap, eventId);
  const title =
    id === null ? t('events:atc.positions.create') : (position?.callsign ?? t('events:atc.positions.edit'));
  const refusal = describeProblem(remove.error, t, i18n.language);
  // The callsigns the form chooses from: the division's, with their names, and the one the position has if the core no longer lists it.
  const choices = (positions ?? []).map((choice) => ({
    value: choice.callsign,
    label: `${choice.callsign} — ${choice.name}`,
  }));
  if (position !== null && !choices.some((choice) => choice.value === position.callsign)) {
    choices.push({ value: position.callsign, label: position.callsign });
  }

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:atc.title'), to: ATC },
        { label: read(event.title) || event.slug, to: back },
        { label: title },
      ]}
      actions={
        position === null || !editable ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:atc.positions.delete.title')}
            description={t('events:atc.positions.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(position.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-8">
        {isClosed(event) ? <Notice tone="info" title={t('events:atc.closedEvent')} /> : null}
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm<AtcPositionFormValues>
          schema={atcPositionSchema(choices)}
          defaults={position === null ? emptyAtcPosition(eventId) : atcPositionToFormValues(position)}
          locales={[]}
          labels="events:atc.positions"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          {...(editable
            ? {
                onSubmit: async (values) => {
                  await save.mutateAsync(values);
                  void navigate({ href: back });
                },
                submitLabel: t('common.save'),
                secondaryAction: (
                  <Button asChild variant="ghost">
                    <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
                  </Button>
                ),
              }
            : {})}
        />
        {position === null ? null : <Candidates positionId={position.id} />}
      </div>
    </PageShell>
  );
}
