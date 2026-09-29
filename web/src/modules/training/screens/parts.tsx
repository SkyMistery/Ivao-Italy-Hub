import { Badge, Subtle } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, useNotice } from '../../../shared/ui';
import { useCancelTraining, type ShownState, type TraineeTrainingDto } from '../api';

import { closingOf, spanText } from './dates';
import { MINE, STATE_COLOURS, daysUntil, formatHours, isTheoryRefusal, type RefusalDetail } from './trainee';

/**
 * The pieces the trainee's pages share (design M3 §4.1), and what the staff's page shares with them: what is said beside a
 * refusal, the site of the theory exam, the state of a training, when a date or a session is, how a training ended, and «cancel».
 * Pieces of the module's own screens, drawn from Atmosphere and the core's closed list, and not a component of the list: nothing
 * outside the training draws them.
 */

/**
 * The sentence beside a refusal, from the same answer the refusal came in: until when, which training is open, how many hours.
 * The times are the server's, in UTC like every time of the hub, and the sentence says so.
 */
export function RefusalDetailText({ detail, mineLink }: { detail: RefusalDetail; mineLink: boolean }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  // The days left are counted from when the page was drawn, and stay the same while it is read.
  const [drawnAt] = useState(() => Date.now());

  switch (detail.kind) {
    case 'bannedUntil':
      return <>{t('training:refusal.bannedUntil', { date: moment(detail.until) })}</>;
    case 'bannedForever':
      return <>{t('training:refusal.bannedForever')}</>;
    case 'open':
      return (
        <>
          {t('training:refusal.open')}
          {mineLink ? (
            <>
              {' '}
              <RouterAnchor href={MINE} className="underline">
                {t('training:mine.title')}
              </RouterAnchor>
            </>
          ) : null}
        </>
      );
    case 'waitUntil':
      return (
        <>
          {t('training:refusal.waitUntil', {
            date: moment(detail.until),
            count: daysUntil(detail.until, drawnAt),
          })}
        </>
      );
    case 'hours': {
      const hours = formatHours(detail.hours, i18n.language);
      return (
        <>
          {hours === null
            ? t('training:refusal.hoursNeeded', { minimum: detail.minimum })
            : t('training:refusal.hours', { minimum: detail.minimum, hours })}
        </>
      );
    }
  }
}

/** Where the theory exam is taken, as the division wrote it in the settings (§12 n.12): it opens beside the hub. */
export function TheoryExamLink({ url }: { url: string }) {
  const { t } = useTranslation();

  return (
    <a href={url} target="_blank" rel="noopener noreferrer" className="underline">
      {t('training:theoryExam')}
    </a>
  );
}

/** Where a training is, in its colour: its state as the pages show it — held, from the day after its session (`shownState`). */
export function StateBadge({ state }: { state: ShownState }) {
  const { t } = useTranslation();

  return <Badge variant="flat" color={STATE_COLOURS[state]} text={t(`training:states.${state}`)} />;
}

/**
 * When a date proposed or a session is (§2.5): in UTC, and under it where the division lives (docs/UI-GUIDELINES.md, «Times») —
 * the hub is read by people flying in one and organising in the other. A span without an end is its start; `emphasis` for the
 * line a tile or an entry of a list is about.
 */
export function WhenText({
  startsAtUtc,
  endsAtUtc = null,
  timezone,
  emphasis = false,
}: {
  startsAtUtc: string;
  endsAtUtc?: string | null;
  timezone: string;
  emphasis?: boolean;
}) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <span className="flex flex-col leading-tight">
      <span className={emphasis ? 'font-semibold tabular-nums' : 'tabular-nums'}>
        {t('training:time.utc', { when: spanText(startsAtUtc, endsAtUtc, moment) })}
      </span>
      <Subtle className="tabular-nums">
        {t('training:time.local', {
          when: spanText(startsAtUtc, endsAtUtc, moment, timezone),
          zone: timezone,
        })}
      </Subtle>
    </span>
  );
}

/**
 * How a training of the trainee's ended without a report, as they read it: refused — by the hub, because they said the theory is
 * not passed, or by the staff with its reason —, or closed — by the staff with its reason, or by the hub, because they chose no
 * date in time (§2.5). Nothing for a training that goes on or ended otherwise.
 */
export function OutcomeText({ training }: { training: TraineeTrainingDto }) {
  const { t } = useTranslation();
  const closing = closingOf(training);

  if (training.state === 'Rejected') {
    return (
      <p className="text-sm">
        {isTheoryRefusal(training)
          ? t('training:mine.theoryRefusal')
          : training.rejectionReason === null
            ? t('training:mine.staffRefusal')
            : t('training:mine.staffRefusalReason', { reason: training.rejectionReason })}
      </p>
    );
  }

  if (closing === null) {
    return null;
  }

  return (
    <p className="text-sm whitespace-pre-line">
      {closing.by === 'staff'
        ? t('training:mine.closedByStaff', { reason: closing.reason })
        : t('training:mine.closedUnanswered')}
    </p>
  );
}

/** «Cancel», while nobody has accepted the request (§2.2, d2): it stays on record, and makes nobody wait. */
export function CancelRequest({ training }: { training: TraineeTrainingDto }) {
  const { t, i18n } = useTranslation();
  const cancel = useCancelTraining();
  const notice = useNotice();

  return (
    <ConfirmDialog
      triggerText={t('training:mine.cancel')}
      triggerVariant="secondary"
      title={t('training:mine.cancelTitle')}
      description={t('training:mine.cancelDescription')}
      confirmText={t('training:mine.cancelConfirm')}
      confirmVariant="destructive"
      disabled={cancel.isPending}
      onConfirm={() =>
        cancel.mutate(training, {
          onSuccess: () => notice({ tone: 'success', title: t('training:mine.cancelled') }),
          onError: (error) =>
            notice({
              tone: 'error',
              title: describeProblem(error, t, i18n.language) ?? t('errors.unknown'),
            }),
        })
      }
    />
  );
}
