import { describe, expect, it } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { MyTourDto, PirepDto, PublicLegDto, PublicTourDto } from '../api';
import englishFlightOps from '../locales/en/flightops.json';

import {
  formatMinutes,
  legGroups,
  nextLeg,
  openTab,
  pageTabs,
  ruleGroups,
  simbriefUrl,
  tourFigures,
} from './tourPage';

/**
 * The page of a tour as design 11 §4 sorts it: which tabs a tour has, which one opens, the next leg, the legs of a hub tour
 * by rotation, the rules by who wrote them, and the numbers of the head — all of them sorted answers of the server.
 */

const leg = (id: number, rotationId: number | null = null): PublicLegDto => ({
  id,
  number: id,
  kind: rotationId === null ? 'HubConnection' : 'Normal',
  rotationId,
  departureIcao: 'LIRF',
  departureIata: null,
  departureLatitude: 41.8,
  departureLongitude: 12.25,
  arrivalIcao: 'LIMC',
  arrivalIata: null,
  arrivalLatitude: 45.63,
  arrivalLongitude: 8.72,
  distanceNm: 319,
  estimatedMinutes: 65,
  callsigns: ['ITY1234'],
  flightNumbers: [],
  aircraft: { types: [], groups: [] },
  releaseAt: null,
  released: true,
});

const tour = (patch: Partial<PublicTourDto> = {}): PublicTourDto => ({
  id: 1,
  slug: 'across-the-alps',
  kind: 'Sequential',
  title: { en: 'Across the Alps' },
  summary: { en: 'Two legs over the mountains.' },
  briefing: { schemaVersion: 1, sections: [] },
  coverMediaId: null,
  bannerMediaId: null,
  state: 'Open',
  releaseAt: '2026-09-01T00:00:00Z',
  closeAt: '2026-12-31T23:59:00Z',
  reportWindowDays: 7,
  progression: 'FlyAhead',
  hubRotationOrder: null,
  requiresProcedures: false,
  minPilotRating: null,
  minPilotRatingName: null,
  referenceAircraftIcao: null,
  aircraft: { types: [], groups: [] },
  requiredNm: null,
  requiredSubtours: null,
  openGoal: null,
  openGoalParameters: null,
  openGoalValues: [],
  constraints: [],
  callsignRules: [],
  rules: [],
  errors: [],
  rotations: [],
  legs: [leg(1), leg(2)],
  totalNm: 638,
  totalEstimatedMinutes: 130,
  parent: null,
  subtours: [],
  department: 'FOD',
  award: null,
  ...patch,
});

const mine = (patch: Partial<MyTourDto> = {}): MyTourDto => ({
  tourId: 1,
  legs: [],
  flyable: [],
  next: null,
  finished: false,
  blocked: null,
  goal: null,
  reports: [],
  ...patch,
});

describe('the tabs of the page of a tour', () => {
  it('are the four of the design for a pilot with reports, in their order', () => {
    expect(pageTabs(tour(), true, mine({ reports: [{ id: 9 } as PirepDto] }))).toEqual([
      'overview',
      'route',
      'rules',
      'reports',
    ]);
  });

  it('have no reports for a visitor, nor for a pilot who has none or whose answer failed', () => {
    expect(pageTabs(tour(), false, null)).toEqual(['overview', 'route', 'rules']);
    expect(pageTabs(tour(), true, mine())).toEqual(['overview', 'route', 'rules']);
    expect(pageTabs(tour(), true, null)).toEqual(['overview', 'route', 'rules']);
  });

  it('keep the reports while the answer is on its way, so the address of the report page does not fall to the first', () => {
    expect(pageTabs(tour(), true, undefined)).toContain('reports');
    expect(openTab('reports', pageTabs(tour(), true, undefined))).toBe('reports');
  });

  it('have no route on a tour with nothing to draw, and one on a container with subtours', () => {
    expect(pageTabs(tour({ kind: 'Open', legs: [] }), false, null)).toEqual(['overview', 'rules']);

    const subtour = {
      id: 2,
      slug: 'part-one',
      title: { en: 'Part one' },
      summary: { en: '' },
      state: 'Open' as const,
      releaseAt: '2026-09-01T00:00:00Z',
      closeAt: '2026-12-31T23:59:00Z',
      legs: 3,
      totalNm: 900,
    };
    expect(pageTabs(tour({ kind: 'Container', legs: [], subtours: [subtour] }), false, null)).toContain(
      'route',
    );
  });

  it('open the one the address asks for when the tour has it, the first otherwise', () => {
    expect(openTab('rules', ['overview', 'route', 'rules'])).toBe('rules');
    expect(openTab('reports', ['overview', 'route', 'rules'])).toBe('overview');
    expect(openTab(undefined, ['overview', 'route', 'rules'])).toBe('overview');
  });
});

describe('the next leg of «Your tour»', () => {
  it("is the server's next, or the first that may be reported now", () => {
    expect(nextLeg(tour(), mine({ next: 2, flyable: [1, 2] }))?.id).toBe(2);
    expect(nextLeg(tour(), mine({ next: null, flyable: [1] }))?.id).toBe(1);
    expect(nextLeg(tour(), mine())).toBeNull();
  });

  it('is none on an Open tour or a container, which have no leg to fly next', () => {
    expect(nextLeg(tour({ kind: 'Open' }), mine({ next: 1 }))).toBeNull();
    expect(nextLeg(tour({ kind: 'Container' }), mine({ next: 1 }))).toBeNull();
  });
});

describe('the legs of the route', () => {
  it('are one untitled group on a tour that is not a hub tour', () => {
    const groups = legGroups(tour());

    expect(groups).toHaveLength(1);
    expect(groups[0]!.titled).toBe(false);
    expect(groups[0]!.legs.map((entry) => entry.id)).toEqual([1, 2]);
  });

  it('are grouped by rotation on a hub tour, numbered within their hub, with the legs between hubs apart', () => {
    const hub = tour({
      kind: 'Hub',
      rotations: [
        { id: 10, icao: 'LIRF', sort: 0, size: 2 },
        { id: 11, icao: 'LIRF', sort: 1, size: 2 },
        { id: 20, icao: 'LIMC', sort: 2, size: 2 },
      ],
      legs: [leg(1, 10), leg(2, 10), leg(3, 11), leg(4, 11), leg(5), leg(6, 20), leg(7, 20)],
    });

    expect(
      legGroups(hub).map((group) => ({
        rotation: group.rotation,
        legs: group.legs.map((entry) => entry.id),
      })),
    ).toEqual([
      { rotation: { hub: 'LIRF', number: 1 }, legs: [1, 2] },
      { rotation: { hub: 'LIRF', number: 2 }, legs: [3, 4] },
      { rotation: null, legs: [5] },
      { rotation: { hub: 'LIMC', number: 1 }, legs: [6, 7] },
    ]);
  });
});

describe('the rules of the rules tab', () => {
  it("are the division's first and the tour's own after, and an empty group is left out", () => {
    const rule = (id: number, tourId: number | null) => ({
      id,
      tourId,
      code: `R${id}`,
      title: { en: `Rule ${id}` },
      text: { en: '' },
      amendsRuleId: null,
      amendsCode: null,
      checkKey: null,
      parameters: {},
      values: [],
      errorIds: [],
    });

    expect(
      ruleGroups(tour({ rules: [rule(1, null), rule(2, 1), rule(3, null)] })).map((group) => ({
        key: group.key,
        rules: group.rules.map((entry) => entry.id),
      })),
    ).toEqual([
      { key: 'division', rules: [1, 3] },
      { key: 'tour', rules: [2] },
    ]);
    expect(ruleGroups(tour({ rules: [rule(2, 1)] })).map((group) => group.key)).toEqual(['tour']);
  });
});

describe('the numbers of the head', () => {
  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishFlightOps);
  const t = i18n.getFixedT('en');
  const read = (value: { en?: string } | null | undefined) => value?.en ?? '';

  it('say the legs, the miles, the time with the reference aircraft, the rating, the aircraft and the award', () => {
    const figures = tourFigures(
      tour({
        referenceAircraftIcao: 'A320',
        minPilotRatingName: 'PP',
        aircraft: { types: ['A320', 'A321', 'B738'], groups: [] },
        award: { id: 4, name: { en: 'Alpine award' } },
      }),
      t,
      read,
    );

    expect(figures.map((figure) => [figure.key, figure.value, figure.hint])).toEqual([
      ['legs', '2', undefined],
      ['distance', '638 NM', undefined],
      ['time', '2:10', 'Reference aircraft: A320'],
      ['rating', 'PP', expect.any(String)],
      ['aircraft', 'A320 · A321', 'and 1 more'],
      ['award', 'Alpine award', undefined],
    ]);
  });

  it('say any aircraft when the tour admits every one, and leave out what the tour has not got', () => {
    const figures = tourFigures(tour({ legs: [], totalNm: 0, totalEstimatedMinutes: null }), t, read);

    expect(figures.map((figure) => [figure.key, figure.value])).toEqual([['aircraft', 'Any aircraft']]);
  });

  it('say the subtours of a container and how many to complete, and no aircraft', () => {
    const figures = tourFigures(
      tour({ kind: 'Container', legs: [], totalNm: 0, totalEstimatedMinutes: null, requiredSubtours: 2 }),
      t,
      read,
    );

    expect(figures.map((figure) => [figure.key, figure.value, figure.hint])).toEqual([
      ['subtours', '0', '2 to complete'],
    ]);
  });
});

describe('the small sayings of the page', () => {
  it('write a flight time as hours and minutes', () => {
    expect(formatMinutes(65)).toBe('1:05');
    expect(formatMinutes(600)).toBe('10:00');
  });

  it('fill SimBrief with the airports, the type and the flight number of the first callsign', () => {
    expect(simbriefUrl(leg(1), 'A320')).toBe(
      'https://dispatch.simbrief.com/options/custom?orig=LIRF&dest=LIMC&type=A320&airline=ITY&fltnum=1234',
    );
  });
});
