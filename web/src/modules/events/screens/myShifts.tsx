import { Badge, Button, H2, H3, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, Dialog, Notice, personName } from '../../../shared/ui';
import {
  myAvailabilityQuery,
  myShiftsQuery,
  useAskTransfer,
  useWithdrawAvailability,
  useWithdrawTransfer,
  type MyAvailabilityDto,
  type MyShiftDto,
} from '../api';
import { transferSchema, type TransferFormValues } from '../schemas';

import { STATE_COLOURS, eventHref, rosterHref } from './cards';
import { shiftsByEvent } from './myBookings';

/**
 * A controller's part of `/events/mine` (design M4 §4.4, §4.4-bis, §7.1, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione,
 * reading 6): their shifts of the published rosters — under their events, by their start, with the way to the roster, «Hand over» until
 * the event starts, and the request waiting or refused under its shift, with «Take back» while it waits —, and their availabilities (E11a's
 * reading 8), with «Change» on the event's page and «Withdraw» until the close. Each section is drawn when its answer arrives: one that
 * failed says so there, and the rest of the page is read all the same.
 */

/** The member's shifts, when they have any, or an answer that failed. */
export function ShiftsSection() {
  const { t, i18n } = useTranslation();
  const shifts = useQuery(myShiftsQuery());
  const failed = describeProblem(shifts.error, t, i18n.language);
  const rows = shifts.data ?? [];

  if (rows.length === 0 && failed === null) {
    return null;
  }

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:mine.shifts.title')}>
      <H2>{t('events:mine.shifts.title')}</H2>
      {failed === null ? null : <Notice tone="error" title={failed} />}
      <ul className="flex flex-col gap-6">
        {shiftsByEvent(rows).map((group) => (
          <ShiftGroup key={group[0]!.eventId} shifts={group} />
        ))}
      </ul>
    </section>
  );
}

function ShiftGroup({ shifts }: { shifts: readonly MyShiftDto[] }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const first = shifts[0]!;
  const title = read(first.eventTitle) || first.eventSlug;

  return (
    <li
      className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4"
      aria-label={title}
    >
      <div className="flex flex-wrap items-center gap-2">
        <H3 className="text-lg">
          <RouterAnchor href={eventHref(first.eventSlug)} className="underline">
            {title}
          </RouterAnchor>
        </H3>
        <Badge
          variant="flat"
          color={STATE_COLOURS[first.eventState] ?? 'gray'}
          text={t(`events:events.options.state.${first.eventState}`)}
        />
        <RouterAnchor href={rosterHref(first.eventSlug)} className="text-sm underline">
          {t('events:mine.shifts.roster')}
        </RouterAnchor>
      </div>
      <ul className="flex flex-col divide-y">
        {shifts.map((shift) => (
          <ShiftItem key={shift.id} shift={shift} />
        ))}
      </ul>
    </li>
  );
}

/**
 * One shift: the position and its hours in UTC; the request to hand it over, when there is one still about the member — waiting, with
 * «Take back», or refused, with the staff's note —; and «Hand over» while it may be.
 */
function ShiftItem({ shift }: { shift: MyShiftDto }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const withdraw = useWithdrawTransfer();
  const refusal = describeProblem(withdraw.error, t, i18n.language);
  const transfer = shift.transfer;

  return (
    <li className="flex flex-col gap-2 py-3 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <span className="font-mono font-semibold">{shift.callsign}</span>
        <span className="text-sm tabular-nums">
          {t('calendar.utc', { at: moment(shift.fromUtc) })} →{' '}
          {t('calendar.utc', { at: moment(shift.toUtc) })}
        </span>
        {transfer === null ? null : (
          <Subtle className="text-sm">
            {transfer.status === 'Pending'
              ? transfer.to === null
                ? t('events:mine.shifts.pendingNobody')
                : t('events:mine.shifts.pendingTo', { name: personName(transfer.to, t) })
              : t('events:mine.shifts.refused', { note: transfer.decisionNote ?? '' })}
          </Subtle>
        )}
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
      </div>

      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {transfer?.status === 'Pending' ? (
          <ConfirmDialog
            triggerText={t('events:mine.shifts.takeBack')}
            triggerVariant="secondary"
            title={t('events:mine.shifts.takeBackTitle', { callsign: shift.callsign })}
            description={t('events:mine.shifts.takeBackDescription')}
            confirmText={t('events:mine.shifts.takeBack')}
            disabled={withdraw.isPending}
            onConfirm={() => withdraw.mutate(shift.id)}
          />
        ) : null}
        {shift.transferable ? <TransferDialog shift={shift} /> : null}
      </div>
    </li>
  );
}

/**
 * «Hand over» (§4.4-bis): the generated form of the request — to whom by VID, or nobody, and why —, in a dialog. The shift stays the
 * member's until the staff decide; what the server refuses lands on its field.
 */
function TransferDialog({ shift }: { shift: MyShiftDto }) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const ask = useAskTransfer();
  const [open, setOpen] = useState(false);

  if (bootstrap === undefined) {
    return null;
  }

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (next) {
          ask.reset();
        }
      }}
      trigger={<Button variant="outline">{t('events:mine.shifts.handOver')}</Button>}
      title={t('events:mine.shifts.handOverTitle', { callsign: shift.callsign })}
      description={t('events:mine.shifts.handOverDescription')}
    >
      <SchemaForm<TransferFormValues>
        schema={transferSchema}
        defaults={{ note: '' }}
        locales={[]}
        labels="events:mine.transfer"
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={async (values) => {
          await ask.mutateAsync({ shiftId: shift.id, toVid: values.toVid ?? null, note: values.note });
          setOpen(false);
        }}
        submitLabel={t('events:mine.shifts.handOver')}
      />
    </Dialog>
  );
}

/** The member's availabilities (E11a, reading 8), when they have any, or an answer that failed. */
export function AvailabilitiesSection() {
  const { t, i18n } = useTranslation();
  const availabilities = useQuery(myAvailabilityQuery());
  const failed = describeProblem(availabilities.error, t, i18n.language);
  const rows = availabilities.data ?? [];

  if (rows.length === 0 && failed === null) {
    return null;
  }

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:mine.availability.title')}>
      <H2>{t('events:mine.availability.title')}</H2>
      {failed === null ? null : <Notice tone="error" title={failed} />}
      <ul className="flex flex-col gap-4">
        {rows.map((availability) => (
          <AvailabilityItem key={availability.id} availability={availability} />
        ))}
      </ul>
    </section>
  );
}

/** One availability: its event, its windows in UTC, the note, «Change» on the event's page and «Withdraw» until the close. */
function AvailabilityItem({ availability }: { availability: MyAvailabilityDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const withdraw = useWithdrawAvailability();
  const refusal = describeProblem(withdraw.error, t, i18n.language);
  const title = read(availability.eventTitle) || availability.eventSlug;

  return (
    <li
      className="bg-card text-card-foreground border-border flex flex-col gap-2 rounded-lg border p-4"
      aria-label={title}
    >
      <H3 className="text-lg">
        <RouterAnchor href={eventHref(availability.eventSlug)} className="underline">
          {title}
        </RouterAnchor>
      </H3>
      <ul className="flex flex-col gap-1 text-sm tabular-nums">
        {availability.windows.map((window) => (
          <li key={window.fromUtc}>
            {t('calendar.utc', { at: moment(window.fromUtc) })} →{' '}
            {t('calendar.utc', { at: moment(window.toUtc) })}
          </li>
        ))}
      </ul>
      {availability.note === null ? null : <Subtle className="text-sm">{availability.note}</Subtle>}
      <Subtle className="text-sm">
        {availability.withdrawable
          ? t('events:mine.availability.until', { date: moment(availability.closesAtUtc) })
          : t('events:mine.availability.closed')}
      </Subtle>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      {availability.changeable || availability.withdrawable ? (
        <div className="flex flex-wrap gap-2">
          {availability.changeable ? (
            <Button asChild variant="outline">
              <RouterAnchor href={`${eventHref(availability.eventSlug)}#atc`}>
                {t('events:mine.availability.change')}
              </RouterAnchor>
            </Button>
          ) : null}
          {availability.withdrawable ? (
            <ConfirmDialog
              triggerText={t('events:mine.withdraw')}
              triggerVariant="secondary"
              title={t('events:mine.availability.withdrawTitle', { title })}
              description={t('events:mine.availability.withdrawDescription')}
              confirmText={t('events:mine.withdraw')}
              confirmVariant="destructive"
              disabled={withdraw.isPending}
              onConfirm={() => withdraw.mutate(availability.eventId)}
            />
          ) : null}
        </div>
      ) : null}
    </li>
  );
}
