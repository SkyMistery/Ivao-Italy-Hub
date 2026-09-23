import { Badge, Button, H1, H2, H3, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useParams } from '@tanstack/react-router';
import { ExternalLink } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, readBody } from '../../../blocks';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { resolveLocalized } from '../../../shared/i18n/localized';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import { EmptyState, NotFound, RouteMap, type RouteMapLeg } from '../../../shared/ui';
import { bootstrapQuery } from '../../../features/me/queries';
import { publicTourQuery, publicToursQuery, type PublicLegDto, type PublicTourDto } from '../api';

import { TourCards } from './TourCards';

/**
 * The two public screens of the tours (design M2 §8.1): the cards of `/tours`, and one tour at `/tours/{slug}` with
 * its briefing, its dates, its rules, its legs and the map of them.
 *
 * What arrives is what a visitor may see, decided by the server: a hidden tour, a draft, a template and one not yet
 * released all answer 404 here, and a member of staff reading these addresses sees what a visitor sees. The draft is
 * in the back office, where it belongs.
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

      <TourRulesSection tour={tour} />

      {tour.legs.length === 0 ? null : (
        <section className="flex flex-col gap-4">
          <H2>{t('flightops:public.legs')}</H2>
          <RouteMap
            legs={tour.legs.map(mapLeg)}
            label={t('flightops:public.mapOf', { title: read(tour.title) })}
          />
          <TourLegs tour={tour} />
        </section>
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

/** The rules in force with their parameters, and the errors a pilot can be given (design M2 §5.2, §5.3). */
function TourRulesSection({ tour }: { tour: PublicTourDto }) {
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
function TourLegs({ tour }: { tour: PublicTourDto }) {
  const { t } = useTranslation();

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
              <td className="py-2">
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
            <td colSpan={2} />
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

/** A leg as the map draws it: grey while it is not released yet, blue to fly. The pilot's colours arrive with T11. */
function mapLeg(leg: PublicLegDto): RouteMapLeg {
  return {
    id: leg.id,
    from: { code: leg.departureIcao, latitude: leg.departureLatitude, longitude: leg.departureLongitude },
    to: { code: leg.arrivalIcao, latitude: leg.arrivalLatitude, longitude: leg.arrivalLongitude },
    status: leg.released ? 'todo' : 'locked',
  };
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
