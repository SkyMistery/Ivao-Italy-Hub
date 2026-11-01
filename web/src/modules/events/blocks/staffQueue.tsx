import { H4, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import { atcHome } from '../screens/atcPaths';

/**
 * `events.staffQueue` (design M4 §7.3, E13b; note 2026-10-16-i-no-show-i-registri-e-i-limiti, reading 11): on the dashboards of the staff of
 * the events and of the ATC, what waits for a decision of theirs — no-shows the check proposed, shifts it left unknown, requests to hand a
 * shift over —, and the reports of support to validate (E14a), one event per row with its numbers, the way to its ATC and to its reports.
 * Always live and the reader's: its other half is `StaffQueueProvider`, which counts only the rows the one handler lets the reader decide,
 * never their own. The server sends numbers; the block writes them.
 */

/** One event of the block, as `StaffQueueEventDto` is on the server. */
export interface StaffQueueEvent {
  id: number;
  slug: string;
  title: Record<string, string>;
  startsAtUtc: string;
  endsAtUtc: string;
  noShows: number;
  toMark: number;
  transfers: number;
  reports: number;
}

/** The tab of an event's reports of support, in the staff's back office. */
function reportsHref(eventId: number): string {
  return `/staff/events/${eventId}?tab=reports`;
}

/** What `StaffQueueProvider` answers with: the events, or `signedIn: false` for a visitor. */
export interface StaffQueueData {
  signedIn?: boolean;
  events?: StaffQueueEvent[];
}

export function StaffQueueBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const queue = data as StaffQueueData | null | undefined;

  // `null` is a request that failed, `undefined` one on its way: the two never read the same (the core's `NoRows`).
  if (queue === null) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.unavailable')}</p>;
  }

  if (queue?.signedIn === undefined || bootstrap === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (!queue.signedIn || queue.events === undefined) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.staffQueue.signIn')}</p>;
  }

  if (queue.events.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.staffQueue.none')}</p>;
  }

  return (
    <ul className="flex flex-col divide-y">
      {queue.events.map((event) => (
        <li key={event.id} className="flex flex-col gap-1 py-3">
          <H4 className="text-base">
            <RouterAnchor
              // An event with reports alone waiting goes to them: its validators may not read its ATC.
              href={
                event.noShows + event.toMark + event.transfers === 0
                  ? reportsHref(event.id)
                  : atcHome(bootstrap, event.id)
              }
              className="underline"
            >
              {read(event.title) || event.slug}
            </RouterAnchor>
          </H4>
          <Subtle className="text-sm tabular-nums">
            {t('calendar.utc', { at: moment(event.startsAtUtc) })}
          </Subtle>
          <ul className="flex flex-col gap-0.5 text-sm">
            {event.noShows > 0 ? (
              <li>{t('events:blocks.staffQueue.noShows', { count: event.noShows })}</li>
            ) : null}
            {event.toMark > 0 ? (
              <li>{t('events:blocks.staffQueue.toMark', { count: event.toMark })}</li>
            ) : null}
            {event.transfers > 0 ? (
              <li>{t('events:blocks.staffQueue.transfers', { count: event.transfers })}</li>
            ) : null}
            {event.reports > 0 ? (
              <li>
                <RouterAnchor href={reportsHref(event.id)} className="underline">
                  {t('events:blocks.staffQueue.reports', { count: event.reports })}
                </RouterAnchor>
              </li>
            ) : null}
          </ul>
        </li>
      ))}
    </ul>
  );
}
