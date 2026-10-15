import { Button, H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
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
  atcPositionsQuery,
  rosterQuery,
  shiftQuery,
  shiftToFormValues,
  useDeleteShift,
  useSaveShift,
  type AtcEventDto,
  type RosterRow,
} from '../api';
import { emptyShift, shiftSchema, shiftSearchSchema, type ShiftFormValues } from '../schemas';

import { ATC_SECTION, atcHome, hasStarted, isClosed } from './atcPaths';
import { Candidates } from './candidates';
import { rosterLines } from './rosterWords';

/**
 * The roster of an event in the staff's back office (design M4 §4.3, §4.4, §7.2, E11b; note 2026-10-13-la-proposta-del-roster): a generated
 * list by position and hour of the shifts and of the pieces nobody holds — uncovered, said by their badge —, with the controller, the why of
 * a proposed shift and the warnings of every shift in words of the language on screen; «Add» on an uncovered piece, «Move» and «Take away»
 * on a shift, for whoever writes the ATC of the event. Before the proposal a sentence says when it comes.
 */

const columns: readonly ColumnSpec<RosterRow>[] = [
  col.text('callsign'),
  col.date('fromUtc'),
  col.date('toUtc'),
  col.badge('state', 'events:atc.roster'),
  col.person('controller'),
  col.text('rating'),
  col.list('why'),
  col.list('alerts'),
];

/** The roster of the event, under its positions: what the job proposed and the staff corrected, or when it will be proposed. */
export function RosterSection({ event }: { event: AtcEventDto }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${ATC_SECTION}/${event.id}/shifts`;
  const started = hasStarted(event);

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:atc.roster.title')}>
      <H2>{t('events:atc.roster.title')}</H2>
      {event.rosterProposedAt === null ? (
        <p className="text-muted-foreground text-sm">
          {t('events:atc.roster.notYet', { date: moment(event.closesAtUtc) })}
        </p>
      ) : (
        <>
          <p className="text-muted-foreground text-sm">
            {t('events:atc.roster.proposed', { date: moment(event.rosterProposedAt) })}
          </p>
          <DataList
            columns={columns}
            query={rosterQuery(event.id, search, i18n.language, (row) => rosterLines(row, t, i18n.language))}
            labels="events:atc.roster"
            locale={i18n.language}
            defaultLocale={bootstrap.division.defaultLocale}
            timezone={bootstrap.division.timezone}
            search={search}
            onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
            emptyDescription={t('events:atc.roster.empty')}
            actions={(row) => (row.editable ? <RowActions row={row} base={base} started={started} /> : null)}
          />
        </>
      )}
    </section>
  );
}

/**
 * «Add» on an uncovered piece, with its position and its hours; «Move» and «Take away» on a shift — for a row the server says the reader may
 * write (the review of #262, point 5), and «Take away» only before the event starts (Carmine's answer on #259).
 */
function RowActions({ row, base, started }: { row: RosterRow; base: string; started: boolean }) {
  const { t, i18n } = useTranslation();
  const remove = useDeleteShift();
  const refusal = describeProblem(remove.error, t, i18n.language);

  if (row.shiftId === null) {
    const query = new URLSearchParams({
      positionId: String(row.positionId),
      fromUtc: row.fromUtc,
      toUtc: row.toUtc,
    });

    return (
      <Button asChild variant="ghost" size="sm">
        <RouterAnchor href={`${base}/new?${query.toString()}`}>{t('events:atc.roster.add')}</RouterAnchor>
      </Button>
    );
  }

  const shiftId = row.shiftId;

  return (
    <div className="flex flex-col items-end gap-1">
      <div className="flex gap-1">
        <Button asChild variant="ghost" size="sm">
          <RouterAnchor href={`${base}/${shiftId}`}>{t('events:atc.roster.move')}</RouterAnchor>
        </Button>
        {started ? null : (
          <ConfirmDialog
            triggerText={t('events:atc.roster.remove.trigger')}
            title={t('events:atc.roster.remove.title')}
            description={t('events:atc.roster.remove.description')}
            confirmText={t('events:atc.roster.remove.confirm')}
            disabled={remove.isPending}
            onOpenChange={(open) => {
              if (open) {
                remove.reset();
              }
            }}
            onConfirm={() => remove.mutate(shiftId)}
          />
        )}
      </div>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
    </div>
  );
}

/**
 * One shift of the roster (§4.4): the event in the breadcrumb, the generated form — the position among the event's, the hours, the
 * controller's VID —, «Take away» for one that exists, and under it who is available on the position. Back to where the ATC of the event
 * lives for the reader.
 */
export function ShiftForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters and search are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const search = shiftSearchSchema.parse(useSearch({ strict: false }));
  const eventId = Number(params.id);
  const id = (params.shiftId ?? 'new') === 'new' ? null : Number(params.shiftId);

  const event = useQuery(atcEventQuery(eventId)).data;
  const positions = useQuery(atcPositionsQuery(eventId)).data?.items;
  const shift = useQuery({ ...shiftQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const save = useSaveShift(id);
  const remove = useDeleteShift();

  if (event === undefined || positions === undefined || (id !== null && shift === null)) {
    return null;
  }

  // What the server would take (the review of #262, point 5): a roster proposed, of an event not cancelled nor over, on a position the
  // reader writes the ATC of — the server says it position by position, a FIR team's their FIR's —, never a shift of the reader's own;
  // and «Take away» only before the event starts (Carmine's answer on #259).
  const closed = isClosed(event);
  const proposed = event.rosterProposedAt !== null;
  const started = hasStarted(event);
  const writable = positions.filter((position) => position.editable);
  const editable =
    !closed &&
    proposed &&
    (shift === null
      ? writable.length > 0
      : writable.some((position) => position.id === shift.positionId) &&
        shift.controllerVid !== bootstrap.user?.vid);
  const back = atcHome(bootstrap, eventId);
  const title = id === null ? t('events:atc.shifts.create') : t('events:atc.shifts.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);
  // The positions of the event, each with its hours: the same callsign may open twice in an evening. Writing, the reader's own.
  const choices = (editable ? writable : positions).map((position) => ({
    value: String(position.id),
    label: `${position.callsign} · ${moment(position.fromUtc)} – ${moment(position.toUtc)}`,
  }));
  const positionId = shift?.positionId ?? search.positionId;
  // From the start a shift is only cut, to now or later (Carmine's answer on #262): the form says so before the server does.
  const why = closed
    ? t('events:atc.closedEvent')
    : !proposed
      ? t('events:atc.roster.notYet', { date: moment(event.closesAtUtc) })
      : started && shift !== null
        ? t('events:errors.shiftAfterStart')
        : null;

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:atc.title'), to: ATC_SECTION },
        { label: read(event.title) || event.slug, to: back },
        { label: title },
      ]}
      actions={
        shift === null || !editable || started ? undefined : (
          <ConfirmDialog
            triggerText={t('events:atc.roster.remove.trigger')}
            title={t('events:atc.roster.remove.title')}
            description={t('events:atc.roster.remove.description')}
            confirmText={t('events:atc.roster.remove.confirm')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(shift.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-8">
        {why === null ? null : <Notice tone="info" title={why} />}
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm<ShiftFormValues>
          schema={shiftSchema(choices)}
          defaults={shift === null ? emptyShift(search) : shiftToFormValues(shift)}
          locales={[]}
          labels="events:atc.shifts"
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
        {positionId === undefined ? null : <Candidates positionId={positionId} />}
      </div>
    </PageShell>
  );
}
