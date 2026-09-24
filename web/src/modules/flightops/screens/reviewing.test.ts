import { describe, expect, it } from 'vitest';

import { queueChecks, type ReviewDto, type ReviewFlightDto, type ReviewPlanDto } from '../api';

import {
  eventNote,
  evidenceText,
  hhmm,
  planAtTakeoff,
  queueOrderOf,
  queueSortOf,
  reviewMapLegs,
  reviewMapTracks,
} from './reviewing';

/**
 * How the validation pages draw the server's answer (T13b). Nothing here decides: the suggestion and what the reader may
 * do are `PirepReview`'s, proved by `PirepTests.Review`.
 */

const plan = (revision: number): ReviewPlanDto => ({
  revision,
  filedAt: '2026-09-22T10:00:00Z',
  departureIcao: 'LIRF',
  arrivalIcao: 'LIMC',
  alternateIcao: null,
  secondAlternateIcao: null,
  aircraftIcao: 'A320',
  wakeTurbulence: 'M',
  equipment: 'SDFGRY',
  transponder: 'S',
  flightRules: 'I',
  flightType: 'S',
  level: 'F360',
  speed: 'N0450',
  route: 'RAVA5A RAVAL UL995 TOP',
  remarks: null,
  departureTimeMinutes: 600,
  enrouteMinutes: 70,
});

const flight = (seq: number, from: string, to: string, atTakeoff: number | null): ReviewFlightDto => ({
  seq,
  trackerSessionId: 100 + seq,
  callsign: 'ITY101',
  aircraft: 'A320',
  departureIcao: from,
  arrivalIcao: to,
  takeoffAt: '2026-09-22T10:10:00Z',
  landingAt: '2026-09-22T11:20:00Z',
  flightPlans: [plan(1), plan(2), plan(3)],
  planAtTakeoffRevision: atTakeoff,
  hasTrack: true,
});

const airport = (icao: string, latitude: number | null, longitude: number | null) => ({
  icao,
  iata: null,
  name: icao,
  countryId: 'XX',
  latitude,
  longitude,
  elevationFeet: null,
});

function review(overrides: Partial<ReviewDto>): ReviewDto {
  return {
    leg: {
      legId: 1,
      number: 1,
      departureIcao: 'LIRF',
      arrivalIcao: 'LIMC',
      distanceNm: 319,
      callsigns: [],
      aircraft: { types: [], groups: [] },
    },
    airports: [airport('LIRF', 41.8, 12.24), airport('LIMC', 45.63, 8.72), airport('LIML', 45.45, 9.28)],
    isDiversion: false,
    flights: [flight(1, 'LIRF', 'LIMC', 2)],
    ...overrides,
  } as ReviewDto;
}

describe('the order of the queue', () => {
  it('is by date unless the validator chose by tour', () => {
    expect(queueOrderOf(null)).toBe('date');
    expect(queueOrderOf('tour')).toBe('tour');
    expect(queueOrderOf('something else')).toBe('date');
  });

  it('sorts on the column the server declared', () => {
    expect(queueSortOf('date')).toBe('queuedAt');
    expect(queueSortOf('tour')).toBe('tourId');
  });
});

describe('the plan at take-off', () => {
  it('is the revision the server named, and none when it named none', () => {
    expect(planAtTakeoff(flight(1, 'LIRF', 'LIMC', 2))?.revision).toBe(2);
    expect(planAtTakeoff(flight(1, 'LIRF', 'LIMC', null))).toBeNull();
  });

  it('writes its times as a plan does', () => {
    expect(hhmm(600)).toBe('1000');
    expect(hhmm(70)).toBe('0110');
    expect(hhmm(null)).toBe('—');
  });
});

describe('the history', () => {
  const t = ((key: string) => `«${key}»`) as never;

  it('translates the steps the server wrote and keeps the words a validator wrote', () => {
    expect(eventNote('flightops:events.taken', t)).toBe('«flightops:events.taken»');
    expect(eventNote('The pilot sent the wrong flight: flightops:events is not a key here.', t)).toBe(
      'The pilot sent the wrong flight: flightops:events is not a key here.',
    );
    expect(eventNote(null, t)).toBeNull();
  });
});

describe('the map', () => {
  it('draws the leg as it was frozen', () => {
    expect(reviewMapLegs(review({})).map((leg) => [leg.from.code, leg.to.code, leg.status])).toEqual([
      ['LIRF', 'LIMC', 'todo'],
    ]);
  });

  it('adds each flight of a diversion, as waiting', () => {
    const diverted = review({
      isDiversion: true,
      flights: [flight(1, 'LIRF', 'LIML', 1), flight(2, 'LIML', 'LIMC', 1)],
    });

    expect(reviewMapLegs(diverted).map((leg) => [leg.from.code, leg.to.code, leg.status])).toEqual([
      ['LIRF', 'LIMC', 'todo'],
      ['LIRF', 'LIML', 'pending'],
      ['LIML', 'LIMC', 'pending'],
    ]);
  });

  it('leaves out a line to an airport the reference data has no position for', () => {
    const unknown = review({ airports: [airport('LIRF', 41.8, 12.24), airport('LIMC', null, null)] });
    expect(reviewMapLegs(unknown)).toEqual([]);
  });

  it('draws the tracks the server still has, and no line of a single point', () => {
    const point = (latitude: number) => ({
      at: '2026-09-22T10:10:00Z',
      latitude,
      longitude: 12,
      altitudeFeet: 0,
      groundSpeedKnots: 0,
      heading: 0,
      onGround: false,
      state: null,
      transponder: null,
    });

    expect(
      reviewMapTracks([
        { seq: 1, points: [point(41), point(42)] },
        { seq: 2, points: null },
        { seq: 3, points: [point(43)] },
      ]),
    ).toEqual([
      {
        id: 1,
        points: [
          { latitude: 41, longitude: 12 },
          { latitude: 42, longitude: 12 },
        ],
      },
    ]);
    expect(reviewMapTracks(undefined)).toEqual([]);
  });
});

describe('the checks', () => {
  const t = ((key: string, values?: Record<string, string>) =>
    `«${key}${values === undefined ? '' : ` ${JSON.stringify(values)}`}»`) as never;

  it('words the lines of the server, keeps those of the agent, and says which flight of a diversion', () => {
    expect(evidenceText(t, { key: 'flightops:evidence.alternateMissing' })).toBe(
      '«flightops:evidence.alternateMissing {}»',
    );
    expect(evidenceText(t, { key: null, text: 'FL350 westbound on a DCT segment' })).toBe(
      'FL350 westbound on a DCT segment',
    );
    expect(evidenceText(t, { key: 'flightops:evidence.noPlan', values: { flight: '2' } })).toBe(
      '«flightops:review.checks.flight {"flight":"2"}»«flightops:evidence.noPlan {"flight":"2"}»',
    );
  });

  it('names what the checks propose in the queue, and nothing before they ran', () => {
    expect(queueChecks({ failedChecks: 0, checkSuggestion: null })).toBeNull();
    expect(queueChecks({ failedChecks: 0, checkSuggestion: 'Accepted' })).toBe('Clean');
    expect(queueChecks({ failedChecks: 2, checkSuggestion: 'Accepted' })).toBe('Accept');
    expect(queueChecks({ failedChecks: 1, checkSuggestion: 'Rejected' })).toBe('Reject');
  });
});
