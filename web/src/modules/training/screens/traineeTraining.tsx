import { Badge, Button, H1, H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useParams, useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ApiError } from '../../../shared/api/problem';
import { describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, NotFound, RatingBadge, personName, useNotice } from '../../../shared/ui';
import {
  mineOneQuery,
  mineQuery,
  shownState,
  useChooseDate,
  type TraineeSlotDto,
  type TraineeTrainingDto,
} from '../api';

import { choosableSlots, spanText } from './dates';
import {
  CancelRequest,
  OutcomeText,
  RefusalDetailText,
  ReportView,
  SessionList,
  StateBadge,
  WhenText,
} from './parts';
import { nextOnTheLadder } from './report';
import { MINE, REQUEST, isCancellable, stateMoment } from './trainee';

/**
 * The page of one training of the trainee's (design M3 §4.1; A8, A9), `/training/mine/$id`, for its signed in trainee — the mails
 * of its dates and of its report point here: where it stands and what comes next, who trains it, and, while it waits for its
 * date, **the dates the trainer proposed as tiles**, one of which the trainee chooses (§2.5, d1); then the session, and «held»
 * from the day after it; **the report** once it is published — the grades, the marks, the comments for the trainee, the general
 * comment and the trainer's boxes —; the sessions that are over, rescheduled or not attended too; or why it ended without a
 * report. Read from `GET /api/training/mine/{id}`, whose DTO has no field of the staff's: no warning of a date, no note of the
 * sheet, no comment of the staff, no note of a session (§1.1). Another member's training is not found.
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
  // The site of the theory exam, beside a refusal for it (A13): the trainee's page of the trainings says where it is.
  const mine = useQuery(mineQuery());
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
          <p>{t('training:detail.trainer', { name: personName(training.trainer, t) })}</p>
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

      <OutcomeText training={training} exam={mine.data ?? null} />

      {/* What comes next, as the trainee is told it: a state that goes on has one, and so has one its session ended (A9); a
          training refused or closed has said it above. */}
      {shown === 'Requested' ||
      shown === 'Accepted' ||
      shown === 'Scheduled' ||
      shown === 'Held' ||
      (shown === 'Assigned' && slots.length === 0) ? (
        <p>{t(`training:detail.next.${shown}`)}</p>
      ) : shown === 'Completed' || shown === 'NoShow' ? (
        <AfterItEnded training={training} />
      ) : null}

      {training.state === 'Completed' ? (
        <section className="flex flex-col gap-3">
          <H2>{t('training:detail.report')}</H2>
          {training.completedAt === null ? null : (
            <p className="text-muted-foreground text-sm">
              {t('training:report.publishedOn', { date: moment(training.completedAt, { time: false }) })}
            </p>
          )}
          <ReportView report={training} audience="trainee" />
        </section>
      ) : null}

      {training.sessions.length === 0 ? null : (
        <section className="flex flex-col gap-3">
          <H2>{t('training:detail.sessions')}</H2>
          <SessionList sessions={training.sessions} timezone={bootstrap.division.timezone} />
        </section>
      )}

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
 * What comes next after a training whose session ended it (§2.6, §2.7): reported — the report is below — or not attended; then,
 * from the trainee's own trainings, what the ladder allows now: until when the waiting runs, or the next training to ask for —
 * a mock exam, as agreed with the trainer, when the server says so (§2.8). Nothing more when something else refuses a request
 * now: their trainings page says it.
 */
function AfterItEnded({ training }: { training: TraineeTrainingDto }) {
  const { t } = useTranslation();
  const mine = useQuery(mineQuery());
  const next = nextOnTheLadder(mine.data?.paths.find((path) => path.kind === training.kind));

  return (
    <div className="flex flex-col gap-2">
      <p>{t(`training:detail.next.${training.state}`)}</p>
      {next === null ? null : next.kind === 'wait' ? (
        <p className="text-sm">
          <RefusalDetailText detail={{ kind: 'waitUntil', until: next.until }} mineLink={false} />
        </p>
      ) : (
        <div className="flex flex-col gap-2 text-sm">
          <p>{t('training:detail.askAgain')}</p>
          {next.mockExam ? <p className="font-semibold">{t('training:mockExam')}</p> : null}
          <div>
            <Button asChild size="sm">
              <RouterAnchor href={`${REQUEST}?kind=${next.ladder}`}>
                {t('training:request.send')}
              </RouterAnchor>
            </Button>
          </div>
        </div>
      )}
    </div>
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
