import { Badge, Button, H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, Notice, PageShell, RatingBadge, useNotice } from '../../../shared/ui';
import {
  memberLabel,
  staffTrainingQuery,
  staffTrainingsQuery,
  trainerCandidatesQuery,
  useStaffStep,
  type StaffTrainingDto,
  type StaffTrainingRow,
} from '../api';
import {
  RATING_KINDS,
  STAFF_QUEUES,
  assignFromFormValues,
  assignSchema,
  rejectSchema,
  staffTrainingsSearchSchema,
  type AssignValues,
  type RatingKind,
  type RejectValues,
  type StaffQueue,
  type StaffTrainingsSearch,
} from '../schemas';

import { StateBadge, TheoryExamLink } from './parts';
import {
  STAFF_TRAININGS,
  decisionOf,
  isConflict,
  listOrder,
  staffTrainingHref,
  trainerChoices,
} from './trainings';
import { formatHours } from './trainee';

/**
 * The staff's side of the trainings (design M3 §2.3, §2.4, §4.2), over the server of A7: the list of every training, generated,
 * with its views — to approve, to assign, in progress, to close, the history —; and the page of one, like the validation page
 * of the tours: the request with the trainee's rating and hours, the reminder to check the theory exam, the decision, the
 * trainer, and what the reader may do.
 *
 * Everything the page may do is the server's answer (`actions`): a button is drawn when the handler said yes on the row, and a
 * refusal comes back field by field. Who may train a training is the server's list, never worked out here.
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
      actions={<DecisionActions training={training} />}
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

/** Where the training stands, in a line: its state, its ladder and rating, its position, whether it is a mock exam. */
function Standing({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <StateBadge state={training.state} />
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
  const refused = useRefused();
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

/**
 * A step the server refused, said in a notice: it stays while the page is read again after a conflict and its buttons and
 * form are drawn anew.
 */
function useRefused(): (error: unknown) => void {
  const { t, i18n } = useTranslation();
  const notice = useNotice();

  return (error) => {
    const why = describeProblem(error, t, i18n.language);
    notice({
      tone: 'error',
      title: t('training:staff.refused'),
      ...(why === null ? {} : { description: why }),
    });
  };
}

/** Accept and refuse (§2.3), while the request waits and the handler lets the reader decide it. */
function DecisionActions({ training }: { training: StaffTrainingDto }) {
  const { t } = useTranslation();
  const notice = useNotice();
  const failed = useRefused();
  const step = useStaffStep(training.id);
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
              onError: failed,
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
              onError: failed,
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
