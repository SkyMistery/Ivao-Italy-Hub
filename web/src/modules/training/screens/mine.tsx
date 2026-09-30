import { Badge, Button, H1, H2, H3, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { EmptyState, Notice, RatingBadge } from '../../../shared/ui';
import {
  mineQuery,
  shownState,
  type MyTrainingDto,
  type MyTrainingPathDto,
  type TraineeTrainingDto,
} from '../api';

import { choosableSlots } from './dates';
import { AskOrRefusal, CancelRequest, OutcomeText, ReadyForExamLine, ReportBoxes, StateBadge } from './parts';
import { REQUEST, formatHours, isCancellable, mineTrainingHref, stateMoment } from './trainee';

/**
 * The trainee's trainings (design M3 §4.1), `/training/mine`, for a signed in member: where they stand on each ladder — their
 * rating and hours, the training they may ask for next or why not now, how long the waiting still runs, what their trainer
 * marked them ready for — and every request and training of theirs, newest first, the refused, the cancelled and the closed
 * ones too, with «cancel» on a request nobody has accepted yet and the page of each, where the date is chosen (A8). One answer,
 * `GET /api/training/mine`, the one the request reads: the page decides nothing about the rules.
 */
export function MinePage() {
  const { t, i18n } = useTranslation();
  const mine = useQuery(mineQuery());

  if (mine.isPending) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (mine.data === undefined) {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-10">
        <Notice tone="error" title={describeProblem(mine.error, t, i18n.language) ?? t('errors.unknown')} />
      </div>
    );
  }

  const { paths, trainings } = mine.data;

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-2">
        <H1>{t('training:mine.title')}</H1>
        <Lead>{t('training:mine.lead')}</Lead>
      </header>

      <div className="grid gap-4 sm:grid-cols-2">
        {paths.map((path) => (
          <PathCard key={path.kind} path={path} trainings={trainings} />
        ))}
      </div>

      <section className="flex flex-col gap-4">
        <H2>{t('training:mine.trainings')}</H2>
        {trainings.length === 0 ? (
          <EmptyState
            title={t('training:mine.none')}
            description={t('training:mine.noneDescription')}
            action={
              <Button asChild>
                <RouterAnchor href={REQUEST}>{t('training:request.send')}</RouterAnchor>
              </Button>
            }
          />
        ) : (
          <ul className="flex flex-col divide-y">
            {trainings.map((training) => (
              <TrainingItem key={training.id} training={training} exam={mine.data} />
            ))}
          </ul>
        )}
      </section>
    </article>
  );
}

/**
 * One ladder (§2.2, d4): the trainee's rating and hours on it, then what they may ask for — or the first rule that refuses,
 * with what the answer says beside it, the waiting that still runs included —, and what the trainer marked them ready for.
 */
function PathCard({
  path,
  trainings,
}: {
  path: MyTrainingPathDto;
  trainings: readonly TraineeTrainingDto[];
}) {
  const { t, i18n } = useTranslation();

  return (
    <section className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4">
      <H3>{t(`training:kinds.${path.kind}`)}</H3>
      <dl className="grid grid-cols-[max-content_1fr] items-center gap-x-4 gap-y-1 text-sm">
        <dt className="text-muted-foreground">{t('training:mine.rating')}</dt>
        <dd>
          {path.ratingShortName === null ? (
            t('training:unknown')
          ) : (
            <RatingBadge kind={path.kind} shortName={path.ratingShortName} />
          )}
        </dd>
        <dt className="text-muted-foreground">{t('training:mine.hours')}</dt>
        <dd className="tabular-nums">{formatHours(path.hours, i18n.language) ?? t('training:unknown')}</dd>
      </dl>

      <AskOrRefusal path={path} />
      <ReadyForExamLine path={path} trainings={trainings} />
    </section>
  );
}

/**
 * A request or a training of theirs: its state, what it is, when, how it ended without a report — the site of the theory exam
 * beside a refusal for it (A13) —, that its report is published and what the trainer's boxes on it say, and its page — where the
 * dates the trainer proposed are chosen, said here when there are some, and where the report is read.
 */
function TrainingItem({
  training,
  exam,
}: {
  training: TraineeTrainingDto;
  exam: Pick<MyTrainingDto, 'theoryExamUrl' | 'theoryExamHint'>;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  // The dates still to come are counted from when the list was drawn, and stay the same while it is read.
  const [drawnAt] = useState(() => Date.now());
  const at = stateMoment(training);
  const toChoose = choosableSlots(training, drawnAt).length;

  return (
    <li className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <StateBadge state={shownState(training)} />
          <span className="font-semibold">{t(`training:kinds.${training.kind}`)}</span>
          {training.ratingShortName === null ? null : (
            <RatingBadge kind={training.kind} shortName={training.ratingShortName} />
          )}
          {training.position === null ? null : <span className="font-mono text-sm">{training.position}</span>}
          {training.isMockExam ? (
            <Badge variant="flat" color="purple" text={t('training:mine.mockExamBadge')} />
          ) : null}
        </div>

        <span className="text-muted-foreground text-sm tabular-nums">
          {[
            t('training:mine.requestedAt', { date: moment(training.requestedAt, { time: false }) }),
            // A session is a time, in UTC as the sentence says; the other moments are a day.
            ...(at === null
              ? []
              : [
                  t(`training:mine.moments.${training.state}`, {
                    date: moment(at, training.state === 'Scheduled' ? {} : { time: false }),
                  }),
                ]),
          ].join(' · ')}
        </span>

        <OutcomeText training={training} exam={exam} />

        {toChoose === 0 ? null : (
          <p className="text-sm font-semibold">{t('training:mine.datesWaiting', { count: toChoose })}</p>
        )}

        {training.state === 'Completed' ? <p className="text-sm">{t('training:mine.reportReady')}</p> : null}

        <ReportBoxes training={training} />

        {(
          [
            [t('training:request.fields.availabilityText'), training.availabilityText],
            [t('training:request.fields.notesText'), training.notesText],
          ] as const
        ).map(([label, text]) =>
          text === null ? null : (
            <p key={label} className="text-muted-foreground line-clamp-3 text-sm whitespace-pre-line">
              <span className="font-semibold">{label}:</span> {text}
            </p>
          ),
        )}
      </div>

      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {/* Its page: the dates to choose from while there are some (A8), the report once it is published (A9), and everything
            said of it. */}
        <Button asChild size="sm" variant={toChoose === 0 ? 'ghost' : 'primary'}>
          <RouterAnchor href={mineTrainingHref(training.id)}>
            {toChoose > 0
              ? t('training:mine.chooseDate')
              : training.state === 'Completed'
                ? t('training:mine.readReport')
                : t('training:mine.open')}
          </RouterAnchor>
        </Button>
        {isCancellable(training) ? <CancelRequest training={training} /> : null}
      </div>
    </li>
  );
}
