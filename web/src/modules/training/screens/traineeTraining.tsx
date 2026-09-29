import { Badge, H1, H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useParams, useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ApiError } from '../../../shared/api/problem';
import { describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, NotFound, RatingBadge, useNotice } from '../../../shared/ui';
import {
  memberLabel,
  mineOneQuery,
  shownState,
  useChooseDate,
  type TraineeSlotDto,
  type TraineeTrainingDto,
} from '../api';

import { choosableSlots, spanText } from './dates';
import { CancelRequest, OutcomeText, StateBadge, WhenText } from './parts';
import { MINE, isCancellable, stateMoment } from './trainee';

/**
 * The page of one training of the trainee's (design M3 §4.1; A8), `/training/mine/$id`, for its signed in trainee — the mails of
 * its dates point here: where it stands and what comes next, who trains it, and, while it waits for its date, **the dates the
 * trainer proposed as tiles**, one of which the trainee chooses (§2.5, d1); then the session, and «held» from the day after it;
 * or why it ended without a report. Read from `GET /api/training/mine/{id}`, whose DTO has no field of the staff's: no warning of
 * a date, no note (§1.1). Another member's training is not found.
 */
export function TraineeTrainingPage() {
  const { t } = useTranslation();
  const { id = '' } = useParams({ strict: false });
  const trainingId = Number(id);
  const valid = Number.isInteger(trainingId) && trainingId > 0;
  const training = useQuery({ ...mineOneQuery(trainingId), enabled: valid });

  if (training.isPending && valid) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (training.data === undefined) {
    return <NotFound />;
  }

  return <TrainingScreen training={training.data} />;
}

function TrainingScreen({ training }: { training: TraineeTrainingDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_member' });
  // The dates still to come are the ones offered when the page was drawn; a choice reads the page again.
  const [drawnAt] = useState(() => Date.now());
  const slots = choosableSlots(training, drawnAt);
  const shown = shownState(training);
  const at = stateMoment(training);
  const kind = t(`training:kinds.${training.kind}`);

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-3">
        <RouterAnchor href={MINE} className="text-sm underline">
          {t('training:mine.title')}
        </RouterAnchor>
        <H1>
          {training.ratingShortName === null
            ? t('training:detail.titleNoRating', { kind })
            : t('training:detail.title', { kind, rating: training.ratingShortName })}
        </H1>
        <div className="flex flex-wrap items-center gap-2">
          <StateBadge state={shown} />
          <span className="font-semibold">{kind}</span>
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
        {training.trainer === null ? null : (
          <p>{t('training:detail.trainer', { name: memberLabel(training.trainer) })}</p>
        )}
      </header>

      {slots.length === 0 ? null : (
        <DatesToChoose training={training} slots={slots} timezone={bootstrap.division.timezone} />
      )}

      {training.state === 'Scheduled' && training.scheduledStartUtc !== null ? (
        <section className="flex flex-col gap-3">
          <H2>{t('training:detail.session')}</H2>
          <WhenText
            startsAtUtc={training.scheduledStartUtc}
            timezone={bootstrap.division.timezone}
            emphasis
          />
        </section>
      ) : null}

      <OutcomeText training={training} />

      {/* What comes next, as the trainee is told it: a state that goes on has one; one that ended has said it above. */}
      {shown === 'Requested' ||
      shown === 'Accepted' ||
      shown === 'Scheduled' ||
      shown === 'Held' ||
      (shown === 'Assigned' && slots.length === 0) ? (
        <p>{t(`training:detail.next.${shown}`)}</p>
      ) : null}

      {/* The two texts of the request, together: what the trainee wrote when they asked. */}
      {training.availabilityText === null && training.notesText === null ? null : (
        <div className="flex flex-col gap-2">
          {(
            [
              [t('training:request.fields.availabilityText'), training.availabilityText],
              [t('training:request.fields.notesText'), training.notesText],
            ] as const
          ).map(([label, text]) =>
            text === null ? null : (
              <p key={label} className="text-muted-foreground text-sm whitespace-pre-line">
                <span className="font-semibold">{label}:</span> {text}
              </p>
            ),
          )}
        </div>
      )}

      {isCancellable(training) ? (
        <div>
          <CancelRequest training={training} />
        </div>
      ) : null}
    </article>
  );
}

/**
 * The dates the trainer proposed (§2.5, d1), as tiles: each when it is, in UTC and where the division lives, and «choose this
 * date», asked once more before it goes — a date chosen is changed by the trainer alone. A choice the trainer overtook (a date
 * proposed or taken back meanwhile) is a 409: the page says so and shows the dates there are now.
 */
function DatesToChoose({
  training,
  slots,
  timezone,
}: {
  training: TraineeTrainingDto;
  slots: readonly TraineeSlotDto[];
  timezone: string;
}) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const notice = useNotice();
  const choose = useChooseDate(training);

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-col gap-1">
        <H2>{t('training:detail.dates.title')}</H2>
        {/* The trainer is named once, above: here, what the tiles are for. */}
        <p className="text-sm">{t('training:detail.dates.lead')}</p>
      </div>
      <ul className="grid gap-3 sm:grid-cols-2">
        {slots.map((slot) => {
          const when = spanText(slot.startsAtUtc, slot.endsAtUtc, moment);

          return (
            <li
              key={slot.id}
              className="bg-card text-card-foreground border-border flex flex-col gap-3 rounded-lg border p-4"
            >
              <WhenText
                startsAtUtc={slot.startsAtUtc}
                endsAtUtc={slot.endsAtUtc}
                timezone={timezone}
                emphasis
              />
              <div>
                <ConfirmDialog
                  triggerText={t('training:detail.dates.choose')}
                  triggerVariant="secondary"
                  title={t('training:detail.dates.confirmTitle', {
                    when,
                    local: t('training:time.local', {
                      when: spanText(slot.startsAtUtc, slot.endsAtUtc, moment, timezone),
                      zone: timezone,
                    }),
                  })}
                  description={t('training:detail.dates.confirmDescription')}
                  confirmText={t('training:detail.dates.confirm')}
                  confirmVariant="primary"
                  disabled={choose.isPending}
                  onConfirm={() =>
                    choose.mutate(slot.id, {
                      onSuccess: (dated) =>
                        notice({
                          tone: 'success',
                          title: t('training:detail.dates.chosen', {
                            when: moment(dated.scheduledStartUtc ?? slot.startsAtUtc),
                          }),
                        }),
                      onError: (error) =>
                        notice({
                          tone: 'error',
                          title:
                            error instanceof ApiError && error.status === 409
                              ? t('training:detail.dates.moved')
                              : (describeProblem(error, t, i18n.language) ?? t('errors.unknown')),
                        }),
                    })
                  }
                />
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
