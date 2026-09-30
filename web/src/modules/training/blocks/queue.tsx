import { Badge } from '@ivao/atmosphere-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { RatingBadge, personName } from '../../../shared/ui';
import type { StaffTrainingRowDto } from '../api';
import { staffTrainingHref } from '../screens/trainings';

/**
 * The pieces the two blocks of the staff (design M3 §4.3) share: a part of a queue — its title, which may be a link to the list — and a
 * training in it, as the staff's list shows it.
 */

/** A part of a queue on a dashboard: what it is, and the trainings in it. */
export function QueuePart({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-1">
      <div className="font-semibold">{title}</div>
      {/* The lines between the trainings in the colour of the border: without one they take the colour of the text. */}
      <ul className="divide-border flex flex-col divide-y">{children}</ul>
    </section>
  );
}

/**
 * A training in a queue: its trainee — a link to the page of the training, where the step is taken —, what it trains for, and what the
 * queue says of it beside.
 */
export function QueueRow({ row, children }: { row: StaffTrainingRowDto; children: ReactNode }) {
  const { t } = useTranslation();

  return (
    <li className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 py-2">
      <span className="flex min-w-0 flex-wrap items-center gap-2">
        <RouterAnchor href={staffTrainingHref(row.id)} className="font-semibold underline">
          {personName(row.trainee, t)}
        </RouterAnchor>
        {row.ratingShortName === null ? null : (
          <RatingBadge kind={row.kind} shortName={row.ratingShortName} />
        )}
        {row.position === null ? null : <span className="font-mono text-sm">{row.position}</span>}
        {row.isMockExam ? (
          <Badge variant="flat" color="purple" text={t('training:mine.mockExamBadge')} />
        ) : null}
      </span>
      <span className="text-muted-foreground text-sm tabular-nums">{children}</span>
    </li>
  );
}
