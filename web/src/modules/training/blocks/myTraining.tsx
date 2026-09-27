import { Badge, Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { useMoment } from '../../../shared/i18n/useMoment';
import type { BlockComponentProps } from '../../../shared/modules';
import { RatingBadge } from '../../../shared/ui';
import { shownState, type MyTrainingDto, type TraineeTrainingDto } from '../api';
import { AskOrRefusal, ReadyForExamLine, StateBadge, WhenText } from '../screens/parts';
import { MINE, mineTrainingHref } from '../screens/trainee';

import { laddersNow } from './reading';

/**
 * `training.myTraining` (design M3 §4.3; note il-training-in-pubblico, «Da portare nel piano»: `/me` receives this block instead of a
 * page of its own): the reader's own training, ladder by ladder — the open request or training and what it waits for, the next date,
 * «choose the date», what may be asked for next or why not — the waiting, a ban —, «ready for the exam», and the last report, each a
 * link to its page. The answer of `/training/mine` itself, read by the pieces that page draws: nothing is decided twice. Always live
 * and with no property; its other half is `MyTrainingProvider`.
 */

/** What `MyTrainingProvider` answers with: the reader's page of the training, or `signedIn: false` for a visitor. */
export type MyTrainingData = Partial<MyTrainingDto> & { signedIn?: boolean };

export function MyTrainingBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  // The dates still to come are counted from when the block was drawn, and stay the same while it is read.
  const [drawnAt] = useState(() => Date.now());
  const mine = data as MyTrainingData | null | undefined;

  if (mine?.signedIn === undefined || bootstrap === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (!mine.signedIn || mine.paths === undefined || mine.trainings === undefined) {
    return <p className="text-muted-foreground text-sm">{t('training:blocks.signIn')}</p>;
  }

  const trainings = mine.trainings;

  return (
    <div className="flex flex-col gap-3">
      <ul className="divide-border flex flex-col divide-y">
        {laddersNow({ paths: mine.paths, trainings }, drawnAt).map((ladder) => (
          <li key={ladder.path.kind} className="flex flex-col gap-2 py-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-semibold">{t(`training:kinds.${ladder.path.kind}`)}</span>
              {ladder.path.ratingShortName === null ? null : (
                <RatingBadge kind={ladder.path.kind} shortName={ladder.path.ratingShortName} />
              )}
            </div>

            {ladder.open === null ? null : (
              <OpenTraining
                training={ladder.open}
                toChoose={ladder.toChoose}
                timezone={bootstrap.division.timezone}
              />
            )}
            {ladder.saysStanding ? <AskOrRefusal path={ladder.path} /> : null}
            <ReadyForExamLine path={ladder.path} trainings={trainings} />
            {ladder.lastReport === null ? null : <LastReport training={ladder.lastReport} />}
          </li>
        ))}
      </ul>

      <RouterAnchor href={MINE} className="text-sm underline">
        {t('training:mine.title')}
      </RouterAnchor>
    </div>
  );
}

/** The last report on a ladder: when it was published, and its page, where it is read. */
function LastReport({ training }: { training: TraineeTrainingDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return training.completedAt === null ? null : (
    <p className="text-sm">
      {t('training:blocks.myTraining.lastReport', { date: moment(training.completedAt, { time: false }) })}{' '}
      <RouterAnchor href={mineTrainingHref(training.id)} className="underline">
        {t('training:mine.readReport')}
      </RouterAnchor>
    </p>
  );
}

/**
 * The request or the training open on a ladder: where it stands, what it is, and what it waits for — the dates to choose, the date
 * the trainer will propose, the session, the report after it —, with its page: «choose the date» when there are some.
 */
function OpenTraining({
  training,
  toChoose,
  timezone,
}: {
  training: TraineeTrainingDto;
  toChoose: number;
  timezone: string;
}) {
  const { t } = useTranslation();
  const shown = shownState(training);

  return (
    <div className="flex flex-col gap-2 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <StateBadge state={shown} />
        {training.ratingShortName === null ? null : (
          <RatingBadge kind={training.kind} shortName={training.ratingShortName} />
        )}
        {training.position === null ? null : <span className="font-mono">{training.position}</span>}
        {training.isMockExam ? (
          <Badge variant="flat" color="purple" text={t('training:mine.mockExamBadge')} />
        ) : null}
      </div>

      {toChoose > 0 ? (
        <p className="font-semibold">{t('training:mine.datesWaiting', { count: toChoose })}</p>
      ) : training.state === 'Assigned' ? (
        <p>{t('training:blocks.myTraining.datesToCome')}</p>
      ) : null}
      {shown === 'Scheduled' && training.scheduledStartUtc !== null ? (
        <WhenText startsAtUtc={training.scheduledStartUtc} timezone={timezone} emphasis />
      ) : null}
      {shown === 'Held' ? <p>{t('training:detail.next.Held')}</p> : null}

      <div>
        <Button asChild size="sm" variant={toChoose === 0 ? 'ghost' : 'primary'}>
          <RouterAnchor href={mineTrainingHref(training.id)}>
            {toChoose > 0 ? t('training:mine.chooseDate') : t('training:mine.open')}
          </RouterAnchor>
        </Button>
      </div>
    </div>
  );
}
