import { Badge, H4, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import { atcHome } from '../screens/atcPaths';

/**
 * `events.atcCoverage` (design M4 §7.3, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 8): on `/staff` and the
 * dashboards of the staff of the events and of the ATC, the events with a roster of the next days that need somebody — pieces of their
 * positions nobody holds, availabilities still open, requests to hand a shift over to decide —, each with its numbers and the way to its
 * ATC. Always live and the reader's: its other half is `AtcCoverageProvider`, which counts on the positions the reader may read. The server
 * sends numbers and a word of a closed set; the block writes them.
 */

/** Where the roster of an event stands, as `RosterStage` says it on the server. */
export type RosterStage = 'Open' | 'ToPropose' | 'Proposed' | 'Published';

/** One event of the block, as `AtcCoverageEventDto` is on the server. */
export interface AtcCoverageEvent {
  id: number;
  slug: string;
  title: Record<string, string>;
  startsAtUtc: string;
  closesAtUtc: string;
  publishesAtUtc: string;
  stage: RosterStage;
  availabilities: number;
  pieces: number;
  uncovered: number;
  transfers: number;
}

/** What `AtcCoverageProvider` answers with: the events and how many days ahead it looked, or `signedIn: false` for a visitor. */
export interface AtcCoverageData {
  signedIn?: boolean;
  days?: number;
  events?: AtcCoverageEvent[];
}

const STAGE_COLOURS: Record<RosterStage, 'blue' | 'orange' | 'purple' | 'green'> = {
  Open: 'blue',
  ToPropose: 'orange',
  Proposed: 'purple',
  Published: 'green',
};

export function AtcCoverageBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const coverage = data as AtcCoverageData | null | undefined;

  // `null` is a request that failed, `undefined` one on its way: the two never read the same (the core's `NoRows`).
  if (coverage === null) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.unavailable')}</p>;
  }

  if (coverage?.signedIn === undefined || bootstrap === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (!coverage.signedIn || coverage.events === undefined) {
    return <p className="text-muted-foreground text-sm">{t('events:blocks.atcCoverage.signIn')}</p>;
  }

  if (coverage.events.length === 0) {
    return (
      <p className="text-muted-foreground text-sm">
        {t('events:blocks.atcCoverage.none', { count: coverage.days ?? 0 })}
      </p>
    );
  }

  return (
    <ul className="flex flex-col divide-y">
      {coverage.events.map((event) => (
        <li key={event.id} className="flex flex-col gap-1 py-3">
          <div className="flex flex-wrap items-center gap-2">
            <H4 className="text-base">
              <RouterAnchor href={atcHome(bootstrap, event.id)} className="underline">
                {read(event.title) || event.slug}
              </RouterAnchor>
            </H4>
            <Badge
              variant="flat"
              color={STAGE_COLOURS[event.stage]}
              text={t(`events:blocks.atcCoverage.stage.${event.stage}`)}
            />
          </div>
          <Subtle className="text-sm tabular-nums">
            {t('calendar.utc', { at: moment(event.startsAtUtc) })}
          </Subtle>
          <ul className="flex flex-col gap-0.5 text-sm">
            {event.stage === 'Open' ? (
              <li>
                {t('events:blocks.atcCoverage.open', {
                  count: event.availabilities,
                  date: moment(event.closesAtUtc),
                })}
              </li>
            ) : (
              <li>{t('events:blocks.atcCoverage.availabilities', { count: event.availabilities })}</li>
            )}
            {event.uncovered > 0 ? (
              <li>
                {t('events:blocks.atcCoverage.uncovered', { count: event.uncovered, pieces: event.pieces })}
              </li>
            ) : null}
            {event.transfers > 0 ? (
              <li>{t('events:blocks.atcCoverage.transfers', { count: event.transfers })}</li>
            ) : null}
            {event.stage === 'Proposed' ? (
              <li>{t('events:blocks.atcCoverage.publishesOn', { date: moment(event.publishesAtUtc) })}</li>
            ) : null}
          </ul>
        </li>
      ))}
    </ul>
  );
}
