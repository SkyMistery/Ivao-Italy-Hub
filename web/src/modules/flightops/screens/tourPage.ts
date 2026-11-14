import type { TFunction } from 'i18next';
import { Award, Clock, Layers, Plane, Route, Ruler, Target, UserCheck, type LucideIcon } from 'lucide-react';

import type { LocalizedString } from '../../../shared/api/bootstrap';
import type { MyTourDto, PublicLegDto, PublicTourDto } from '../api';
import { TOUR_PAGE_TABS, type TourPageTab } from '../schemas';

/**
 * What the page of a tour puts where (design 11 §4, phase 6), which is little: the server answers every question of the
 * rules and every number (`PublicTours`, `TourRules`), and these only sort its answers into the head, the pilot's panel and
 * the four tabs. Pure, so a test reads them without a browser.
 */

/** The rotations of a hub tour, as the page groups the legs by them. */
export type PublicRotationDto = PublicTourDto['rotations'][number];

/** One number of the tour: what it is, its value, a word more when it needs one. */
export interface TourFigure {
  readonly key: string;
  readonly label: string;
  readonly value: string;
  readonly hint?: string;
  readonly Icon: LucideIcon;
}

/** At most this many aircraft by name in the head; the rest are counted, and the rules tab lists them all. */
const AIRCRAFT_SHOWN = 2;

/**
 * The tour's identity card in numbers (design 11 §4, the head): legs, miles, estimated time, minimum rating, the aircraft it
 * admits, the award — and, by kind, the distance a `Distance` tour asks, the subtours of a container, the goal of an `Open`
 * tour. A number the tour has not got is left out rather than drawn as a dash. Every one is the server's: here they are
 * only said.
 */
export function tourFigures(
  tour: PublicTourDto,
  t: TFunction,
  read: (value: LocalizedString | null | undefined) => string,
): TourFigure[] {
  const figures: TourFigure[] = [];

  if (tour.legs.length > 0) {
    figures.push({
      key: 'legs',
      label: t('flightops:public.figures.legs'),
      value: String(tour.legs.length),
      Icon: Route,
    });
  }

  if (tour.totalNm > 0) {
    figures.push({
      key: 'distance',
      label: t('flightops:public.figures.distance'),
      value: t('flightops:public.facts.miles', { distance: Math.round(tour.totalNm) }),
      Icon: Ruler,
    });
  }

  if (tour.totalEstimatedMinutes !== null) {
    figures.push({
      key: 'time',
      label: t('flightops:public.figures.time'),
      value: formatMinutes(tour.totalEstimatedMinutes),
      ...(tour.referenceAircraftIcao === null || tour.referenceAircraftIcao === ''
        ? {}
        : { hint: t('flightops:public.figures.timeWith', { type: tour.referenceAircraftIcao }) }),
      Icon: Clock,
    });
  }

  if (tour.requiredNm !== null) {
    figures.push({
      key: 'requiredNm',
      label: t('flightops:public.facts.requiredNm'),
      value: t('flightops:public.facts.miles', { distance: tour.requiredNm }),
      Icon: Ruler,
    });
  }

  if (tour.kind === 'Container') {
    figures.push({
      key: 'subtours',
      label: t('flightops:public.subtours'),
      value: String(tour.subtours.length),
      ...(tour.requiredSubtours === null
        ? {}
        : { hint: t('flightops:public.figures.toComplete', { count: tour.requiredSubtours }) }),
      Icon: Layers,
    });
  }

  if (tour.openGoal !== null) {
    figures.push({
      key: 'goal',
      label: t('flightops:public.facts.goal'),
      value: t(`flightops:tours.options.openGoal.${tour.openGoal}`),
      ...(tour.openGoalValues.length === 0 ? {} : { hint: tour.openGoalValues.join(' · ') }),
      Icon: Target,
    });
  }

  // The rating by its short name, as a pilot says it, and its full name under it: the number is IVAO's and says nothing.
  if (tour.minPilotRatingName !== null) {
    figures.push({
      key: 'rating',
      label: t('flightops:public.facts.minRating'),
      value: tour.minPilotRatingName,
      hint: t(`ratings.Pilot.${tour.minPilotRatingName}`, { defaultValue: tour.minPilotRatingName }),
      Icon: UserCheck,
    });
  }

  // A container admits what each of its subtours admits: its own head says nothing about aircraft.
  if (tour.kind !== 'Container') {
    const aircraft = [...tour.aircraft.types, ...tour.aircraft.groups.map((group) => read(group.name))];
    const more = aircraft.length - AIRCRAFT_SHOWN;

    figures.push({
      key: 'aircraft',
      label: t('flightops:public.facts.aircraft'),
      value:
        aircraft.length === 0
          ? t('flightops:public.facts.anyAircraft')
          : aircraft.slice(0, AIRCRAFT_SHOWN).join(' · '),
      ...(more > 0 ? { hint: t('flightops:public.figures.moreAircraft', { count: more }) } : {}),
      Icon: Plane,
    });
  }

  if (tour.award !== null) {
    figures.push({
      key: 'award',
      label: t('flightops:public.figures.award'),
      value: read(tour.award.name),
      Icon: Award,
    });
  }

  return figures;
}

/** Hours and minutes, which is how a pilot reads a flight time: `1:05`. */
export function formatMinutes(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;

  return `${hours}:${String(rest).padStart(2, '0')}`;
}

/**
 * The flight on SimBrief, with the two airports, the aircraft the tour estimates with, and the real flight number when
 * the leg suggests one — `ITY1234` is the airline `ITY` and the flight `1234`, which is the pair SimBrief's form takes.
 * Nothing else is filled in: the route, the fuel and the day are the pilot's.
 */
export function simbriefUrl(leg: PublicLegDto, type: string | null): string {
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

/**
 * The tabs this tour has, in their order. The briefing and the rules always — even an empty briefing says the tour has
 * none, and the rules tab holds what the tour asks —; the route when there is something to draw: legs, or the subtours of
 * a container; the reports of a signed in pilot who has some, or whose answer is still on its way (`undefined`), so that the
 * address the report page goes back to (`?tab=reports`) does not fall to the first tab while the answer loads. `null` is no
 * answer at all — a visitor, or a request that failed.
 */
export function pageTabs(
  tour: PublicTourDto,
  signedIn: boolean,
  mine: MyTourDto | null | undefined,
): TourPageTab[] {
  const route = tour.legs.length > 0 || (tour.kind === 'Container' && tour.subtours.length > 0);
  const reports = signedIn && (mine === undefined || (mine !== null && mine.reports.length > 0));

  return TOUR_PAGE_TABS.filter((tab) => (tab !== 'route' || route) && (tab !== 'reports' || reports));
}

/** The tab to open: the one the address asks for when this tour has it, the first otherwise. */
export function openTab(asked: TourPageTab | undefined, tabs: readonly TourPageTab[]): TourPageTab {
  return asked !== undefined && tabs.includes(asked) ? asked : 'overview';
}

/**
 * The leg the pilot flies next, as the server says it: its own «next», or the first that may be reported now. None on an
 * `Open` tour (a flight of the pilot's choice, not a leg), on a container, or when nothing may be flown.
 */
export function nextLeg(tour: PublicTourDto, mine: MyTourDto): PublicLegDto | null {
  if (tour.kind === 'Open' || tour.kind === 'Container') {
    return null;
  }

  const id = mine.next ?? mine.flyable[0] ?? null;

  return tour.legs.find((leg) => leg.id === id) ?? null;
}

/** One group of legs in the route: every leg of the tour, or — on a hub tour — one rotation, or the legs between hubs. */
export interface LegGroup {
  readonly key: string;
  /** The rotation the legs fly, with its number among the rotations of the same hub; none for the legs between hubs. */
  readonly rotation: { readonly hub: string; readonly number: number } | null;
  /** Whether the group has a heading: only a hub tour's groups do. */
  readonly titled: boolean;
  readonly legs: readonly PublicLegDto[];
}

/**
 * The legs as the route draws them (design 11 §4, tab 2): one group for most tours; on a hub tour one group for each run of
 * consecutive legs of the same rotation, in the order the tour flies them, and the connections between two hubs as groups
 * of their own. A rotation is numbered among the rotations of its hub, in their order (`sort`), as the editor orders them.
 */
export function legGroups(tour: PublicTourDto): LegGroup[] {
  if (tour.kind !== 'Hub') {
    return tour.legs.length === 0 ? [] : [{ key: 'all', rotation: null, titled: false, legs: tour.legs }];
  }

  const rotations = new Map(tour.rotations.map((rotation) => [rotation.id, rotation]));
  const numbers = new Map<number, number>();
  const byHub = new Map<string, PublicRotationDto[]>();
  for (const rotation of [...tour.rotations].sort((a, b) => a.sort - b.sort)) {
    const ofHub = byHub.get(rotation.icao) ?? [];
    ofHub.push(rotation);
    byHub.set(rotation.icao, ofHub);
    numbers.set(rotation.id, ofHub.length);
  }

  const groups: { key: string; rotationId: number | null; legs: PublicLegDto[] }[] = [];
  for (const leg of tour.legs) {
    const rotationId = leg.rotationId ?? null;
    const last = groups.at(-1);

    if (last !== undefined && last.rotationId === rotationId) {
      last.legs.push(leg);
    } else {
      groups.push({ key: `${rotationId ?? 'between'}-${leg.id}`, rotationId, legs: [leg] });
    }
  }

  return groups.map((group) => {
    const rotation = group.rotationId === null ? undefined : rotations.get(group.rotationId);

    return {
      key: group.key,
      rotation: rotation === undefined ? null : { hub: rotation.icao, number: numbers.get(rotation.id) ?? 1 },
      titled: true,
      legs: group.legs,
    };
  });
}

/** The tour's rules in two groups, as the division writes them and as the tour adds or amends them (design M2 §5.2). */
export function ruleGroups(tour: PublicTourDto): {
  readonly key: 'division' | 'tour';
  readonly rules: PublicTourDto['rules'];
}[] {
  const division = tour.rules.filter((rule) => rule.tourId === null || rule.tourId === undefined);
  const own = tour.rules.filter((rule) => rule.tourId !== null && rule.tourId !== undefined);

  return [
    ...(division.length === 0 ? [] : [{ key: 'division' as const, rules: division }]),
    ...(own.length === 0 ? [] : [{ key: 'tour' as const, rules: own }]),
  ];
}
