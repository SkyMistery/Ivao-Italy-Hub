import { Badge, Button, Subtle } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import type { LocalizedString } from '../../../shared/api/bootstrap';
import { describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ConfirmDialog, RatingBadge, personName, useNotice } from '../../../shared/ui';
import {
  useCancelTraining,
  type MyTrainingDto,
  type MyTrainingPathDto,
  type SessionOutcome,
  type ShownState,
  type TraineeEvaluationDto,
  type TraineeTrainingDto,
  type TrainingMemberDto,
} from '../api';

import { closingOf, spanText } from './dates';
import { OUTCOME_COLOURS, evaluationSays } from './report';
import {
  MINE,
  REQUEST,
  STATE_COLOURS,
  daysUntil,
  formatHours,
  isTheoryRefusal,
  readyForExam,
  refusalDetail,
  type RefusalDetail,
} from './trainee';

/**
 * The pieces the trainee's pages share (design M3 §4.1), and what the staff's page shares with them: what is said beside a
 * refusal, what may be asked for on a ladder and «ready for the exam», the site of the theory exam, the state of a training, when a
 * date or a session is, how a training ended, «cancel», a report as it was published, the trainer's boxes, and the sessions that are
 * over. Pieces of the module's own screens and blocks, drawn from Atmosphere and the core's closed list, and not a component of the
 * list: nothing outside the training draws them.
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

/**
 * What a trainee may ask for on one ladder (§2.2, d4): the training proposed, a mock exam when the server says so, and «Request
 * training» — or the first rule that refuses, with what the answer says beside it, the waiting that still runs included. Their page
 * of the trainings (A6b) and the block of their own training (A10b) say it the same way.
 */
export function AskOrRefusal({ path }: { path: MyTrainingPathDto }) {
  const { t } = useTranslation();
  const detail = refusalDetail(path);

  return path.refusal === null && path.next !== null ? (
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
  );
}

/** «Ready for the exam» on a ladder, when the trainer's box on its last report still says it (`readyForExam`); nothing otherwise. */
export function ReadyForExamLine({
  path,
  trainings,
}: {
  path: MyTrainingPathDto;
  trainings: readonly TraineeTrainingDto[];
}) {
  const { t } = useTranslation();

  return path.next !== null && readyForExam(path, trainings) ? (
    <p className="text-sm font-semibold">
      {t('training:mine.readyForExamOn', { rating: path.next.shortName })}
    </p>
  ) : null;
}

/**
 * Where the theory exam is taken, as the division wrote it in the settings (§12 n.12): it opens beside the hub. Under it, for a
 * trainee, what to do there to book the exam, in the language on screen, when the division wrote it (A13); the staff's reminder
 * has the link alone.
 */
export function TheoryExamLink({ url, hint = null }: { url: string; hint?: LocalizedString | null }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const words = read(hint).trim();

  const link = (
    <a href={url} target="_blank" rel="noopener noreferrer" className="underline">
      {t('training:theoryExam')}
    </a>
  );

  return words === '' ? (
    link
  ) : (
    <span className="flex flex-col gap-1">
      {link}
      <span className="whitespace-pre-line">{words}</span>
    </span>
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
 * not passed, with the site of the exam and what to do there when the division wrote them (A13), or by the staff with its reason
 * —, or closed — by the staff with its reason, or by the hub, because they chose no date in time (§2.5). Nothing for a training
 * that goes on or ended otherwise.
 */
export function OutcomeText({
  training,
  exam = null,
}: {
  training: TraineeTrainingDto;
  exam?: Pick<MyTrainingDto, 'theoryExamUrl' | 'theoryExamHint'> | null;
}) {
  const { t } = useTranslation();
  const closing = closingOf(training);

  if (training.state === 'Rejected') {
    const theory = isTheoryRefusal(training);

    return (
      <div className="flex flex-col gap-1 text-sm">
        <p>
          {theory
            ? t('training:mine.theoryRefusal')
            : training.rejectionReason === null
              ? t('training:mine.staffRefusal')
              : t('training:mine.staffRefusalReason', { reason: training.rejectionReason })}
        </p>
        {theory && exam?.theoryExamUrl ? (
          <TheoryExamLink url={exam.theoryExamUrl} hint={exam.theoryExamHint} />
        ) : null}
      </div>
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

/**
 * A report as it was published, as a page reads it: the staff's answer (A9a) with the notes of the staff — none when the reader
 * is the training's trainee (`reservedLeftOut`) —, or the trainee's, which has no field of the staff's at all.
 */
interface PublishedReport {
  readonly sheet: readonly (TraineeEvaluationDto & { readonly staffNote?: string | null })[];
  readonly generalComment: string | null;
  readonly staffComment?: string | null;
  readonly readyForMockExam: boolean;
  readonly readyForExam: boolean;
  readonly cooldownWaived: boolean;
}

/**
 * A report as it was published (design M3 §2.7, §4.1, §4.2): every item of its sheet in the order the report copied it — its
 * title and section as they were then, the grade, the mark or «N/A» (d4) —, the comment written for the trainee under each and,
 * for the staff, the note of the staff; then the general comment, the comment for the staff, and the trainer's boxes. It draws
 * what it is handed: the trainee's answer has no note of the staff's (§4.1), and neither has the page of a trainer reading their
 * own training. The staff read who each text is for; the trainee reads the comments that are theirs.
 */
export function ReportView({ report, audience }: { report: PublishedReport; audience: 'staff' | 'trainee' }) {
  const { t } = useTranslation();
  const read = useLocalized();

  return (
    <div className="flex flex-col gap-4">
      {report.sheet.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('training:report.noSheet')}</p>
      ) : (
        // The lines between the items in the colour of the border around them: without one they take the colour of the text.
        <ul className="border-border divide-border flex flex-col divide-y rounded-md border">
          {report.sheet.map((item, index) => (
            // The report's own copy, in the order it keeps: nothing reorders it while it is read.
            <li key={index} className="flex flex-col gap-1 p-3 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="font-semibold">{read(item.title)}</span>
                <EvaluationBadge item={item} />
              </div>
              <Subtle>{t(`training:sheets.options.section.${item.section}`)}</Subtle>
              {item.traineeComment === null ? null : (
                <p className="whitespace-pre-line">
                  {audience === 'staff'
                    ? t('training:report.forTrainee', { comment: item.traineeComment })
                    : item.traineeComment}
                </p>
              )}
              {item.staffNote === undefined || item.staffNote === null ? null : (
                <p className="text-muted-foreground whitespace-pre-line">
                  {t('training:report.forStaff', { note: item.staffNote })}
                </p>
              )}
            </li>
          ))}
        </ul>
      )}

      {report.generalComment === null ? null : (
        <div className="flex flex-col gap-1 text-sm">
          <span className="font-semibold">{t('training:report.generalComment')}</span>
          <p className="whitespace-pre-line">{report.generalComment}</p>
        </div>
      )}
      {report.staffComment === undefined || report.staffComment === null ? null : (
        <div className="flex flex-col gap-1 text-sm">
          <span className="font-semibold">{t('training:report.staffComment')}</span>
          <p className="whitespace-pre-line">{report.staffComment}</p>
        </div>
      )}

      <ReportBoxes training={report} />
    </div>
  );
}

/** How the session went on an item (§1.4): the grade of practice, the mark of theory, or «N/A» when it did not touch it (d4). */
function EvaluationBadge({ item }: { item: TraineeEvaluationDto }) {
  const { t } = useTranslation();
  const says = evaluationSays(item);

  switch (says.kind) {
    case 'grade':
      return <Badge variant="flat" color="blue" text={t('training:report.grade', { grade: says.grade })} />;
    case 'mark':
      return (
        <Badge
          variant="flat"
          color={says.mark === 'Done' ? 'green' : says.mark === 'ToImprove' ? 'orange' : 'red'}
          text={t(`training:marks.${says.mark}`)}
        />
      );
    case 'notApplicable':
      return <Badge variant="flat" color="gray" text={t('training:report.notApplicable')} />;
  }
}

/**
 * The trainer's boxes on a report (§2.7, §2.8), as badges: ready for the mock exam, ready for the exam, and the waiting taken away
 * after it. Nothing when none is ticked.
 */
export function ReportBoxes({
  training,
}: {
  training: {
    readonly readyForMockExam: boolean;
    readonly readyForExam: boolean;
    readonly cooldownWaived: boolean;
  };
}) {
  const { t } = useTranslation();
  const boxes = [
    training.readyForMockExam ? t('training:mine.readyForMockExam') : null,
    training.readyForExam ? t('training:mine.readyForExam') : null,
    training.cooldownWaived ? t('training:mine.cooldownWaived') : null,
  ].filter((box) => box !== null);

  if (boxes.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {boxes.map((box) => (
        <Badge key={box} variant="flat" color="green" text={box} />
      ))}
    </div>
  );
}

/**
 * The sessions of a training that are over (§1.3), the earliest first: when each was, in UTC and where the division lives, and
 * what it came to — held, rescheduled or a no-show. The staff's answer adds the internal notes of a session rescheduled and who
 * recorded it when; the trainee's has neither, and none is drawn (§4.1).
 */
export function SessionList({
  sessions,
  timezone,
}: {
  sessions: readonly {
    readonly id?: number;
    readonly startsAtUtc: string;
    readonly outcome: SessionOutcome;
    readonly internalNotes?: string | null;
    readonly recordedBy?: TrainingMemberDto;
    readonly recordedAt?: string;
  }[];
  timezone: string;
}) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <ul className="flex flex-col gap-3">
      {sessions.map((session, index) => (
        // The staff's sessions carry their id; the trainee's do not, and keep the server's order, the earliest first, which
        // nothing changes while the list is on screen.
        <li
          key={session.id ?? index}
          className="border-border flex flex-col gap-2 rounded-md border p-3 text-sm sm:flex-row sm:items-start sm:justify-between"
        >
          <div className="flex min-w-0 flex-col gap-2">
            <WhenText startsAtUtc={session.startsAtUtc} timezone={timezone} emphasis />
            {session.internalNotes === undefined || session.internalNotes === null ? null : (
              <p className="whitespace-pre-line">
                {t('training:sessions.internalNotes', { notes: session.internalNotes })}
              </p>
            )}
            {session.recordedBy === undefined || session.recordedAt === undefined ? null : (
              <Subtle>
                {t('training:sessions.recordedBy', {
                  name: personName(session.recordedBy, t),
                  date: moment(session.recordedAt, { time: false }),
                })}
              </Subtle>
            )}
          </div>
          <div>
            <Badge
              variant="flat"
              color={OUTCOME_COLOURS[session.outcome]}
              text={t(`training:outcomes.${session.outcome}`)}
            />
          </div>
        </li>
      ))}
    </ul>
  );
}
