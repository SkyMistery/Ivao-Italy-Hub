import {
  AccordionContent,
  AccordionItem,
  AccordionRoot,
  AccordionTrigger,
  Badge,
  Button,
  H1,
  H2,
  H3,
  Lead,
  Tabs,
} from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useLocation, useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { Clock, ExternalLink, LogIn, MessageCircleQuestion, Pencil, Send } from 'lucide-react';
import { useId, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ContentRenderer, readBody } from '../../../blocks';
import { loginHref } from '../../../shared/api/client';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { resolveLocalized } from '../../../shared/i18n/localized';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import { ConfirmDialog, EmptyState, NotFound, Notice, RouteMap, useNotice } from '../../../shared/ui';
import { bootstrapQuery } from '../../../features/me/queries';
import {
  myToursQuery,
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
  type StartedTourDto,
} from '../api';
import {
  disputeSchema,
  legIssueReportSchema,
  tourPageSearchSchema,
  type DisputeValues,
  type LegIssueReportValues,
  type TourPageTab,
} from '../schemas';

import { ProgressLine } from './ProgressLine';
import { isDecided, mapLeg, reportActions } from './reporting';
import { REPORT_STATUS_COLOURS } from './reviewing';
import { TourCards } from './TourCards';
import { TourHead } from './TourFace';
import { formatMinutes, legGroups, nextLeg, openTab, pageTabs, ruleGroups, simbriefUrl } from './tourPage';

/**
 * The two public screens of the tours: the cards of `/tours` (design M2 §8.1), and one tour at `/tours/{slug}` as the
 * design of phase 6 draws it (`11-design-pagine-dei-tour` §4, approved by Carmine on 31 October 2026): the head, «Your
 * tour» right under it, and four tabs — the briefing, the route, the rules, the pilot's reports — with the open one in the
 * address (`?tab=`).
 *
 * What arrives is what a visitor may see, decided by the server: a hidden tour, a draft, a template and one not yet
 * released all answer 404 here, and a member of staff reading these addresses sees what a visitor sees. The draft is
 * in the back office, where it belongs.
 *
 * A signed in pilot sees their own side on top of it (T11b): the colour of every leg, «send the report» for the next one,
 * why they may send nothing when they may not, and their reports with «withdraw» and «correct». All of it is
 * `…/reports/mine` and `/my-tours`, the server's answers: the page decides nothing about the rules. Since T14b they also
 * dispute a rejection within its window, follow the thread it opened, ask for a clarification about a report, a leg or a
 * rule (`/tours/{slug}/ask`), and report a problem on a leg.
 */

export function PublicToursPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const tours = useQuery(publicToursQuery).data;

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6 px-4 py-10">
      <PageMetadata title={t('flightops:public.title')} description={t('flightops:public.description')} />

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
  const navigate = useNavigate();
  // A module route is not in the generated tree, so its parameters are not typed: the manifest
  // declares the path and this reads the one segment it has.
  const { slug = '' } = useParams({ strict: false });
  const search = tourPageSearchSchema.parse(useSearch({ strict: false }));
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const { data: tour, isPending } = useQuery(publicTourQuery(slug));
  const signedIn = bootstrap?.user !== null && bootstrap?.user !== undefined;
  const mineQuery = useQuery({
    ...myTourQuery(tour?.id ?? 0),
    enabled: signedIn && tour !== undefined && tour !== null,
  });
  // The pilot's progress, the one the cards draw (`/my-tours`): the bar of «Your tour» and of each subtour.
  const started = useQuery({ ...myToursQuery, enabled: signedIn }).data?.tours;

  if (isPending) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-5xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (tour === undefined || tour === null) {
    return <NotFound />;
  }

  const mine = mineQuery.data;
  // Still on its way is not «none»: the tab of the reports waits for the answer rather than vanishing under the address.
  const answer = !signedIn ? null : mineQuery.isPending ? undefined : (mine ?? null);
  const tabs = pageTabs(tour, signedIn, answer);
  const tab = openTab(search.tab, tabs);

  const onTab = (next: string) =>
    void navigate({
      // The first tab is the page's own address: a link to the tour stays `/tours/{slug}`.
      search: ((previous: Record<string, unknown>) => ({
        ...previous,
        tab: next === 'overview' ? undefined : next,
      })) as never,
      to: '.',
    });

  const contents: Record<TourPageTab, { trigger: string; content: ReactNode }> = {
    overview: {
      trigger: t('flightops:public.tabs.overview'),
      content: <Overview tour={tour} />,
    },
    route: {
      trigger: t('flightops:public.tabs.route'),
      content: <RouteTab tour={tour} mine={mine} />,
    },
    rules: {
      trigger: t('flightops:public.tabs.rules'),
      content: (
        <div className="flex flex-col gap-10">
          <TourFacts tour={tour} />
          <TourRulesSection tour={tour} signedIn={signedIn} />
        </div>
      ),
    },
    reports: {
      trigger: t('flightops:public.myReports'),
      content:
        mine === undefined ? (
          <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
        ) : (
          <MyReports tour={tour} reports={mine.reports} />
        ),
    },
  };

  return (
    <article className="mx-auto flex w-full max-w-5xl flex-col gap-8 px-4 py-10">
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

      <TourHead tour={tour} />

      <YourTour tour={tour} signedIn={signedIn} mine={mine} started={started} />

      <Tabs
        // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]` (HANDOFF §13).
        className="w-full"
        value={tab}
        onValueChange={onTab}
        tabs={Object.fromEntries(
          tabs.map((key) => [
            key,
            { trigger: contents[key].trigger, content: <div className="pt-6">{contents[key].content}</div> },
          ]),
        )}
      />
    </article>
  );
}

/** The first tab: the briefing, as the department wrote it. */
function Overview({ tour }: { tour: PublicTourDto }) {
  const { t } = useTranslation();
  const body = readBody(tour.briefing);

  return body.sections.length === 0 ? (
    <p className="text-muted-foreground text-sm">{t('flightops:public.noBriefing')}</p>
  ) : (
    <ContentRenderer body={body} />
  );
}

/**
 * «Your tour» (design 11 §4), right under the head: what the pilot flies now and the button to fly it (principle 1). The
 * bar, the next leg with «send the report» and SimBrief, and — when they may send nothing — why, which is the first refusal
 * `…/reports/mine` gives (a ban, a report to correct, a rating). A container has no leg of its own: its subtours, each with
 * the pilot's progress. A visitor is asked to sign in.
 */
function YourTour({
  tour,
  signedIn,
  mine,
  started,
}: {
  tour: PublicTourDto;
  signedIn: boolean;
  mine: MyTourDto | undefined;
  started: readonly StartedTourDto[] | undefined;
}) {
  const { t } = useTranslation();
  const location = useLocation();
  const heading = useId();

  if (!signedIn) {
    return (
      <section className="bg-card text-card-foreground border-border flex flex-wrap items-center justify-between gap-3 rounded-lg border p-5">
        <p className="text-muted-foreground">{t('flightops:public.signInLead')}</p>
        <Button asChild>
          <a href={loginHref(location.href)}>
            <LogIn aria-hidden className="mr-2 size-4" />
            {t('flightops:public.signInToReport')}
          </a>
        </Button>
      </section>
    );
  }

  const own = started?.find((row) => row.tourId === tour.id);

  return (
    <section
      aria-labelledby={heading}
      className="bg-card text-card-foreground border-border flex flex-col gap-4 rounded-lg border p-5"
    >
      <H2 id={heading}>{t('flightops:public.pilot')}</H2>

      {mine === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : (
        <>
          {mine.finished ? <Notice tone="success" title={t('flightops:public.finished')} /> : null}
          {own === undefined || mine.finished ? null : <ProgressLine tour={{ ...own, next: null }} />}
          {mine.blocked === null ? null : <Notice tone="warning" title={t(mine.blocked)} />}

          {mine.goal === null ? null : (
            <p className="text-sm tabular-nums">
              {t('flightops:public.goalProgress', { done: mine.goal.done, target: mine.goal.target })}
              {mine.goal.missingMinFlightsAt.length === 0
                ? ''
                : ` · ${t('flightops:public.goalMissing', { airports: mine.goal.missingMinFlightsAt.join(', ') })}`}
            </p>
          )}

          {tour.kind === 'Container' ? <Subtours tour={tour} started={started} /> : null}
          <PilotActions tour={tour} mine={mine} />
        </>
      )}
    </section>
  );
}

/** The subtours of a container in «Your tour»: each with the pilot's progress, or not started yet. */
function Subtours({
  tour,
  started,
}: {
  tour: PublicTourDto;
  started: readonly StartedTourDto[] | undefined;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  if (tour.subtours.length === 0) {
    return null;
  }

  return (
    <ul className="flex flex-col divide-y">
      {tour.subtours.map((subtour) => {
        const progress = started?.find((row) => row.tourId === subtour.id);

        return (
          <li key={subtour.id} className="flex flex-col gap-1 py-3">
            <RouterAnchor href={`/tours/${subtour.slug}`} className="font-semibold underline">
              {read(subtour.title)}
            </RouterAnchor>
            {progress === undefined ? (
              <span className="text-muted-foreground text-sm">{t('flightops:public.notStarted')}</span>
            ) : (
              <ProgressLine tour={progress} />
            )}
          </li>
        );
      })}
    </ul>
  );
}

/**
 * What the pilot can do from «Your tour»: the next leg with «send the report» and SimBrief; on an `Open` tour, a report of
 * a flight of their choice; the report «to modify» to correct, when that is what holds them; and a question to the staff.
 */
function PilotActions({ tour, mine }: { tour: PublicTourDto; mine: MyTourDto }) {
  const { t } = useTranslation();
  const leg = nextLeg(tour, mine);
  const free = mine.blocked === null;
  const toCorrect = mine.reports.find((report) => reportActions(report.status).correct);

  return (
    <div className="flex flex-col gap-4">
      {leg === null || !free ? null : <NextLeg tour={tour} leg={leg} />}

      <div className="flex flex-wrap gap-2">
        {tour.kind === 'Open' && free ? (
          <Button asChild>
            <RouterAnchor href={`/tours/${tour.slug}/report`}>
              <Send aria-hidden className="mr-2 size-4" />
              {t('flightops:public.sendReport')}
            </RouterAnchor>
          </Button>
        ) : null}
        {toCorrect === undefined ? null : (
          <Button asChild>
            <RouterAnchor href={`/tours/${tour.slug}/report?report=${toCorrect.id}`}>
              <Pencil aria-hidden className="mr-2 size-4" />
              {t('flightops:public.correctReport')}
            </RouterAnchor>
          </Button>
        )}
        <Button asChild variant="outline">
          <RouterAnchor href={`/tours/${tour.slug}/ask`}>
            <MessageCircleQuestion aria-hidden className="mr-2 size-4" />
            {t('flightops:public.ask')}
          </RouterAnchor>
        </Button>
      </div>
    </div>
  );
}

/** The next leg, said as a pilot reads it, with the two things to do before and after flying it. */
function NextLeg({ tour, leg }: { tour: PublicTourDto; leg: PublicLegDto }) {
  const { t } = useTranslation();
  const facts = [
    t('flightops:public.facts.miles', { distance: Math.round(leg.distanceNm) }),
    ...(leg.estimatedMinutes === null ? [] : [formatMinutes(leg.estimatedMinutes)]),
    ...leg.callsigns,
  ];

  return (
    <div className="border-border flex flex-col gap-3 rounded-md border p-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="flex flex-col gap-1">
        <span className="text-muted-foreground text-sm">{t('flightops:public.nextLeg')}</span>
        <span className="text-lg font-semibold">
          {t('flightops:report.leg', { number: leg.number, from: leg.departureIcao, to: leg.arrivalIcao })}
        </span>
        <span className="text-muted-foreground text-sm tabular-nums">{facts.join(' · ')}</span>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button asChild>
          <RouterAnchor href={`/tours/${tour.slug}/report?leg=${leg.id}`}>
            <Send aria-hidden className="mr-2 size-4" />
            {t('flightops:public.sendReport')}
          </RouterAnchor>
        </Button>
        {leg.released ? (
          <Button asChild variant="outline">
            <a href={simbriefUrl(leg, tour.referenceAircraftIcao)} target="_blank" rel="noreferrer">
              <ExternalLink aria-hidden className="mr-2 size-4" />
              {t('flightops:public.simbrief')}
            </a>
          </Button>
        ) : null}
      </div>
    </div>
  );
}

/**
 * The second tab (design 11 §4, tab 2): the map and the legs, with the pilot's state leg by leg; a hub tour grouped by hub
 * and rotation; a container's subtours as cards.
 */
function RouteTab({ tour, mine }: { tour: PublicTourDto; mine: MyTourDto | undefined }) {
  const { t } = useTranslation();
  const read = useLocalized();

  return (
    <div className="flex flex-col gap-6">
      {tour.legs.length === 0 ? null : (
        <>
          <RouteMap
            legs={tour.legs.map((leg) => mapLeg(leg, mine))}
            label={t('flightops:public.mapOf', { title: read(tour.title) })}
          />
          <TourLegs tour={tour} mine={mine} />
        </>
      )}

      {tour.kind !== 'Container' || tour.subtours.length === 0 ? null : (
        <TourCards
          tours={tour.subtours.map((subtour) => ({
            ...subtour,
            kind: tour.kind,
            coverMediaId: null,
            title: read(subtour.title),
            summary: read(subtour.summary),
          }))}
        />
      )}
    </div>
  );
}

/**
 * The legs: where, how far, how long, what to call yourself, the pilot's state, a report and a flight plan on SimBrief. A
 * table on a wide screen; on a phone each row is a block of its own, the same row with the same cells — one markup, not a
 * second list to keep in step.
 */
function TourLegs({ tour, mine }: { tour: PublicTourDto; mine: MyTourDto | undefined }) {
  const { t } = useTranslation();
  const progressOf = (leg: PublicLegDto) => mine?.legs.find((entry) => entry.id === leg.id)?.progress;
  const flyable = (leg: PublicLegDto) =>
    mine !== undefined && mine.blocked === null && mine.flyable.includes(leg.id);
  const columns = mine === undefined ? 6 : 7;

  return (
    <table className="w-full text-sm">
      <thead className="max-sm:hidden">
        <tr className="text-muted-foreground border-b text-left">
          <th className="py-2 pr-3 font-medium">{t('flightops:public.legNumber')}</th>
          <th className="py-2 pr-3 font-medium">{t('flightops:public.route')}</th>
          <th className="py-2 pr-3 font-medium">{t('flightops:public.figures.distance')}</th>
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
      {legGroups(tour).map((group) => (
        <tbody key={group.key}>
          {group.titled ? (
            <tr className="max-sm:flex">
              <th colSpan={columns} scope="colgroup" className="pt-5 pb-2 text-left font-semibold">
                {group.rotation === null
                  ? t('flightops:public.betweenHubs')
                  : t('flightops:public.rotation', {
                      hub: group.rotation.hub,
                      number: group.rotation.number,
                    })}
              </th>
            </tr>
          ) : null}
          {group.legs.map((leg) => (
            <tr
              key={leg.id}
              className="border-b last:border-0 max-sm:flex max-sm:flex-wrap max-sm:items-center max-sm:gap-x-3 max-sm:gap-y-1 max-sm:py-3"
            >
              <td className="py-2 pr-3 tabular-nums max-sm:p-0 max-sm:font-semibold">{leg.number}</td>
              <td className="py-2 pr-3 max-sm:p-0">
                {leg.departureIcao}
                {leg.departureIata === null ? '' : ` (${leg.departureIata})`}
                {' → '}
                {leg.arrivalIcao}
                {leg.arrivalIata === null ? '' : ` (${leg.arrivalIata})`}
              </td>
              <td className="py-2 pr-3 tabular-nums max-sm:p-0">
                {t('flightops:public.facts.miles', { distance: Math.round(leg.distanceNm) })}
              </td>
              <td className="py-2 pr-3 tabular-nums max-sm:p-0">
                <Clock aria-hidden className="text-muted-foreground mr-1 inline size-3.5 sm:hidden" />
                {leg.estimatedMinutes === null ? '—' : formatMinutes(leg.estimatedMinutes)}
              </td>
              <td className="py-2 pr-3 font-mono max-sm:p-0">{leg.callsigns.join(' · ')}</td>
              {mine === undefined ? null : (
                <td className="py-2 pr-3 max-sm:p-0">
                  <LegProgressBadge progress={progressOf(leg)} />
                </td>
              )}
              <td className="py-2 max-sm:flex max-sm:w-full max-sm:flex-wrap max-sm:gap-1 max-sm:p-0">
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
      ))}
    </table>
  );
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

  // The rating by its name, from the core's vocabulary: the number is IVAO's and says nothing to a pilot.
  if (tour.minPilotRatingName !== null) {
    facts.push({
      label: t('flightops:public.facts.minRating'),
      value: `${tour.minPilotRatingName} — ${t(`ratings.Pilot.${tour.minPilotRatingName}`)}`,
    });
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
 * The rules in force with their parameters (design M2 §5.2), in two groups — the division's and the tour's own —, each
 * closed under its code and title and opened one at a time (design 11 §4, tab 3); the errors a pilot can be given at the
 * bottom (§5.3). A signed in pilot may ask for a rule to be explained (§3.10), from the rule itself.
 */
function TourRulesSection({ tour, signedIn }: { tour: PublicTourDto; signedIn: boolean }) {
  const { t } = useTranslation();
  const read = useLocalized();

  if (tour.rules.length === 0 && tour.errors.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-6">
      <H2>{t('flightops:public.rules')}</H2>

      {ruleGroups(tour).map((group) => (
        <div key={group.key} className="flex flex-col gap-2">
          <H3>{t(`flightops:public.ruleGroups.${group.key}`)}</H3>
          <AccordionRoot type="multiple" className="w-full">
            {group.rules.map((rule) => {
              const text = read(rule.text);

              return (
                <AccordionItem key={rule.id} value={String(rule.id)}>
                  <AccordionTrigger>
                    <span className="flex flex-wrap items-baseline gap-x-2 text-left">
                      <span className="font-semibold">{rule.code}</span>
                      <span>{read(rule.title)}</span>
                      {rule.values.length === 0 ? null : (
                        <span className="text-muted-foreground text-sm font-normal tabular-nums">
                          {rule.values.join(' · ')}
                        </span>
                      )}
                    </span>
                  </AccordionTrigger>
                  <AccordionContent>
                    <div className="flex flex-col gap-2">
                      {rule.amendsCode === null || rule.amendsCode === undefined ? null : (
                        <p className="text-muted-foreground text-sm">
                          {t('flightops:public.amends', { code: rule.amendsCode })}
                        </p>
                      )}
                      {text === '' ? null : <p className="text-muted-foreground text-sm">{text}</p>}
                      {signedIn ? (
                        <RouterAnchor
                          href={`/tours/${tour.slug}/ask?rule=${rule.id}`}
                          className="text-sm underline"
                        >
                          {t('flightops:public.askAbout')}
                        </RouterAnchor>
                      ) : null}
                    </div>
                  </AccordionContent>
                </AccordionItem>
              );
            })}
          </AccordionRoot>
        </div>
      ))}

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
