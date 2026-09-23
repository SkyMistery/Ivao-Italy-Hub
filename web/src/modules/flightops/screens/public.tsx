import { Badge, Button, H1, H2, H3, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useLocation, useParams } from '@tanstack/react-router';
import { ExternalLink, MessageCircleQuestion, Send } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ContentRenderer, readBody } from '../../../blocks';
import { loginHref } from '../../../shared/api/client';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { resolveLocalized } from '../../../shared/i18n/localized';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import { ConfirmDialog, EmptyState, NotFound, Notice, RouteMap, useNotice } from '../../../shared/ui';
import { bootstrapQuery } from '../../../features/me/queries';
import {
  myTourQuery,
  publicTourQuery,
  publicToursQuery,
  useDisputeReport,
  useReportLegIssue,
  useWithdrawReport,
  type LegProgress,
  type MyTourDto,
  type PirepDto,
  type PublicLegDto,
  type PublicTourDto,
} from '../api';
import {
  disputeSchema,
  legIssueReportSchema,
  type DisputeValues,
  type LegIssueReportValues,
} from '../schemas';

import { isDecided, mapLeg, reportActions } from './reporting';
import { REPORT_STATUS_COLOURS } from './reviewing';
import { TourCards } from './TourCards';

/**
 * The two public screens of the tours (design M2 §8.1): the cards of `/tours`, and one tour at `/tours/{slug}` with
 * its briefing, its dates, its rules, its legs and the map of them.
 *
 * What arrives is what a visitor may see, decided by the server: a hidden tour, a draft, a template and one not yet
 * released all answer 404 here, and a member of staff reading these addresses sees what a visitor sees. The draft is
 * in the back office, where it belongs.
 *
 * A signed in pilot sees their own side on top of it (T11b): the colour of every leg on the map, «send the report», and
 * their reports with «withdraw» and «correct». All of it is `…/reports/mine`, the server's answer: the page decides
 * nothing about the rules. Since T14b they also dispute a rejection within its window, follow the thread it opened, ask
 * for a clarification about a report, a leg or a rule (`/tours/{slug}/ask`), and report a problem on a leg.
 */

export function PublicToursPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const tours = useQuery(publicToursQuery).data;

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-10">
      <header className="flex flex-col gap-2">
        <H1>{t('flightops:public.title')}</H1>
        <Lead>{t('flightops:public.description')}</Lead>
      </header>

      {tours === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : tours.length === 0 ? (
        <EmptyState title={t('flightops:public.none')} />
      ) : (
        <TourCards
          tours={tours.map((tour) => ({
            ...tour,
            title: read(tour.title),
            summary: read(tour.summary),
          }))}
        />
      )}
    </div>
  );
}

export function PublicTourPage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  // A module route is not in the generated tree, so its parameters are not typed: the manifest
  // declares the path and this reads the one segment it has.
  const { slug = '' } = useParams({ strict: false });
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const { data: tour, isPending } = useQuery(publicTourQuery(slug));
  const signedIn = bootstrap?.user !== null && bootstrap?.user !== undefined;
  const mine = useQuery({
    ...myTourQuery(tour?.id ?? 0),
    enabled: signedIn && tour !== undefined && tour !== null,
  }).data;

  if (isPending) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-4xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (tour === undefined || tour === null) {
    return <NotFound />;
  }

  const summary = read(tour.summary);

  return (
    <article className="mx-auto flex w-full max-w-4xl flex-col gap-8 px-4 py-10">
      <PageMetadata
        title={tour.title}
        description={tour.summary}
        imageMediaId={tour.coverMediaId ?? tour.bannerMediaId}
        divisionName={resolveLocalized(
          bootstrap?.division.name,
          i18n.language,
          bootstrap?.division.defaultLocale ?? i18n.language,
        )}
      />

      {tour.bannerMediaId === null ? null : (
        <img
          src={mediaFileUrl(tour.bannerMediaId)}
          alt=""
          className="bg-muted w-full rounded-lg object-cover"
        />
      )}

      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant="flat"
            color={tour.state === 'Upcoming' ? 'gray' : 'blue'}
            text={t(`flightops:tours.options.state.${tour.state}`)}
          />
          <span className="text-muted-foreground text-sm">
            {t(`flightops:tours.options.kind.${tour.kind}`)}
          </span>
          {tour.parent === null ? null : (
            <a href={`/tours/${tour.parent.slug}`} className="text-sm underline">
              {t('flightops:public.partOf', { title: read(tour.parent.title) })}
            </a>
          )}
        </div>

        <H1>{read(tour.title)}</H1>
        {summary === '' ? null : <Lead>{summary}</Lead>}

        <p className="text-muted-foreground text-sm tabular-nums">
          {t('flightops:public.period', {
            from: moment(tour.releaseAt, { time: false }),
            to: moment(tour.closeAt, { time: false }),
          })}
        </p>
      </header>

      <ContentRenderer body={readBody(tour.briefing)} />

      <TourFacts tour={tour} />

      {tour.subtours.length === 0 ? null : (
        <section className="flex flex-col gap-4">
          <H2>{t('flightops:public.subtours')}</H2>
          <TourCards
            tours={tour.subtours.map((subtour) => ({
              ...subtour,
              kind: tour.kind,
              coverMediaId: null,
              title: read(subtour.title),
              summary: read(subtour.summary),
            }))}
          />
        </section>
      )}

      <TourRulesSection tour={tour} signedIn={signedIn} />

      {tour.kind === 'Container' ? null : <PilotSection tour={tour} signedIn={signedIn} mine={mine} />}

      {tour.legs.length === 0 ? null : (
        <section className="flex flex-col gap-4">
          <H2>{t('flightops:public.legs')}</H2>
          <RouteMap
            legs={tour.legs.map((leg) => mapLeg(leg, mine))}
            label={t('flightops:public.mapOf', { title: read(tour.title) })}
          />
          <TourLegs tour={tour} mine={mine} />
        </section>
      )}

      {mine === undefined || mine.reports.length === 0 ? null : (
        <MyReports tour={tour} reports={mine.reports} />
      )}
    </article>
  );
}

/** What the tour asks of a pilot before any rule: aircraft, rating, procedures, the goal of an open tour. */
function TourFacts({ tour }: { tour: PublicTourDto }) {
  const { t } = useTranslation();
  const read = useLocalized();

  const aircraft = [...tour.aircraft.types, ...tour.aircraft.groups.map((group) => read(group.name))].join(
    ' · ',
  );

  const facts: { label: string; value: string }[] = [
    {
      label: t('flightops:public.facts.aircraft'),
      value: aircraft === '' ? t('flightops:public.facts.anyAircraft') : aircraft,
    },
    {
      label: t('flightops:public.facts.reportWindow'),
      value: t('flightops:public.facts.days', { count: tour.reportWindowDays }),
    },
    {
      label: t('flightops:public.facts.progression'),
      value: t(`flightops:tours.options.progression.${tour.progression}`),
    },
  ];

  if (tour.minPilotRating !== null) {
    facts.push({ label: t('flightops:public.facts.minRating'), value: String(tour.minPilotRating) });
  }

  if (tour.requiresProcedures) {
    facts.push({
      label: t('flightops:public.facts.procedures'),
      value: t('flightops:public.facts.required'),
    });
  }

  if (tour.requiredNm !== null) {
    facts.push({
      label: t('flightops:public.facts.requiredNm'),
      value: t('flightops:public.facts.miles', { distance: tour.requiredNm }),
    });
  }

  if (tour.requiredSubtours !== null) {
    facts.push({
      label: t('flightops:public.facts.requiredSubtours'),
      value: String(tour.requiredSubtours),
    });
  }

  if (tour.openGoal !== null) {
    facts.push({
      label: t('flightops:public.facts.goal'),
      value: [t(`flightops:tours.options.openGoal.${tour.openGoal}`), ...tour.openGoalValues].join(' · '),
    });
  }

  return (
    <section className="flex flex-col gap-4">
      <H2>{t('flightops:public.facts.title')}</H2>
      <dl className="grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
        {facts.map((fact) => (
          <div key={fact.label} className="flex flex-col">
            <dt className="text-muted-foreground text-sm">{fact.label}</dt>
            <dd>{fact.value}</dd>
          </div>
        ))}
      </dl>

      {tour.constraints.length === 0 ? null : (
        <div className="flex flex-col gap-1">
          <H3>{t('flightops:constraints.title')}</H3>
          <ul className="flex flex-col gap-1 text-sm">
            {tour.constraints.map((constraint) => (
              <li key={constraint.id} className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold">
                  {t(`flightops:constraints.options.kind.${constraint.kind}`)}
                </span>
                {constraint.values.length === 0 ? null : <span>{constraint.values.join(' · ')}</span>}
              </li>
            ))}
          </ul>
        </div>
      )}

      {tour.callsignRules.length === 0 ? null : (
        <div className="flex flex-col gap-1">
          <H3>{t('flightops:callsigns.title')}</H3>
          <ul className="flex flex-col gap-1 text-sm">
            {tour.callsignRules.map((rule) => (
              <li key={rule.id} className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold">{t(`flightops:callsigns.options.mode.${rule.mode}`)}</span>
                <span>{t(`flightops:callsigns.options.match.${rule.match}`)}</span>
                <span className="font-mono">{rule.value}</span>
                {rule.legNumber === null ? null : (
                  <span className="text-muted-foreground">
                    {t('flightops:public.onLeg', { number: rule.legNumber })}
                  </span>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

/**
 * The rules in force with their parameters, and the errors a pilot can be given (design M2 §5.2, §5.3); a signed in pilot
 * may ask for one to be explained (§3.10).
 */
function TourRulesSection({ tour, signedIn }: { tour: PublicTourDto; signedIn: boolean }) {
  const { t } = useTranslation();
  const read = useLocalized();

  if (tour.rules.length === 0 && tour.errors.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-4">
      <H2>{t('flightops:public.rules')}</H2>

      <ul className="flex flex-col divide-y">
        {tour.rules.map((rule) => {
          const text = read(rule.text);

          return (
            <li key={rule.id} className="flex flex-col gap-1 py-3">
              <div className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold">{rule.code}</span>
                <span>{read(rule.title)}</span>
                {rule.values.length === 0 ? null : (
                  <span className="text-muted-foreground text-sm tabular-nums">
                    {rule.values.join(' · ')}
                  </span>
                )}
                {signedIn ? (
                  <RouterAnchor
                    href={`/tours/${tour.slug}/ask?rule=${rule.id}`}
                    className="text-sm underline"
                  >
                    {t('flightops:public.askAbout')}
                  </RouterAnchor>
                ) : null}
              </div>
              {text === '' ? null : <p className="text-muted-foreground text-sm">{text}</p>}
            </li>
          );
        })}
      </ul>

      {tour.errors.length === 0 ? null : (
        <div className="flex flex-col gap-1">
          <H3>{t('flightops:public.errors')}</H3>
          <ul className="flex flex-col gap-1 text-sm">
            {tour.errors.map((error) => (
              <li key={error.id} className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold">{read(error.name)}</span>
                <span className="text-muted-foreground">
                  {error.category === 'Warning' && error.yearlyMax !== null
                    ? t('flightops:blocks.errorCatalog.warningMax', { count: error.yearlyMax })
                    : t(`flightops:tourErrors.options.category.${error.category}`)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

/** The legs in a table: where, how far, how long, what to call yourself, and a flight plan on SimBrief. */
function TourLegs({ tour, mine }: { tour: PublicTourDto; mine: MyTourDto | undefined }) {
  const { t } = useTranslation();
  const progressOf = (leg: PublicLegDto) => mine?.legs.find((entry) => entry.id === leg.id)?.progress;
  const flyable = (leg: PublicLegDto) =>
    mine !== undefined && mine.blocked === null && mine.flyable.includes(leg.id);

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-muted-foreground border-b text-left">
            <th className="py-2 pr-3 font-medium">{t('flightops:public.legNumber')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:legs.fields.departureIcao')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:legs.fields.arrivalIcao')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:legs.fields.distanceNm')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:legs.fields.estimatedMinutes')}</th>
            <th className="py-2 pr-3 font-medium">{t('flightops:legs.fields.callsigns')}</th>
            {mine === undefined ? null : (
              <th className="py-2 pr-3 font-medium">{t('flightops:public.progress')}</th>
            )}
            <th className="py-2 font-medium">
              <span className="sr-only">{t('flightops:public.simbrief')}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {tour.legs.map((leg) => (
            <tr key={leg.id} className="border-b last:border-0">
              <td className="py-2 pr-3 tabular-nums">{leg.number}</td>
              <td className="py-2 pr-3">
                {leg.departureIcao}
                {leg.departureIata === null ? '' : ` (${leg.departureIata})`}
              </td>
              <td className="py-2 pr-3">
                {leg.arrivalIcao}
                {leg.arrivalIata === null ? '' : ` (${leg.arrivalIata})`}
              </td>
              <td className="py-2 pr-3 tabular-nums">{Math.round(leg.distanceNm)}</td>
              <td className="py-2 pr-3 tabular-nums">
                {leg.estimatedMinutes === null ? '—' : formatMinutes(leg.estimatedMinutes)}
              </td>
              <td className="py-2 pr-3 font-mono">{leg.callsigns.join(' · ')}</td>
              {mine === undefined ? null : (
                <td className="py-2 pr-3">
                  <LegProgressBadge progress={progressOf(leg)} />
                </td>
              )}
              <td className="py-2">
                {flyable(leg) ? (
                  <Button asChild size="sm" variant="ghost">
                    <RouterAnchor href={`/tours/${tour.slug}/report?leg=${leg.id}`}>
                      <Send aria-hidden className="mr-1 size-4" />
                      {t('flightops:public.reportLeg')}
                    </RouterAnchor>
                  </Button>
                ) : null}
                {leg.released ? (
                  <Button asChild size="sm" variant="ghost">
                    <a href={simbriefUrl(leg, tour.referenceAircraftIcao)} target="_blank" rel="noreferrer">
                      <ExternalLink aria-hidden className="mr-1 size-4" />
                      {t('flightops:public.simbrief')}
                    </a>
                  </Button>
                ) : (
                  <span className="text-muted-foreground">{t('flightops:public.notReleased')}</span>
                )}
                {mine === undefined ? null : <ReportLegIssue tour={tour} leg={leg} />}
              </td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr>
            <td className="py-2 pr-3" colSpan={3}>
              {t('flightops:public.total')}
            </td>
            <td className="py-2 pr-3 tabular-nums">{Math.round(tour.totalNm)}</td>
            <td className="py-2 pr-3 tabular-nums">
              {tour.totalEstimatedMinutes === null ? '—' : formatMinutes(tour.totalEstimatedMinutes)}
            </td>
            <td colSpan={mine === undefined ? 2 : 3} />
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

/** Hours and minutes, which is how a pilot reads a flight time. */
function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return `${hours}:${String(rest).padStart(2, '0')}`;
}

/**
 * The flight on SimBrief, with the two airports, the aircraft the tour estimates with, and the real flight number when
 * the leg suggests one — `ITY1234` is the airline `ITY` and the flight `1234`, which is the pair SimBrief's form takes.
 * Nothing else is filled in: the route, the fuel and the day are the pilot's.
 */
function simbriefUrl(leg: PublicLegDto, type: string | null): string {
  const parameters = new URLSearchParams({ orig: leg.departureIcao, dest: leg.arrivalIcao });

  if (type !== null && type !== '') {
    parameters.set('type', type);
  }

  const callsign = leg.callsigns[0] ?? '';
  const parts = /^([A-Z]{2,3})([0-9]{1,4}[A-Z]?)$/.exec(callsign);
  if (parts !== null) {
    parameters.set('airline', parts[1]!);
    parameters.set('fltnum', parts[2]!);
  }

  return `https://dispatch.simbrief.com/options/custom?${parameters.toString()}`;
}

/** A leg's state for the pilot, in the colours of the map. */
function LegProgressBadge({ progress }: { progress: LegProgress | undefined }) {
  const { t } = useTranslation();

  if (progress === undefined) {
    return null;
  }

  return (
    <Badge
      variant="flat"
      color={PROGRESS_COLOURS[progress]}
      text={t(`flightops:public.legProgress.${progress}`)}
    />
  );
}

const PROGRESS_COLOURS: Readonly<Record<LegProgress, 'blue' | 'green' | 'orange' | 'gray'>> = {
  Todo: 'blue',
  Done: 'green',
  Pending: 'orange',
  Locked: 'gray',
};

/**
 * What the pilot can do from here: sign in, when they are not; why they may send nothing, when they may not; otherwise
 * «send the report» for the next leg (or for a new flight on an Open tour), how far the goal of an Open tour is, and
 * whether the tour is done.
 */
function PilotSection({
  tour,
  signedIn,
  mine,
}: {
  tour: PublicTourDto;
  signedIn: boolean;
  mine: MyTourDto | undefined;
}) {
  const { t } = useTranslation();
  const location = useLocation();

  if (!signedIn) {
    return (
      <p className="text-sm">
        <a href={loginHref(location.href)} className="underline">
          {t('flightops:public.signInToReport')}
        </a>
      </p>
    );
  }

  if (mine === undefined) {
    return null;
  }

  const next = tour.kind === 'Open' ? null : (mine.next ?? mine.flyable[0] ?? null);
  const canSend = mine.blocked === null && (tour.kind === 'Open' || next !== null);

  return (
    <section className="flex flex-col gap-3" aria-label={t('flightops:public.pilot')}>
      {mine.finished ? <Notice tone="success" title={t('flightops:public.finished')} /> : null}
      {mine.blocked === null ? null : <Notice tone="warning" title={t(mine.blocked)} />}

      {mine.goal === null ? null : (
        <p className="text-sm tabular-nums">
          {t('flightops:public.goalProgress', { done: mine.goal.done, target: mine.goal.target })}
          {mine.goal.missingMinFlightsAt.length === 0
            ? ''
            : ` · ${t('flightops:public.goalMissing', { airports: mine.goal.missingMinFlightsAt.join(', ') })}`}
        </p>
      )}

      <div className="flex flex-wrap gap-2">
        {canSend ? (
          <Button asChild>
            <RouterAnchor href={`/tours/${tour.slug}/report${next === null ? '' : `?leg=${next}`}`}>
              <Send aria-hidden className="mr-2 size-4" />
              {t('flightops:public.sendReport')}
            </RouterAnchor>
          </Button>
        ) : null}
        <Button asChild variant="outline">
          <RouterAnchor href={`/tours/${tour.slug}/ask`}>
            <MessageCircleQuestion aria-hidden className="mr-2 size-4" />
            {t('flightops:public.ask')}
          </RouterAnchor>
        </Button>
      </div>
    </section>
  );
}

/** The pilot's reports on this tour, newest first, with the two things a pilot can do to one. */
function MyReports({ tour, reports }: { tour: PublicTourDto; reports: readonly PirepDto[] }) {
  const { t } = useTranslation();
  const moment = useMoment();

  return (
    <section className="flex flex-col gap-4">
      <H2>{t('flightops:public.myReports')}</H2>
      <ul className="flex flex-col divide-y">
        {reports.map((report) => {
          const leg = tour.legs.find((entry) => entry.id === report.legId);
          const actions = reportActions(report.status);

          return (
            <li key={report.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
              <div className="flex flex-col gap-1">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge
                    variant="flat"
                    color={REPORT_STATUS_COLOURS[report.status]}
                    text={t(`flightops:public.reportStatus.${report.status}`)}
                  />
                  <span className="font-semibold">
                    {leg === undefined
                      ? `${report.departureIcao} → ${report.arrivalIcao}`
                      : t('flightops:report.leg', {
                          number: leg.number,
                          from: report.departureIcao,
                          to: report.arrivalIcao,
                        })}
                  </span>
                </div>
                <span className="text-muted-foreground text-sm tabular-nums">
                  {[
                    t('flightops:public.reportFlown', { date: moment(report.takeoffAt) }),
                    ...report.flights.map((flight) => flight.callsign),
                    ...(report.isDiversion && report.diversionIcao !== null
                      ? [t('flightops:public.reportDiverted', { airport: report.diversionIcao })]
                      : []),
                  ].join(' · ')}
                </span>
                {report.disputeStatus === null || report.disputeStatus === undefined ? null : (
                  <span className="text-sm">
                    {t(`flightops:public.dispute.${report.disputeStatus}`)}
                    {report.threadId === null || report.threadId === undefined ? null : (
                      <>
                        {' · '}
                        <RouterAnchor href={`/me/contacts/${report.threadId}`} className="underline">
                          {t('flightops:public.disputeThread')}
                        </RouterAnchor>
                      </>
                    )}
                  </span>
                )}
                {report.disputableUntil === null || report.disputableUntil === undefined ? null : (
                  <span className="text-muted-foreground text-sm">
                    {t('flightops:public.disputableUntil', { date: moment(report.disputableUntil) })}
                  </span>
                )}
              </div>

              <div className="flex flex-wrap gap-2">
                {actions.correct ? (
                  <Button asChild size="sm">
                    <RouterAnchor href={`/tours/${tour.slug}/report?report=${report.id}`}>
                      {t('flightops:public.correct')}
                    </RouterAnchor>
                  </Button>
                ) : null}
                {actions.withdraw ? <WithdrawReport report={report} /> : null}
                {report.disputableUntil === null || report.disputableUntil === undefined ? null : (
                  <DisputeReport report={report} />
                )}
                {isDecided(report.status) ? (
                  <Button asChild size="sm" variant="ghost">
                    <RouterAnchor href={`/tours/${tour.slug}/ask?pirep=${report.id}`}>
                      {t('flightops:public.askAbout')}
                    </RouterAnchor>
                  </Button>
                ) : null}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/**
 * Disputing a rejection (§3.8), within its window: what to look at again. The thread it opens is where the answer comes,
 * and the mail says so.
 */
function DisputeReport({ report }: { report: PirepDto }) {
  const { t, i18n } = useTranslation();
  const dispute = useDisputeReport();
  const notice = useNotice();
  const [text, setText] = useState('');

  return (
    <ConfirmDialog
      triggerText={t('flightops:public.disputeAction')}
      triggerVariant="secondary"
      title={t('flightops:public.disputeTitle')}
      description={t('flightops:public.disputeDescription')}
      confirmText={t('flightops:public.disputeAction')}
      confirmVariant="primary"
      disabled={dispute.isPending}
      confirmDisabled={text.trim() === ''}
      onConfirm={() =>
        dispute.mutate(
          { report, text },
          {
            onSuccess: () => {
              setText('');
              notice({ tone: 'success', title: t('flightops:public.disputed') });
            },
            onError: (error) =>
              notice({
                tone: 'error',
                title: describeProblem(error, t, i18n.language) ?? t('errors.unknown'),
              }),
          },
        )
      }
    >
      <SchemaForm<DisputeValues>
        schema={disputeSchema}
        defaults={{ text }}
        locales={[]}
        labels="flightops:public"
        onChange={(values) => setText(values.text)}
      />
    </ConfirmDialog>
  );
}

/** A problem on a leg (§3.11): an airport closed, a route that does not exist. It goes to the tour's department. */
function ReportLegIssue({ tour, leg }: { tour: PublicTourDto; leg: PublicLegDto }) {
  const { t, i18n } = useTranslation();
  const report = useReportLegIssue(tour.id);
  const notice = useNotice();
  const [body, setBody] = useState('');

  return (
    <ConfirmDialog
      triggerText={t('flightops:public.issueAction')}
      triggerVariant="ghost"
      title={t('flightops:public.issueTitle', {
        number: leg.number,
        from: leg.departureIcao,
        to: leg.arrivalIcao,
      })}
      description={t('flightops:public.issueDescription')}
      confirmText={t('flightops:public.issueSend')}
      confirmVariant="primary"
      disabled={report.isPending}
      confirmDisabled={body.trim() === ''}
      onConfirm={() =>
        report.mutate(
          { legId: leg.id, body },
          {
            onSuccess: () => {
              setBody('');
              notice({ tone: 'success', title: t('flightops:public.issueSent') });
            },
            onError: (error) =>
              notice({
                tone: 'error',
                title: describeProblem(error, t, i18n.language) ?? t('errors.unknown'),
              }),
          },
        )
      }
    >
      <SchemaForm<LegIssueReportValues>
        schema={legIssueReportSchema}
        defaults={{ body }}
        locales={[]}
        labels="flightops:public"
        onChange={(values) => setBody(values.body)}
      />
    </ConfirmDialog>
  );
}

function WithdrawReport({ report }: { report: PirepDto }) {
  const { t, i18n } = useTranslation();
  const withdraw = useWithdrawReport();
  const notice = useNotice();

  return (
    <ConfirmDialog
      triggerText={t('flightops:public.withdraw')}
      triggerVariant="secondary"
      title={t('flightops:public.withdrawTitle')}
      description={t('flightops:public.withdrawDescription')}
      confirmText={t('flightops:public.withdraw')}
      confirmVariant="destructive"
      disabled={withdraw.isPending}
      onConfirm={() =>
        withdraw.mutate(report, {
          onSuccess: () => notice({ tone: 'success', title: t('flightops:public.withdrawn') }),
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
