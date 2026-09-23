import { Badge, Button, Checkbox, H2, Label } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { preferenceQuery, useSavePreference } from '../../../features/me/queries';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, Notice, PageShell, RouteMap, useNotice } from '../../../shared/ui';
import {
  reviewQuery,
  reviewQueueQuery,
  reviewSuggestionQuery,
  reviewTracksQuery,
  toursListQuery,
  memberName,
  useReviewStep,
  type ReviewDto,
  type ReviewErrorDto,
  type ReviewFlightDto,
  type ReviewQueueRow,
} from '../api';
import {
  decisionSchema,
  reopenSchema,
  reviewQueueSearchSchema,
  type DecisionValues,
  type ReopenValues,
  type ReviewQueueSearch,
} from '../schemas';

import { TOURS } from './tours';
import { useStaff } from './hooks';
import {
  REPORT_STATUS_COLOURS,
  REVIEW_QUEUE_ORDERS,
  REVIEW_QUEUE_ORDER_PREFERENCE,
  eventNote,
  hhmm,
  planAtTakeoff,
  queueOrderOf,
  queueSortOf,
  reviewMapLegs,
  reviewMapTracks,
  type ReviewQueueOrder,
} from './reviewing';

/**
 * The validation (design M2 §4, §8.5), the pages of T13b over the server of T13a: the queue, one list for every tour or
 * narrowed to one, ordered by date or by tour as the validator's own preference; and the page of one report, with the map
 * and the track, every revision of the plan, the table of the errors of the rules it froze with the pilot's counts, the
 * suggestion the server works out as errors are ticked, the decision, and the reopening of one taken.
 *
 * Everything this page may do is the server's answer (`actions`): a button is drawn when the handler said yes, and a
 * refusal comes back field by field.
 */

export const REVIEW = '/staff/tours/review';

const queueColumns: readonly ColumnSpec<ReviewQueueRow>[] = [
  col.localized('tourTitle'),
  col.number('legNumber'),
  col.text('route'),
  col.text('pilotName'),
  col.date('takeoffAt'),
  col.date('queuedAt'),
  col.badge('status', 'flightops:review'),
  col.text('assignedToName'),
];

// ---- the queue --------------------------------------------------------------------------------------

export function ReviewQueuePage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const search = reviewQueueSearchSchema.parse(useSearch({ strict: false }));

  const preference = useQuery(preferenceQuery(REVIEW_QUEUE_ORDER_PREFERENCE));
  const saveOrder = useSavePreference(REVIEW_QUEUE_ORDER_PREFERENCE);
  const order = queueOrderOf(saveOrder.isPending ? saveOrder.variables : preference.data);

  // The tours the filter offers. A validator enabled on one tour by a grant may not hold `Tours.View`: then there is
  // no filter, and the queue — which they may read — is still the whole of it.
  const tours = useQuery({
    ...toursListQuery({ page: 1, pageSize: 100, dir: 'asc' }, { isTemplate: false }),
    retry: false,
  });

  const onSearchChange = (patch: Partial<ReviewQueueSearch>) =>
    void navigate({
      search: ((previous: ReviewQueueSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  const filters = {
    open: search.decided !== true,
    ...(search.tour === undefined ? {} : { tourId: search.tour }),
  };

  return (
    <PageShell
      title={t('flightops:review.title')}
      description={t('flightops:review.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:review.title') }]}
    >
      {preference.isPending ? null : (
        <DataList
          columns={queueColumns}
          query={reviewQueueQuery({ ...search, sort: queueSortOf(order), dir: 'asc' }, filters)}
          labels="flightops:review"
          locale={i18n.language}
          defaultLocale={bootstrap.division.defaultLocale}
          timezone={bootstrap.division.timezone}
          search={search}
          onSearchChange={onSearchChange}
          toolbar={
            <div className="flex flex-wrap items-end gap-3">
              {tours.data === undefined ? null : (
                <ListFilter
                  id="review-tour"
                  label={t('flightops:review.filters.tour')}
                  none={t('flightops:review.filters.allTours')}
                  value={search.tour === undefined ? undefined : String(search.tour)}
                  onChange={(value) =>
                    onSearchChange({ tour: value === undefined ? undefined : Number(value), page: 1 })
                  }
                  items={tours.data.items.map((tour) => ({
                    value: String(tour.id),
                    label: read(tour.title),
                  }))}
                />
              )}
              <ListFilter
                id="review-shown"
                label={t('flightops:review.filters.shown')}
                none={t('flightops:review.filters.waiting')}
                value={search.decided === true ? 'decided' : undefined}
                onChange={(value) =>
                  onSearchChange({ decided: value === 'decided' ? true : undefined, page: 1 })
                }
                items={[{ value: 'decided', label: t('flightops:review.filters.decided') }]}
              />
              <ListFilter
                id="review-order"
                label={t('flightops:review.filters.order')}
                none={t('flightops:review.order.date')}
                value={order === 'date' ? undefined : order}
                onChange={(value) => {
                  const chosen: ReviewQueueOrder = value === 'tour' ? 'tour' : 'date';
                  saveOrder.mutate(chosen);
                  onSearchChange({ page: 1 });
                }}
                items={REVIEW_QUEUE_ORDERS.filter((entry) => entry !== 'date').map((entry) => ({
                  value: entry,
                  label: t(`flightops:review.order.${entry}`),
                }))}
              />
            </div>
          }
          actions={(row) => (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${REVIEW}/${row.id}`}>
                {row.isOwn ? t('flightops:review.own') : t('flightops:review.open')}
              </RouterAnchor>
            </Button>
          )}
        />
      )}
    </PageShell>
  );
}

// ---- the page of one report --------------------------------------------------------------------------

export function ReviewPage() {
  const { t } = useTranslation();
  const { id = '' } = useParams({ strict: false });
  const reportId = Number(id);
  const review = useQuery({ ...reviewQuery(reportId), enabled: Number.isInteger(reportId) });

  if (review.isPending && Number.isInteger(reportId)) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (review.data === undefined) {
    return <NotFound />;
  }

  return <ReviewScreen review={review.data} />;
}

function ReviewScreen({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const title = read(review.tourTitle);

  return (
    <PageShell
      title={t('flightops:review.pageTitle', {
        tour: title,
        from: review.leg.departureIcao,
        to: review.leg.arrivalIcao,
      })}
      breadcrumb={[
        { label: t('flightops:nav.section'), to: TOURS },
        { label: t('flightops:review.title'), to: REVIEW },
        { label: `#${review.id}` },
      ]}
      actions={<ReviewActions review={review} />}
    >
      <div className="flex flex-col gap-8">
        <ReviewStanding review={review} />

        <Section title={t('flightops:review.sections.flight')}>
          <p className="text-sm">
            {review.leg.number === null
              ? t('flightops:review.route', { from: review.leg.departureIcao, to: review.leg.arrivalIcao })
              : t('flightops:report.leg', {
                  number: review.leg.number,
                  from: review.leg.departureIcao,
                  to: review.leg.arrivalIcao,
                })}
            {' · '}
            {t('flightops:review.pilotLine', { pilot: memberName(review.pilot) })}
            {' · '}
            {t('flightops:review.sentOn', { date: moment(review.submittedAt) })}
          </p>
          <ReviewMap review={review} />
          {review.flights.map((flight) => (
            <FlightPlans key={flight.seq} flight={flight} />
          ))}
        </Section>

        <Section title={t('flightops:review.sections.declared')}>
          <Declared review={review} />
        </Section>

        <Section title={t('flightops:review.sections.pilot')}>
          <PilotProfile review={review} />
        </Section>

        <Section title={t('flightops:review.sections.weather')}>
          <p className="text-muted-foreground text-sm">{t('flightops:review.weatherLater')}</p>
        </Section>

        <Section title={t('flightops:review.sections.checks')}>
          <p className="text-muted-foreground text-sm">{t('flightops:review.checksLater')}</p>
        </Section>

        <Decision key={review.rowVersion} review={review} />

        <Section title={t('flightops:review.sections.history')}>
          <History review={review} />
        </Section>
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

/** Where the report stands, in a line: its status, who holds it and until when, or that it is the reader's own. */
function ReviewStanding({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <Badge
          variant="flat"
          color={REPORT_STATUS_COLOURS[review.status]}
          text={t(`flightops:review.options.status.${review.status}`)}
        />
        {review.status === 'InReview' && review.assignedTo !== null ? (
          <span>
            {t('flightops:review.heldBy', {
              name: memberName(review.assignedTo),
              until: moment(review.leaseUntil, { date: false }),
            })}
          </span>
        ) : null}
        {review.decidedBy !== null && review.decidedAt !== null ? (
          <span>
            {t('flightops:review.decidedBy', {
              name: memberName(review.decidedBy),
              date: moment(review.decidedAt),
            })}
          </span>
        ) : null}
      </div>
      {review.isOwn ? <Notice tone="info" title={t('flightops:review.ownNotice')} /> : null}
    </div>
  );
}

/** Take, let go, reopen: each drawn only when the server said the reader may. */
function ReviewActions({ review }: { review: ReviewDto }) {
  const { t, i18n } = useTranslation();
  const notice = useNotice();
  const step = useReviewStep(review.id);
  const [reason, setReason] = useState('');

  const failed = (error: unknown) => {
    const reason = describeProblem(error, t, i18n.language);
    notice({
      tone: 'error',
      title: t('flightops:review.refused'),
      ...(reason === null ? {} : { description: reason }),
    });
  };

  return (
    <div className="flex flex-wrap gap-2">
      {review.actions.canTake ? (
        <Button
          disabled={step.isPending}
          onClick={() =>
            step.mutate(
              { step: 'take', rowVersion: review.rowVersion },
              {
                onSuccess: () => notice({ tone: 'success', title: t('flightops:review.taken') }),
                onError: failed,
              },
            )
          }
        >
          {t('flightops:review.take')}
        </Button>
      ) : null}
      {review.actions.canRelease ? (
        <Button
          variant="outline"
          disabled={step.isPending}
          onClick={() => step.mutate({ step: 'release', rowVersion: review.rowVersion }, { onError: failed })}
        >
          {t('flightops:review.release')}
        </Button>
      ) : null}
      {review.actions.canReopen ? (
        <ConfirmDialog
          triggerText={t('flightops:review.reopen')}
          triggerVariant="secondary"
          title={t('flightops:review.reopenTitle')}
          description={t('flightops:review.reopenDescription')}
          confirmText={t('flightops:review.reopen')}
          confirmVariant="primary"
          disabled={step.isPending}
          confirmDisabled={reason.trim() === ''}
          onConfirm={() =>
            step.mutate(
              { step: 'reopen', reason, rowVersion: review.rowVersion },
              {
                onSuccess: () => {
                  setReason('');
                  notice({ tone: 'success', title: t('flightops:review.reopened') });
                },
                onError: failed,
              },
            )
          }
        >
          <SchemaForm<ReopenValues>
            schema={reopenSchema}
            defaults={{ reason }}
            locales={[]}
            labels="flightops:review"
            onChange={(values) => setReason(values.reason)}
          />
        </ConfirmDialog>
      ) : null}
    </div>
  );
}

/** The leg as it was frozen and, over it, the tracks the server still keeps (90 days after the decision). */
function ReviewMap({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const anyTrack = review.flights.some((flight) => flight.hasTrack);
  const tracks = useQuery({ ...reviewTracksQuery(review.id), enabled: anyTrack });
  const legs = useMemo(() => reviewMapLegs(review), [review]);
  const drawn = useMemo(() => reviewMapTracks(tracks.data), [tracks.data]);

  return (
    <div className="flex flex-col gap-2">
      <RouteMap
        legs={legs}
        tracks={drawn}
        label={t('flightops:review.mapOf', { from: review.leg.departureIcao, to: review.leg.arrivalIcao })}
      />
      {anyTrack ? null : <p className="text-muted-foreground text-sm">{t('flightops:review.noTrack')}</p>}
    </div>
  );
}

/** One flight and every revision of its plan, the one at take-off first and marked (§4.3). */
function FlightPlans({ flight }: { flight: ReviewFlightDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const atTakeoff = planAtTakeoff(flight);
  const plans = [
    ...(atTakeoff === null ? [] : [atTakeoff]),
    ...flight.flightPlans.filter((plan) => plan !== atTakeoff).reverse(),
  ];

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm font-semibold">
        {t('flightops:review.flightLine', {
          callsign: flight.callsign,
          from: flight.departureIcao,
          to: flight.arrivalIcao,
          takeoff: moment(flight.takeoffAt),
          landing: flight.landingAt === null ? '—' : moment(flight.landingAt, { date: false }),
        })}
        {flight.aircraft === null ? null : ` · ${flight.aircraft}`}
      </p>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-muted-foreground border-b text-left">
              {[
                'revision',
                'filedAt',
                'flightRules',
                'aircraft',
                'equipment',
                'level',
                'speed',
                'route',
                'alternate',
                'times',
                'remarks',
              ].map((column) => (
                <th key={column} className="py-2 pr-3 font-medium">
                  {t(`flightops:review.plan.${column}`)}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {plans.map((plan) => (
              <tr
                key={plan.revision}
                className={`border-b align-top last:border-0 ${plan === atTakeoff ? 'bg-muted/60 font-medium' : ''}`}
              >
                <td className="py-2 pr-3 whitespace-nowrap tabular-nums">
                  {plan.revision}
                  {plan === atTakeoff ? (
                    <span className="ml-2">
                      <Badge variant="flat" color="blue" text={t('flightops:review.plan.atTakeoff')} />
                    </span>
                  ) : null}
                </td>
                <td className="py-2 pr-3 whitespace-nowrap">{moment(plan.filedAt)}</td>
                <td className="py-2 pr-3">
                  {plan.flightRules}
                  {plan.flightType === null ? '' : ` / ${plan.flightType}`}
                </td>
                <td className="py-2 pr-3 font-mono">
                  {plan.aircraftIcao ?? '—'}
                  {plan.wakeTurbulence === null ? '' : `/${plan.wakeTurbulence}`}
                </td>
                <td className="py-2 pr-3 font-mono">
                  {plan.equipment}/{plan.transponder}
                </td>
                <td className="py-2 pr-3 font-mono">{plan.level ?? '—'}</td>
                <td className="py-2 pr-3 font-mono">{plan.speed ?? '—'}</td>
                <td className="py-2 pr-3 font-mono break-words">{plan.route ?? '—'}</td>
                <td className="py-2 pr-3 font-mono">
                  {[plan.alternateIcao, plan.secondAlternateIcao].filter(Boolean).join(' ') || '—'}
                </td>
                <td className="py-2 pr-3 font-mono whitespace-nowrap">
                  {hhmm(plan.departureTimeMinutes)} · {hhmm(plan.enrouteMinutes)}
                </td>
                <td className="py-2 font-mono break-words">{plan.remarks ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** What the pilot declared: procedures, controllers, exemptions, the diversion, their note. */
function Declared({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const facts: [string, ReactNode][] = [
    [t('flightops:review.fields.flightRules'), review.flightRules],
    [t('flightops:report.fields.sid'), review.sid ?? '—'],
    [t('flightops:report.fields.star'), review.star ?? '—'],
    [t('flightops:report.fields.approach'), review.approach ?? '—'],
  ];

  if (review.isDiversion) {
    facts.push([
      t('flightops:report.fields.diversionIcao'),
      [
        review.diversionIcao,
        review.diversionReason === null
          ? null
          : t(`flightops:report.options.diversionReason.${review.diversionReason}`),
        review.diversionNote,
      ]
        .filter(Boolean)
        .join(' · '),
    ]);
  }

  facts.push([t('flightops:report.fields.pilotRemarks'), review.pilotRemarks ?? '—']);

  return (
    <div className="flex flex-col gap-4 text-sm">
      <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1">
        {facts.map(([label, value]) => (
          <div key={label} className="contents">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="break-words whitespace-pre-line">{value}</dd>
          </div>
        ))}
      </dl>

      <div className="flex flex-col gap-1">
        <p className="font-semibold">{t('flightops:report.atc.title')}</p>
        {review.atcArchiveAvailable ? null : (
          <p className="text-muted-foreground">{t('flightops:review.atcUnavailable')}</p>
        )}
        {review.atcContacts.length === 0 ? (
          <p className="text-muted-foreground">{t('flightops:review.noAtc')}</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {review.atcContacts.map((contact) => (
              <li key={`${contact.callsign}-${contact.origin}`} className="flex flex-wrap items-center gap-2">
                <span className={`font-mono ${contact.origin === 'Removed' ? 'line-through' : ''}`}>
                  {contact.callsign}
                </span>
                {contact.frequency === null ? null : (
                  <span className="text-muted-foreground">{contact.frequency}</span>
                )}
                <Badge
                  variant="flat"
                  color={
                    contact.origin === 'Added' ? 'orange' : contact.origin === 'Removed' ? 'gray' : 'blue'
                  }
                  text={t(`flightops:review.options.origin.${contact.origin}`)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>

      {review.exemptions.length === 0 ? null : (
        <div className="flex flex-col gap-1">
          <p className="font-semibold">{t('flightops:report.atc.fields.exemptions')}</p>
          <ul className="flex flex-col gap-1">
            {review.exemptions.map((exemption, index) => (
              <li key={index} className="flex flex-wrap items-center gap-2">
                <span className="font-mono">{exemption.callsign}</span>
                <span>{t(`flightops:report.atc.options.exemptions.kind.${exemption.kind}`)}</span>
                <Badge
                  variant="flat"
                  color={
                    exemption.status === 'Online'
                      ? 'green'
                      : exemption.status === 'NotOnline'
                        ? 'red'
                        : 'gray'
                  }
                  text={t(`flightops:review.options.exemptionStatus.${exemption.status}`)}
                />
                {exemption.note === null ? null : (
                  <span className="text-muted-foreground">{exemption.note}</span>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

/** The pilot in this tour: legs reported, accepted, rejected, disputed, and every ban. */
function PilotProfile({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const { profile } = review;

  return (
    <div className="flex flex-col gap-2 text-sm">
      <p>
        {t('flightops:review.profile', {
          reported: profile.reported,
          accepted: profile.accepted,
          rejected: profile.rejected,
          disputed: profile.disputed,
        })}
      </p>
      {profile.bans.length === 0 ? (
        <p className="text-muted-foreground">{t('flightops:review.noBans')}</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {profile.bans.map((ban, index) => (
            <li key={index}>
              {t(ban.inForce ? 'flightops:review.banInForce' : 'flightops:review.banPast', {
                from: moment(ban.startsAt, { time: false }),
                to: ban.endsAt === null ? '—' : moment(ban.endsAt, { time: false }),
                reason: ban.reason,
              })}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * The errors of the rules the report froze, the suggestion and the decision (§4.3). While the reader holds the report the
 * errors are ticked here and the server says what it proposes for them; otherwise the table shows the decision as it
 * stands, read only.
 */
function Decision({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const notice = useNotice();
  const step = useReviewStep(review.id);
  const editable = review.actions.canDecide;
  const [marked, setMarked] = useState<number[]>(() =>
    review.errors.filter((error) => error.marked).map((error) => error.id),
  );

  const asked = useQuery({ ...reviewSuggestionQuery(review.id, marked), enabled: editable });
  const suggestion = editable ? (asked.data ?? review.suggestion) : review.suggestion;

  const toggle = (error: ReviewErrorDto, on: boolean) =>
    setMarked((current) => (on ? [...current, error.id] : current.filter((entry) => entry !== error.id)));

  const decide = async (values: DecisionValues) => {
    await step.mutateAsync({
      step: 'decide',
      decision: {
        outcome: values.outcome,
        errorIds: marked,
        noteToPilot: values.noteToPilot === '' ? null : values.noteToPilot,
        staffNote: values.staffNote === '' ? null : values.staffNote,
        overrideReason: values.overrideReason === '' ? null : values.overrideReason,
        rowVersion: review.rowVersion,
      },
    });
    notice({ tone: 'success', title: t('flightops:review.decided') });
  };

  return (
    <>
      <Section title={t('flightops:review.sections.errors')}>
        <ErrorTable errors={review.errors} marked={marked} editable={editable} onToggle={toggle} />
      </Section>

      <Section title={t('flightops:review.sections.suggestion')}>
        <Suggestion errors={review.errors} outcome={suggestion.outcome} reasons={suggestion.reasons} />
      </Section>

      <Section title={t('flightops:review.sections.decision')}>
        {editable ? (
          <SchemaForm<DecisionValues>
            schema={decisionSchema}
            defaults={{
              outcome: suggestion.outcome === 'Rejected' ? 'Rejected' : 'Accepted',
              noteToPilot: review.noteToPilot ?? '',
              staffNote: review.staffNote ?? '',
              overrideReason: review.overrideReason ?? '',
            }}
            locales={[]}
            labels="flightops:review"
            onSubmit={decide}
            submitLabel={t('flightops:review.decide')}
          />
        ) : (
          <DecisionAsItStands review={review} />
        )}
      </Section>
    </>
  );
}

function ErrorTable({
  errors,
  marked,
  editable,
  onToggle,
}: {
  errors: readonly ReviewErrorDto[];
  marked: readonly number[];
  editable: boolean;
  onToggle: (error: ReviewErrorDto, on: boolean) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  if (errors.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('flightops:review.noErrors')}</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-muted-foreground border-b text-left">
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.marked')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.name')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.category')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.rules')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.inYear')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:review.errors.ever')}</th>
            <th className="py-2 font-medium">{t('flightops:review.errors.yearlyMax')}</th>
          </tr>
        </thead>
        <tbody>
          {errors.map((error) => {
            const id = `review-error-${error.id}`;
            const on = marked.includes(error.id);

            return (
              <tr key={error.id} className="border-b last:border-0">
                <td className="py-2 pr-3">
                  <Checkbox
                    id={id}
                    checked={on}
                    disabled={!editable}
                    onCheckedChange={(checked) => onToggle(error, checked === true)}
                  />
                </td>
                <td className="py-2 pr-3">
                  <Label htmlFor={id}>{read(error.name)}</Label>
                  {error.suggestedByCheck ? (
                    <span className="text-muted-foreground ml-2 text-xs">
                      {t('flightops:review.errors.byCheck')}
                    </span>
                  ) : null}
                </td>
                <td className="py-2 pr-3">
                  <Badge
                    variant="flat"
                    color={
                      error.category === 'Dangerous'
                        ? 'red'
                        : error.category === 'Warning'
                          ? 'orange'
                          : 'gray'
                    }
                    text={t(`flightops:tourErrors.options.category.${error.category}`)}
                  />
                </td>
                <td className="py-2 pr-3 font-mono">{error.ruleCodes.join(' · ')}</td>
                <td className="py-2 pr-3 tabular-nums">{error.countInYear}</td>
                <td className="py-2 pr-3 tabular-nums">{error.countEver}</td>
                <td className="py-2 tabular-nums">{error.yearlyMax ?? '—'}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** What the system proposes, and the errors that make it a rejection. */
function Suggestion({
  errors,
  outcome,
  reasons,
}: {
  errors: readonly ReviewErrorDto[];
  outcome: ReviewDto['suggestion']['outcome'];
  reasons: ReviewDto['suggestion']['reasons'];
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  return (
    <div className="flex flex-col gap-2 text-sm" data-testid="review-suggestion">
      <div className="flex items-center gap-2">
        <Badge
          variant="flat"
          color={REPORT_STATUS_COLOURS[outcome]}
          text={t(`flightops:review.options.status.${outcome}`)}
        />
      </div>
      {reasons.length === 0 ? (
        <p className="text-muted-foreground">{t('flightops:review.suggestionAccept')}</p>
      ) : (
        <ul className="list-disc pl-5">
          {reasons.map((reason) => {
            const error = errors.find((entry) => entry.id === reason.errorId);
            return (
              <li key={`${reason.errorId}-${reason.reason}`}>
                {t(`flightops:review.reasons.${reason.reason}`, {
                  error: error === undefined ? `#${reason.errorId}` : read(error.name),
                  count: reason.countInYear ?? 0,
                  max: reason.yearlyMax ?? 0,
                })}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

function DecisionAsItStands({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();

  if (review.decidedAt === null) {
    return <p className="text-muted-foreground text-sm">{t('flightops:review.notDecided')}</p>;
  }

  const facts: [string, string][] = [
    [t('flightops:review.fields.noteToPilot'), review.noteToPilot ?? '—'],
    [t('flightops:review.fields.staffNote'), review.staffNote ?? '—'],
  ];
  if (review.thresholdOverridden) {
    facts.push([t('flightops:review.fields.overrideReason'), review.overrideReason ?? '—']);
  }

  return (
    <dl className="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
      {facts.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-muted-foreground">{label}</dt>
          <dd className="whitespace-pre-line">{value}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Every step of the report, with who took it: the staff read the names, the pilot never does (§3.5). */
function History({ review }: { review: ReviewDto }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <ol className="flex flex-col gap-1 text-sm">
      {review.history.map((step, index) => {
        const note = eventNote(step.note, t);
        return (
          <li key={index} className="flex flex-wrap gap-2">
            <span className="text-muted-foreground tabular-nums">{moment(step.at)}</span>
            <span className="font-medium">{t(`flightops:review.options.status.${step.toStatus}`)}</span>
            {step.by === null ? null : <span>{memberName(step.by)}</span>}
            {note === null ? null : <span className="text-muted-foreground">— {note}</span>}
          </li>
        );
      })}
    </ol>
  );
}
