import { Badge } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { TrainingState } from '../api';

import { MINE, STATE_COLOURS, daysUntil, formatHours, type RefusalDetail } from './trainee';

/**
 * The pieces the trainee's two pages share (design M3 §4.1): what is said beside a refusal, the site of the theory exam, and
 * the state of a training. Pieces of the module's own screens, drawn from Atmosphere and the core's closed list, and not a
 * component of the list: nothing outside the training draws them.
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

/** Where a training is, in its colour. */
export function StateBadge({ state }: { state: TrainingState }) {
  const { t } = useTranslation();

  return <Badge variant="flat" color={STATE_COLOURS[state]} text={t(`training:states.${state}`)} />;
}
