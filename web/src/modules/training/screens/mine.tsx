import { Badge, Button, H1, H2, H3, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, EmptyState, Notice, RatingBadge, useNotice } from '../../../shared/ui';
import { mineQuery, useCancelTraining, type MyTrainingPathDto, type TraineeTrainingDto } from '../api';

import { RefusalDetailText, StateBadge } from './parts';
import {
  REQUEST,
  formatHours,
  isCancellable,
  isTheoryRefusal,
  readyForExam,
  refusalDetail,
  stateMoment,
} from './trainee';

/**
 * The trainee's trainings (design M3 §4.1), `/training/mine`, for a signed in member: where they stand on each ladder — their
 * rating and hours, the training they may ask for next or why not now, how long the waiting still runs, what their trainer
 * marked them ready for — and every request and training of theirs, newest first, the refused and the cancelled ones too,
 * with «cancel» on a request nobody has accepted yet. One answer, `GET /api/training/mine`, the one the request reads: the
 * page decides nothing about the rules.
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
              <TrainingItem key={training.id} training={training} />
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
  const detail = refusalDetail(path);

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

      {path.refusal === null && path.next !== null ? (
        <div className="flex flex-col gap-2">
          <p className="flex flex-wrap items-center gap-2 text-sm">
            <span>{t('training:mine.canAsk')}</span>
            <RatingBadge kind={path.kind} shortName={path.next.shortName} />
          </p>
          {path.isMockExam ? <p className="text-sm">{t('training:mockExam')}</p> : null}
          <div>
            <Button asChild size="sm">
              <RouterAnchor href={`${REQUEST}?kind=${path.kind}`}>{t('training:request.send')}</RouterAnchor>
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-1 text-sm">
          <p>{t(path.refusal ?? 'training:errors.requestNothingToAsk')}</p>
          {detail === null ? null : (
            <p className="text-muted-foreground">
              <RefusalDetailText detail={detail} mineLink={false} />
            </p>
          )}
        </div>
      )}

      {path.next !== null && readyForExam(path, trainings) ? (
        <p className="text-sm font-semibold">
          {t('training:mine.readyForExamOn', { rating: path.next.shortName })}
        </p>
      ) : null}
    </section>
  );
}

/** A request or a training of theirs: its state, what it is, when, why it was refused, and what the report marked. */
function TrainingItem({ training }: { training: TraineeTrainingDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const at = stateMoment(training);

  return (
    <li className="flex flex-col gap-3 py-4 sm:flex-row sm:items-start sm:justify-between">
      <div className="flex min-w-0 flex-col gap-1">
        <div className="flex flex-wrap items-center gap-2">
          <StateBadge state={training.state} />
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

        {training.state !== 'Rejected' ? null : (
          <p className="text-sm">
            {isTheoryRefusal(training)
              ? t('training:mine.theoryRefusal')
              : training.rejectionReason === null
                ? t('training:mine.staffRefusal')
                : t('training:mine.staffRefusalReason', { reason: training.rejectionReason })}
          </p>
        )}

        {training.readyForMockExam || training.readyForExam ? (
          <div className="flex flex-wrap gap-2">
            {training.readyForMockExam ? (
              <Badge variant="flat" color="green" text={t('training:mine.readyForMockExam')} />
            ) : null}
            {training.readyForExam ? (
              <Badge variant="flat" color="green" text={t('training:mine.readyForExam')} />
            ) : null}
          </div>
        ) : null}

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

      {isCancellable(training) ? <CancelRequest training={training} /> : null}
    </li>
  );
}

/** «Cancel», while nobody has accepted the request (§2.2, d2): it stays on record, and makes nobody wait. */
function CancelRequest({ training }: { training: TraineeTrainingDto }) {
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
