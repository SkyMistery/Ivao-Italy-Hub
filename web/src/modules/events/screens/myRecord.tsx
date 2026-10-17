import { Badge, H2, H3, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { Notice } from '../../../shared/ui';
import { myRecordQuery, type PilotRecordDto } from '../api';

import { eventHref } from './cards';

/**
 * A member's own registers on `/events/mine` (design M4 §3.7, §4.5, §7.1, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, readings 3,
 * 5 and 8): the bookings they did not fly that count, whether they have the limits and until when, and how their shifts went with their
 * penalty. Nothing is said to a member with nothing in either. `LimitsNotice` is the same record where a booking was refused for the limits:
 * the refusal's key says why, the record the numbers and the date.
 */

/** The limits in words: how many bookings count, until when, and what the member books meanwhile in this event. */
function LimitsLine({ pilot }: { pilot: PilotRecordDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return pilot.limitedUntil === null ? null : (
    <>
      {t('events:mine.record.limited', {
        count: pilot.counted,
        date: moment(pilot.limitedUntil),
        perWindow: pilot.maxPerWindow,
        hours: pilot.windowHours,
        perEvent: pilot.maxPerEvent,
      })}
    </>
  );
}

/** Where a booking was refused for the limits: the numbers and the date of the member's record, with the limits valid in the event. */
export function LimitsNotice({ eventId }: { eventId: number }) {
  const { t } = useTranslation();
  const record = useQuery(myRecordQuery(eventId)).data;

  if (record === undefined || !record.pilot.limited) {
    return null;
  }

  return (
    <Notice
      tone="warning"
      title={t('events:mine.record.limitedTitle')}
      description={
        <span className="flex flex-col gap-1">
          <span>
            <LimitsLine pilot={record.pilot} />
          </span>
          <RouterAnchor href="/events/mine#record" className="underline">
            {t('events:mine.record.toRecord')}
          </RouterAnchor>
        </span>
      }
    />
  );
}

/** The member's two registers, when either has something in it. */
export function RecordSection() {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const record = useQuery(myRecordQuery()).data;

  if (record === undefined || (record.pilot.unflown.length === 0 && record.controller.shifts.length === 0)) {
    return null;
  }

  const { pilot, controller } = record;

  return (
    <section id="record" className="flex flex-col gap-4" aria-label={t('events:mine.record.title')}>
      <H2>{t('events:mine.record.title')}</H2>

      {pilot.unflown.length === 0 ? null : (
        <section className="flex flex-col gap-2" aria-label={t('events:mine.record.pilot')}>
          <H3 className="text-lg">{t('events:mine.record.pilot')}</H3>
          <p className="text-sm">
            {t('events:mine.record.unflown', { count: pilot.counted, threshold: pilot.threshold })}
          </p>
          {pilot.limited ? (
            <Notice
              tone="warning"
              title={t('events:mine.record.limitedTitle')}
              description={<LimitsLine pilot={pilot} />}
            />
          ) : null}
          <ul className="flex flex-col divide-y">
            {pilot.unflown.map((entry) => (
              <li key={entry.id} className="flex flex-col gap-1 py-2 text-sm">
                <span className="flex flex-wrap items-center gap-2">
                  <RouterAnchor href={eventHref(entry.eventSlug)} className="underline">
                    {read(entry.eventTitle) || entry.eventSlug}
                  </RouterAnchor>
                  <span className="font-mono">{entry.callsign}</span>
                  {entry.excused ? (
                    <Badge variant="flat" color="green" text={t('events:mine.record.excused')} />
                  ) : null}
                </span>
                <span className="tabular-nums">
                  <span className="font-mono">{entry.departureIcao}</span>{' '}
                  {t('calendar.utc', { at: moment(entry.offBlockUtc) })} →{' '}
                  <span className="font-mono">{entry.arrivalIcao}</span>
                </span>
                {entry.excusedNote === null ? null : <Subtle>{entry.excusedNote}</Subtle>}
              </li>
            ))}
          </ul>
        </section>
      )}

      {controller.shifts.length === 0 ? null : (
        <section className="flex flex-col gap-2" aria-label={t('events:mine.record.controller')}>
          <H3 className="text-lg">{t('events:mine.record.controller')}</H3>
          <p className="text-sm">
            {t('events:mine.record.shifts', {
              attended: controller.attended,
              noShows: controller.noShows,
              excused: controller.excused,
              penalty: Math.round(controller.penalty * 100) / 100,
            })}
          </p>
          <ul className="flex flex-col divide-y">
            {controller.shifts.map((shift) => (
              <li key={shift.id} className="flex flex-col gap-1 py-2 text-sm">
                <span className="flex flex-wrap items-center gap-2">
                  <RouterAnchor href={eventHref(shift.eventSlug)} className="underline">
                    {read(shift.eventTitle) || shift.eventSlug}
                  </RouterAnchor>
                  <span className="font-mono">{shift.callsign}</span>
                  <Badge
                    variant="flat"
                    color="gray"
                    text={t(`events:stats.shifts.options.attendance.${shift.attendance}`)}
                  />
                </span>
                <span className="tabular-nums">
                  {t('calendar.utc', { at: moment(shift.fromUtc) })} →{' '}
                  {t('calendar.utc', { at: moment(shift.toUtc) })}
                </span>
                {shift.note === null ? null : <Subtle>{shift.note}</Subtle>}
              </li>
            ))}
          </ul>
        </section>
      )}
    </section>
  );
}
