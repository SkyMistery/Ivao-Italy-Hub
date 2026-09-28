import { Badge, Button, H2, H3, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { useMemo, useRef, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ApiError } from '../../../shared/api/problem';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, Notice, PageShell, RatingBadge, useNotice } from '../../../shared/ui';
import {
  dateConflicts,
  memberLabel,
  shownState,
  staffTrainingQuery,
  staffTrainingsQuery,
  trainerCandidatesQuery,
  useRereadStaffTraining,
  useStaffStep,
  type DateWarning,
  type StaffSlotDto,
  type StaffTrainingDto,
  type StaffTrainingRow,
} from '../api';
import {
  EMPTY_PROPOSAL,
  RATING_KINDS,
  STAFF_QUEUES,
  assignFromFormValues,
  assignSchema,
  closeSchema,
  dateSchema,
  proposalFromFormValues,
  proposalSchema,
  rejectSchema,
  staffTrainingsSearchSchema,
  type AssignValues,
  type CloseValues,
  type DateValues,
  type ProposalValues,
  type RatingKind,
  type RejectValues,
  type StaffQueue,
  type StaffTrainingsSearch,
} from '../schemas';

import {
  asksConfirmation,
  closingOf,
  dateSteps,
  isHubAddress,
  isRefused,
  isWhole,
  sameDates,
  spanText,
  warningSays,
  whatTheyMeet,
  type Met,
  type WrittenDate,
} from './dates';
import { StateBadge, TheoryExamLink, WhenText } from './parts';
import {
  STAFF_TRAININGS,
  decisionOf,
  isConflict,
  listOrder,
  staffTrainingHref,
  trainerChoices,
} from './trainings';
import { formatHours, splitRefusal } from './trainee';

/**
 * The staff's side of the trainings (design M3 §2.3, §2.4, §2.5, §4.2), over the servers of A7 and A8: the list of every training,
 * generated, with its views — to approve, to assign, in progress, to close, the history —; and the page of one, like the
 * validation page of the tours: the request with the trainee's rating and hours, the reminder to check the theory exam, the
 * decision, the trainer; its dates — the ones proposed with what the hub warned of, the proposal, the date set by hand, the
 * session —, how it was closed, and what the reader may do.
 *
 * Everything the page may do is the server's answer (`actions`): a button is drawn when the handler said yes on the row, and a
 * refusal comes back field by field. Who may train a training, and what a date meets, are the server's answers too, never worked
 * out here.
 */

const columns: readonly ColumnSpec<StaffTrainingRow>[] = [
  col.date('createdAt', { sortable: true }),
  col.text('traineeName'),
  col.text('ratingShortName'),
  col.text('position'),
  col.badge('state', 'training:staff'),
  col.text('trainerName'),
];

export function StaffTrainingsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const search = staffTrainingsSearchSchema.parse(useSearch({ strict: false }));

  const onSearchChange = (patch: Partial<StaffTrainingsSearch>) =>
    void navigate({
      search: ((previous: StaffTrainingsSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  return (
    <PageShell
      title={t('training:staff.title')}
      description={t('training:staff.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:staff.title') }]}
    >
      <DataList
        columns={columns}
        query={staffTrainingsQuery(listOrder(search))}
        labels="training:staff"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        toolbar={
          <>
            <ListFilter
              id="trainings-queue"
              label={t('training:staff.filters.queue')}
              none={t('training:staff.filters.anyQueue')}
              value={search.queue}
              onChange={(queue) => onSearchChange({ queue: queue as StaffQueue | undefined, page: 1 })}
              items={STAFF_QUEUES.map((queue) => ({
                value: queue,
                label: t(`training:staff.queues.${queue}`),
              }))}
            />
            <ListFilter
              id="trainings-kind"
              label={t('training:staff.filters.kind')}
              none={t('training:staff.filters.anyKind')}
              value={search.kind}
              onChange={(kind) => onSearchChange({ kind: kind as RatingKind | undefined, page: 1 })}
              items={RATING_KINDS.map((kind) => ({ value: kind, label: t(`training:kinds.${kind}`) }))}
            />
          </>
        }
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={staffTrainingHref(row.id)}>{t('training:staff.open')}</RouterAnchor>
          </Button>
        )}
      />
    </PageShell>
  );
}

// ---- the page of one training --------------------------------------------------------------------------------------------

export function StaffTrainingPage() {
  const { t } = useTranslation();
  const { id = '' } = useParams({ strict: false });
  const trainingId = Number(id);
  const valid = Number.isInteger(trainingId) && trainingId > 0;
  const training = useQuery({ ...staffTrainingQuery(trainingId), enabled: valid });

  if (training.isPending && valid) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (training.data === undefined) {
    return <NotFound />;
  }

  return <StaffTrainingScreen training={training.data} />;
}

function StaffTrainingScreen({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const trainee = memberLabel(training.trainee);
  const accepted = decisionOf(training).kind === 'accepted';

  return (
    <PageShell
      title={
        training.ratingShortName === null
          ? t('training:staff.pageTitleNoRating', { trainee })
          : t('training:staff.pageTitle', { rating: training.ratingShortName, trainee })
      }
      breadcrumb={[
        { label: t('training:nav.section') },
        { label: t('training:staff.title'), to: STAFF_TRAININGS },
        { label: `#${String(training.id)}` },
      ]}
      actions={
        <>
          <DecisionActions training={training} />
          <CloseTraining training={training} />
        </>
      }
    >
      <div className="flex flex-col gap-8">
        <Standing training={training} />

        {/* The reminder of whoever approves (§2.3, note il-teorico-lo-dichiara-il-trainee): the trainee's word is checked. */}
        {training.state === 'Requested' ? <TheoryReminder training={training} /> : null}

        <Section title={t('training:staff.sections.request')}>
          <RequestDetails training={training} />
        </Section>

        <Section title={t('training:staff.sections.decision')}>
          <DecisionDetails training={training} />
        </Section>

        {accepted ? (
          <Section title={t('training:staff.sections.trainer')}>
            <TrainerDetails training={training} />
          </Section>
        ) : null}

        {/* The dates (§2.5, A8), below the trainer: while the training has one and waits for its date, or has it. */}
        {training.state === 'Assigned' || training.state === 'Scheduled' ? (
          <Section title={t('training:staff.sections.dates')}>
            <Dates training={training} />
          </Section>
        ) : null}

        {closingOf(training) === null ? null : (
          <Section title={t('training:staff.sections.closing')}>
            <ClosingDetails training={training} />
          </Section>
        )}
      </div>
    </PageShell>
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <H2 className="text-lg">{title}</H2>
      {children}
    </section>
  );
}

/**
 * Where the training stands, in a line: its state — held from the day after its session —, its ladder and rating, its position,
 * whether it is a mock exam.
 */
function Standing({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <StateBadge state={shownState(training)} />
      <span className="font-semibold">{t(`training:kinds.${training.kind}`)}</span>
      {training.ratingShortName === null ? null : (
        <RatingBadge kind={training.kind} shortName={training.ratingShortName} />
      )}
      {training.ratingNameKey === null ? null : <span>{t(training.ratingNameKey)}</span>}
      {training.position === null ? null : <span className="font-mono">{training.position}</span>}
      {training.isMockExam ? (
        <Badge variant="flat" color="purple" text={t('training:mine.mockExamBadge')} />
      ) : null}
    </div>
  );
}

/**
 * A moment of a request as a day — the day it was asked, decided, assigned —, as the trainee's pages say it (A6b): an hour
 * would have to say it is UTC's.
 */
function useDay(): (value: string) => string {
  const moment = useMoment();
  return (value) => moment(value, { time: false });
}

/**
 * What a step of the staff that was refused says, in the corner: the training was not changed, and why. One that somebody else
 * overtook (409) reads the page again, so the next press goes with the version there is now.
 */
function useRefused(id: number): (error: unknown) => void {
  const { t, i18n } = useTranslation();
  const notice = useNotice();
  const reread = useRereadStaffTraining(id);

  return (error) => {
    if (error instanceof ApiError && error.status === 409) {
      void reread();
    }

    const why = describeProblem(error, t, i18n.language);
    notice({
      tone: 'error',
      title: t('training:staff.refused'),
      ...(why === null ? {} : { description: why }),
    });
  };
}

/** The reminder while a request waits (§2.3, d1): check on the site of the exam that the trainee's «yes» holds. */
function TheoryReminder({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const day = useDay();

  return (
    <Notice
      tone="warning"
      title={t('training:staff.theoryReminder.title', {
        trainee: memberLabel(training.trainee),
        rating: training.ratingShortName ?? '',
      })}
      description={
        <span className="flex flex-col gap-1">
          {training.theoryConfirmedAt === null ? null : (
            <span>
              {t('training:staff.theoryReminder.declared', { date: day(training.theoryConfirmedAt) })}
            </span>
          )}
          {training.theoryExamUrl === null ? null : <TheoryExamLink url={training.theoryExamUrl} />}
        </span>
      }
    />
  );
}

/** The request as the trainee sent it, with their rating and hours when they asked (§2.3). Never their address. */
function RequestDetails({ training }: { training: StaffTrainingDto }) {
  const { t, i18n } = useTranslation();
  const day = useDay();
  const place = [training.airportIcao, training.fir].filter((part) => part !== null).join(' · ');

  const rows: [string, ReactNode][] = [
    [t('training:staff.request.trainee'), memberLabel(training.trainee)],
    [
      t('training:staff.request.rating'),
      training.traineeRatingShortName === null ? (
        t('training:unknown')
      ) : (
        <RatingBadge kind={training.kind} shortName={training.traineeRatingShortName} />
      ),
    ],
    [
      t('training:staff.request.hours'),
      formatHours(training.traineeHoursAtRequest, i18n.language) ?? t('training:unknown'),
    ],
    [t('training:staff.request.requestedAt'), day(training.requestedAt)],
    ...(training.position === null
      ? []
      : ([
          [
            t('training:staff.request.position'),
            place === '' ? training.position : `${training.position} (${place})`,
          ],
        ] as [string, ReactNode][])),
    [
      t('training:staff.request.availabilityText'),
      training.availabilityText ?? t('training:staff.request.none'),
    ],
    [t('training:staff.request.notesText'), training.notesText ?? t('training:staff.request.none')],
  ];

  return (
    <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-[max-content_1fr]">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className="break-words whitespace-pre-line">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Who decided, when, and why a refusal: the staff's, the hub's own for the theory, or the trainee's cancellation. */
function DecisionDetails({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const day = useDay();
  const decision = decisionOf(training);
  const name = (member: StaffTrainingDto['decidedBy']) => (member === null ? '' : memberLabel(member));

  switch (decision.kind) {
    case 'none':
      return <p className="text-sm">{t('training:staff.decision.none')}</p>;
    case 'accepted':
      return (
        <p className="text-sm">
          {t('training:staff.decision.accepted', { name: name(decision.by), date: day(decision.at) })}
        </p>
      );
    case 'rejected':
      return (
        <div className="flex flex-col gap-1 text-sm">
          <p>{t('training:staff.decision.rejected', { name: name(decision.by), date: day(decision.at) })}</p>
          {decision.reason === null ? null : (
            <p className="whitespace-pre-line">
              {t('training:staff.decision.reason', { reason: decision.reason })}
            </p>
          )}
        </div>
      );
    case 'theory':
      return <p className="text-sm">{t('training:staff.decision.theory', { date: day(decision.at) })}</p>;
    case 'cancelled':
      return <p className="text-sm">{t('training:staff.decision.cancelled', { date: day(decision.at) })}</p>;
  }
}

/** The trainer, if there is one, and — to whoever may assign — the choice of a trainer, or of another. */
function TrainerDetails({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const day = useDay();

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm">
        {training.trainer === null
          ? t('training:staff.trainer.none')
          : t('training:staff.trainer.assigned', {
              name: memberLabel(training.trainer),
              by: training.assignedBy === null ? '' : memberLabel(training.assignedBy),
              date: training.assignedAt === null ? '' : day(training.assignedAt),
            })}
      </p>
      {training.actions.canAssign ? <AssignTrainer training={training} /> : null}
    </div>
  );
}

/**
 * The choice of the trainer (§2.4): among whoever the server says may train it — the staff of the training who signed in, rated
 * at least as high, never the trainee —, the one already assigned left out. The server asks the rule again, and its refusal
 * lands on the field.
 */
function AssignTrainer({ training }: { training: StaffTrainingDto }) {
  const { t, i18n } = useTranslation();
  const notice = useNotice();
  const refused = useRefused(training.id);
  const step = useStaffStep(training.id);
  const candidates = useQuery(trainerCandidatesQuery(training.id));
  const choices = useMemo(() => trainerChoices(candidates.data ?? []), [candidates.data]);
  const schema = useMemo(() => assignSchema(choices), [choices]);

  if (candidates.isPending) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (candidates.data === undefined) {
    return (
      <Notice
        tone="error"
        title={describeProblem(candidates.error, t, i18n.language) ?? t('errors.unknown')}
      />
    );
  }

  // Nobody at all, or nobody but the trainer already assigned: two different things to say.
  if (choices.length === 0) {
    const rating = training.ratingShortName ?? '';
    return (
      <Notice
        tone="info"
        title={
          training.trainer === null
            ? t('training:staff.trainer.noCandidates', { rating })
            : t('training:staff.trainer.noOtherCandidates', { rating })
        }
      />
    );
  }

  return (
    <SchemaForm<AssignValues>
      // Keyed on the version: a training somebody else moved since — read again after a conflict — is drawn again when it
      // arrives, and the next assignment is sent from there.
      key={training.rowVersion}
      schema={schema}
      defaults={{ trainerVid: '', rowVersion: training.rowVersion }}
      locales={[]}
      labels="training:staff.assign"
      onSubmit={async (values) => {
        let page: StaffTrainingDto;
        try {
          page = await step.mutateAsync({ step: 'assign', assignment: assignFromFormValues(values) });
        } catch (error) {
          // The form is about to be drawn anew with the version the server has now: the conflict is said where it stays.
          if (isConflict(error)) {
            refused(error);
            return;
          }

          throw error;
        }

        notice({
          tone: 'success',
          title: t('training:staff.assign.assigned', {
            name: page.trainer === null ? '' : memberLabel(page.trainer),
          }),
        });
      }}
      submitLabel={
        training.trainer === null ? t('training:staff.assign.submit') : t('training:staff.assign.change')
      }
    />
  );
}

/** Accept and refuse (§2.3), while the request waits and the handler lets the reader decide it. */
function DecisionActions({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const notice = useNotice();
  const step = useStaffStep(training.id);
  const refused = useRefused(training.id);
  const [reason, setReason] = useState('');

  if (!training.actions.canDecide) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-2">
      <ConfirmDialog
        triggerText={t('training:staff.accept.button')}
        triggerVariant="secondary"
        title={t('training:staff.accept.title')}
        description={t('training:staff.accept.description')}
        confirmText={t('training:staff.accept.button')}
        confirmVariant="primary"
        disabled={step.isPending}
        onConfirm={() =>
          step.mutate(
            { step: 'accept', rowVersion: training.rowVersion },
            {
              onSuccess: () => notice({ tone: 'success', title: t('training:staff.accept.done') }),
              onError: refused,
            },
          )
        }
      />
      <ConfirmDialog
        triggerText={t('training:staff.reject.button')}
        title={t('training:staff.reject.title')}
        description={t('training:staff.reject.description')}
        confirmText={t('training:staff.reject.button')}
        disabled={step.isPending}
        confirmDisabled={reason.trim() === ''}
        // Written afresh every time: the dialog's field starts empty whenever it opens.
        onOpenChange={(open) => {
          if (open) {
            setReason('');
          }
        }}
        onConfirm={() =>
          step.mutate(
            { step: 'reject', reason, rowVersion: training.rowVersion },
            {
              onSuccess: () => notice({ tone: 'success', title: t('training:staff.reject.done') }),
              onError: refused,
            },
          )
        }
      >
        <SchemaForm<RejectValues>
          schema={rejectSchema}
          defaults={{ reason: '' }}
          locales={[]}
          labels="training:staff.reject"
          onChange={(values) => setReason(values.reason)}
        />
      </ConfirmDialog>
    </div>
  );
}

// ---- the dates (A8) -------------------------------------------------------------------------------------------------------

/**
 * The dates of a training (§2.5): the session once it has one — whose choice its date was, and held from the day after it —; while
 * it waits for one, the dates proposed with what the hub warned of when they were written; and, to whoever conducts it, the
 * proposal of dates and the date set by hand.
 */
function Dates({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const timezone = bootstrap.division.timezone;
  const steps = dateSteps(training);

  return (
    <div className="flex flex-col gap-6">
      {training.state === 'Scheduled' && training.scheduledStartUtc !== null ? (
        <div className="flex flex-col gap-2 text-sm">
          <H3 className="text-base">{t('training:staff.session.title')}</H3>
          <WhenText startsAtUtc={training.scheduledStartUtc} timezone={timezone} emphasis />
          <p>
            {training.dateChosenByTrainee
              ? t('training:staff.session.chosen')
              : t('training:staff.session.byHand')}
          </p>
          {training.held ? <p>{t('training:staff.session.held')}</p> : null}
        </div>
      ) : null}

      {training.state === 'Assigned' ? (
        <div className="flex flex-col gap-2">
          <H3 className="text-base">{t('training:staff.dates.proposedTitle')}</H3>
          <ProposedDates training={training} withdraw={steps.propose} timezone={timezone} />
        </div>
      ) : null}

      {steps.propose ? <ProposeDates training={training} timezone={timezone} /> : null}
      {steps.setByHand ? <SetDate training={training} timezone={timezone} /> : null}
    </div>
  );
}

/**
 * The dates proposed (§2.5), the soonest first: when each is, what the hub found on its days when it was written, who proposed it
 * and when — and, to whoever conducts the training, «take back». One gone by stays until the trainee chooses or the training
 * closes, and the trainee is offered it no more.
 */
function ProposedDates({
  training,
  withdraw,
  timezone,
}: {
  training: StaffTrainingDto;
  withdraw: boolean;
  timezone: string;
}) {
  const { t } = useTranslation();
  const day = useDay();
  // Whether a date has gone by is counted from when the page was drawn.
  const [drawnAt] = useState(() => Date.now());

  if (training.slots.length === 0) {
    return <p className="text-sm">{t('training:staff.dates.none')}</p>;
  }

  return (
    <ul className="flex flex-col gap-3">
      {training.slots.map((slot) => (
        <li
          key={slot.id}
          className="border-border flex flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-start sm:justify-between"
        >
          <div className="flex min-w-0 flex-col gap-2 text-sm">
            <WhenText
              startsAtUtc={slot.startsAtUtc}
              endsAtUtc={slot.endsAtUtc}
              timezone={timezone}
              emphasis
            />
            {Date.parse(slot.startsAtUtc) <= drawnAt ? <p>{t('training:staff.dates.passed')}</p> : null}
            <WarningList warnings={slot.warnings} />
            <Subtle>
              {t('training:staff.dates.proposedBy', {
                name: memberLabel(slot.proposedBy),
                date: day(slot.proposedAt),
              })}
            </Subtle>
          </div>
          {withdraw ? <WithdrawSlot training={training} slot={slot} /> : null}
        </li>
      ))}
    </ul>
  );
}

/** A date taken back before the trainee chose it (§2.5): the trainee no longer sees it, and is not written to. */
function WithdrawSlot({ training, slot }: { training: StaffTrainingDto; slot: StaffSlotDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const notice = useNotice();
  const step = useStaffStep(training.id);
  const refused = useRefused(training.id);

  return (
    <ConfirmDialog
      triggerText={t('training:staff.dates.withdraw.button')}
      triggerVariant="secondary"
      title={t('training:staff.dates.withdraw.title', {
        when: spanText(slot.startsAtUtc, slot.endsAtUtc, moment),
      })}
      description={t('training:staff.dates.withdraw.description')}
      confirmText={t('training:staff.dates.withdraw.button')}
      disabled={step.isPending}
      onConfirm={() =>
        step.mutate(
          { step: 'withdraw', slotId: slot.id, rowVersion: training.rowVersion },
          {
            onSuccess: () => notice({ tone: 'success', title: t('training:staff.dates.withdraw.done') }),
            onError: refused,
          },
        )
      }
    />
  );
}

/**
 * What a date met when it was looked at (§2.5), each on its line: another training by what the public calendar shows of it, with
 * the staff's page of it; an entry of the calendar by its kind and title, with where it is read. Nobody's name.
 */
function WarningList({ warnings }: { warnings: readonly DateWarning[] }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const read = useLocalized();
  const moment = useMoment();

  if (warnings.length === 0) {
    return <p className="text-muted-foreground">{t('training:staff.dates.noWarnings')}</p>;
  }

  const calendarKind = (key: string | null) =>
    key === null ? '' : read(bootstrap.calendarKinds.find((kind) => kind.key === key)?.label) || key;

  return (
    <ul className="flex list-disc flex-col gap-1 pl-5">
      {warnings.map((warning, index) => {
        const says = warningSays(warning, read);
        const text =
          says.about === 'training'
            ? t('training:staff.dates.warning.training', {
                ladder: says.ladder === null ? '' : t(`training:kinds.${says.ladder}`),
                what: says.what,
                when: spanText(says.startsAtUtc, null, moment),
              })
            : t('training:staff.dates.warning.calendar', {
                kind: calendarKind(says.calendarKind),
                what: says.what,
                when: spanText(says.startsAtUtc, says.endsAtUtc, moment),
              });

        return (
          // A warning has no identifier of its own: the list is drawn in the server's order, which nothing changes.
          <li key={index}>
            {says.href === null ? (
              text
            ) : isHubAddress(says.href) ? (
              <RouterAnchor href={says.href} className="underline">
                {text}
              </RouterAnchor>
            ) : (
              <a href={says.href} target="_blank" rel="noopener noreferrer" className="underline">
                {text}
              </a>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * What the dates on screen meet (§2.5), before they are written: under `Warn`, what was found and the confirmation of those very
 * dates; under `Block`, what was found, beside the refusal the server says on each date's field. Nothing when nothing was found.
 */
function MetNotice({
  met,
  confirmText,
  onConfirm,
  disabled,
}: {
  met: Met;
  confirmText: string;
  onConfirm: () => void;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const moment = useMoment();
  const asks = asksConfirmation(met);

  if (!asks && !isRefused(met)) {
    return null;
  }

  return (
    <Notice
      tone={asks ? 'warning' : 'error'}
      title={asks ? t('training:staff.dates.met.confirmTitle') : t('training:staff.dates.met.refusedTitle')}
      description={
        <div className="flex flex-col gap-3">
          {met.dates.map((date, index) => {
            const warnings = met.warnings[index] ?? [];

            return date.startsAtUtc === null || warnings.length === 0 ? null : (
              // The dates of the form, in its order: nothing reorders them while the notice is on screen.
              <div key={index} className="flex flex-col gap-1">
                <span className="font-semibold tabular-nums">
                  {t('training:time.utc', {
                    when: spanText(date.startsAtUtc, date.endsAtUtc ?? null, moment),
                  })}
                </span>
                <WarningList warnings={warnings} />
              </div>
            );
          })}
          {asks ? (
            <div>
              <Button type="button" variant="secondary" disabled={disabled} onClick={onConfirm}>
                {confirmText}
              </Button>
            </div>
          ) : null}
        </div>
      }
    />
  );
}

/**
 * How a date is written on this page (§2.5), the same for the dates proposed and for the date set by hand: what the dates meet is
 * asked before they are written. Under `Warn`, with something found, the page shows it and waits for the confirmation — a second
 * press, of the same dates —, which sends them confirmed; otherwise they go as they are, and the server has the last word: under
 * `Block` it refuses the dates it found something on, each on its field, and the page shows what it found. A box left empty is
 * not asked about: the server says what is missing. A refusal on a field of the form lands there, one about the dates as a whole
 * above the form, and a step somebody else overtook (409) reads the page again, keeping what was written.
 */
function useDatesWriter<TValues extends Record<string, unknown>>({
  training,
  formId,
  written,
  send,
  isFormField,
}: {
  training: StaffTrainingDto;
  formId: string;
  written: (values: TValues) => readonly WrittenDate[];
  send: (values: TValues, confirmed: boolean) => Promise<void>;
  isFormField: (field: string) => boolean;
}) {
  const reread = useRereadStaffTraining(training.id);
  const [met, setMet] = useState<Met | null>(null);
  const [problem, setProblem] = useState<ApiError | null>(null);
  const confirming = useRef(false);

  const submit = async (values: TValues) => {
    const dates = written(values);
    const confirmed =
      confirming.current && met !== null && asksConfirmation(met) && sameDates(met.dates, dates);
    confirming.current = false;
    setProblem(null);

    if (!confirmed) {
      const found = dates.every(isWhole)
        ? await whatTheyMeet(dates, (start, end) => dateConflicts(training.id, start, end))
        : null;
      setMet(found);

      if (found !== null && asksConfirmation(found)) {
        return;
      }
    }

    try {
      await send(values, confirmed);
      setMet(null);
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        void reread();
      }

      const { form, page } = splitRefusal(error, isFormField);
      setProblem(page);
      if (form !== null) {
        throw form;
      }
    }
  };

  // The confirmation sends the form again, from the notice: the dates are the form's, and so are the refusals on them.
  const confirm = () => {
    confirming.current = true;
    const form = document.getElementById(formId);
    if (form instanceof HTMLFormElement) {
      form.requestSubmit();
    }
  };

  return { submit, met, problem, confirm };
}

/** The fields of a date proposed, where a refusal of one of them lands: `slots[0].startsAtUtc`, as the server names them. */
const SLOT_FIELD = /^slots\[\d+\]\.(startsAtUtc|endsAtUtc)$/;

/**
 * The trainer's dates (§2.5, R.3), proposed together so that the trainee is written to once: as many as the list holds, each
 * with its start and end in UTC — the division's own time under each —, and what they meet asked first. A proposal that went
 * starts the form again, empty.
 */
function ProposeDates({ training, timezone }: { training: StaffTrainingDto; timezone: string }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const notice = useNotice();
  const step = useStaffStep(training.id);
  const [round, setRound] = useState(0);
  const formId = `training-${String(training.id)}-dates`;

  const { submit, met, problem, confirm } = useDatesWriter<ProposalValues>({
    training,
    formId,
    written: proposalFromFormValues,
    send: async (values, confirmed) => {
      const slots = proposalFromFormValues(values);
      await step.mutateAsync({
        step: 'propose',
        proposal: { slots, confirmed, rowVersion: training.rowVersion },
      });
      setRound((current) => current + 1);
      notice({ tone: 'success', title: t('training:staff.dates.propose.done', { count: slots.length }) });
    },
    isFormField: (field) => SLOT_FIELD.test(field),
  });

  return (
    <div className="flex flex-col gap-3">
      <H3 className="text-base">{t('training:staff.dates.propose.title')}</H3>
      <p className="text-muted-foreground text-sm">
        {t('training:staff.dates.propose.lead', { zone: timezone })}
      </p>
      {problem === null ? null : (
        <Notice tone="error" title={describeProblem(problem, t, i18n.language) ?? t('errors.unknown')} />
      )}
      <SchemaForm<ProposalValues>
        // A new proposal after one that went: the form starts again. A refusal keeps what was written.
        key={round}
        id={formId}
        schema={proposalSchema}
        defaults={EMPTY_PROPOSAL}
        locales={[]}
        labels="training:staff.dates.propose"
        division={bootstrap.division}
        onSubmit={submit}
        submitLabel={t('training:staff.dates.propose.submit')}
      />
      {met === null ? null : (
        <MetNotice
          met={met}
          confirmText={t('training:staff.dates.propose.confirm')}
          onConfirm={confirm}
          disabled={step.isPending}
        />
      )}
    </div>
  );
}

/**
 * The date set by hand (§2.5, d2), by whoever conducts the training: when the session starts, among the dates proposed or not —
 * gone by too, for a session held earlier than planned —, with what it meets asked first. The dates proposed go, and the trainee
 * and the trainer are written to.
 */
function SetDate({ training, timezone }: { training: StaffTrainingDto; timezone: string }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const moment = useMoment();
  const notice = useNotice();
  const step = useStaffStep(training.id);
  const [round, setRound] = useState(0);
  const formId = `training-${String(training.id)}-date`;

  const { submit, met, problem, confirm } = useDatesWriter<DateValues>({
    training,
    formId,
    written: (values) => [{ startsAtUtc: values.startsAtUtc ?? null }],
    send: async (values, confirmed) => {
      const page = await step.mutateAsync({
        step: 'date',
        date: { startsAtUtc: values.startsAtUtc ?? null, confirmed, rowVersion: training.rowVersion },
      });
      setRound((current) => current + 1);
      notice({
        tone: 'success',
        title: t('training:staff.dates.setByHand.done', {
          when: moment(page.scheduledStartUtc ?? values.startsAtUtc),
        }),
      });
    },
    isFormField: (field) => field === 'startsAtUtc',
  });

  return (
    <div className="flex flex-col gap-3">
      <H3 className="text-base">{t('training:staff.dates.setByHand.title')}</H3>
      <p className="text-muted-foreground text-sm">
        {t('training:staff.dates.setByHand.lead', { zone: timezone })}
      </p>
      {problem === null ? null : (
        <Notice tone="error" title={describeProblem(problem, t, i18n.language) ?? t('errors.unknown')} />
      )}
      <SchemaForm<DateValues>
        key={round}
        id={formId}
        schema={dateSchema}
        defaults={{}}
        locales={[]}
        labels="training:staff.dates.setByHand"
        division={bootstrap.division}
        onSubmit={submit}
        submitLabel={t('training:staff.dates.setByHand.submit')}
      />
      {met === null ? null : (
        <MetNotice
          met={met}
          confirmText={t('training:staff.dates.setByHand.confirm')}
          onConfirm={confirm}
          disabled={step.isPending}
        />
      )}
    </div>
  );
}

// ---- the closing (A8) -----------------------------------------------------------------------------------------------------

/**
 * Closing a training (§2.5, R.3), with a reason the trainee reads — when they never chose a date, or for anything else —, while it
 * is accepted and goes on and the handler lets the reader approve it. The session leaves the calendar, and the dates proposed go.
 */
function CloseTraining({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const notice = useNotice();
  const step = useStaffStep(training.id);
  const refused = useRefused(training.id);
  const [reason, setReason] = useState('');

  if (!training.actions.canClose) {
    return null;
  }

  return (
    <ConfirmDialog
      triggerText={t('training:staff.close.button')}
      triggerVariant="secondary"
      title={t('training:staff.close.title')}
      description={t('training:staff.close.description')}
      confirmText={t('training:staff.close.button')}
      disabled={step.isPending}
      confirmDisabled={reason.trim() === ''}
      // Written afresh every time: the dialog's field starts empty whenever it opens.
      onOpenChange={(open) => {
        if (open) {
          setReason('');
        }
      }}
      onConfirm={() =>
        step.mutate(
          { step: 'close', reason, rowVersion: training.rowVersion },
          {
            onSuccess: () => notice({ tone: 'success', title: t('training:staff.close.done') }),
            onError: refused,
          },
        )
      }
    >
      <SchemaForm<CloseValues>
        schema={closeSchema}
        defaults={{ reason: '' }}
        locales={[]}
        labels="training:staff.close"
        onChange={(values) => setReason(values.reason)}
      />
    </ConfirmDialog>
  );
}

/** Who closed the training, when, and why: the staff with its reason, or the hub, because the trainee chose no date in time. */
function ClosingDetails({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const day = useDay();
  const closing = closingOf(training);

  if (closing === null) {
    return null;
  }

  return closing.by === 'hub' ? (
    <p className="text-sm">{t('training:staff.closing.byHub', { date: day(closing.at) })}</p>
  ) : (
    <div className="flex flex-col gap-1 text-sm">
      <p>
        {t('training:staff.closing.byStaff', {
          name: training.closedBy === null ? '' : memberLabel(training.closedBy),
          date: day(closing.at),
        })}
      </p>
      <p className="whitespace-pre-line">{t('training:staff.decision.reason', { reason: closing.reason })}</p>
    </div>
  );
}
