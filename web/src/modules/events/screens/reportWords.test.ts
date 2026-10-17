import type { TFunction } from 'i18next';
import { describe, expect, it } from 'vitest';

import type { ReportItemDto } from '../api';
import { decidableReportStates, emptyAtcReport, emptyPilotReport, pilotReportValues } from '../schemas';

import { atcItems, checkLine, itemLine, pilotItems, reportLines } from './reportWords';

/** A `t` that writes the key and what it was given, so a test reads which words an item asks for. */
const t = ((key: string, options?: Record<string, unknown>) =>
  options === undefined ? key : `${key} ${JSON.stringify(options)}`) as unknown as TFunction;

const flight: ReportItemDto = {
  id: 1,
  ordinal: 0,
  callsign: 'TST004',
  departureIcao: 'XEA1',
  arrivalIcao: 'XEA2',
  flightDay: '2026-10-15T00:00:00Z',
  fromUtc: null,
  toUtc: null,
  sessionId: 1000007,
  minutes: 139,
  distanceNm: 273.5,
  verified: true,
};

const shift: ReportItemDto = {
  ...flight,
  callsign: 'XEA1_TWR',
  departureIcao: null,
  arrivalIcao: null,
  flightDay: null,
  fromUtc: '2026-10-15T10:30:00Z',
  toUtc: '2026-10-15T11:30:00Z',
  sessionId: null,
  minutes: 40,
  distanceNm: null,
};

describe('the words of an item of a report of support', () => {
  it('say what a flight and a shift declared, the times in UTC', () => {
    expect(itemLine(flight, 'Pilot', t, 'en')).toBe(
      'events:reports.flight {"callsign":"TST004","departure":"XEA1","arrival":"XEA2","day":"Oct 15, 2026"}',
    );
    expect(itemLine(shift, 'Atc', t, 'en')).toBe(
      'events:reports.shift {"callsign":"XEA1_TWR","from":"Oct 15, 2026, 10:30","to":"Oct 15, 2026, 11:30"}',
    );
  });

  it('say what the check found: flown with the distance, not found, controlled, too short, or not checked', () => {
    expect(checkLine(flight, 'Pilot', t)).toBe(
      'events:reports.check.flown {"minutes":139} events:reports.check.distance {"distance":273.5}',
    );
    expect(checkLine({ ...flight, verified: false, sessionId: null, minutes: null }, 'Pilot', t)).toBe(
      'events:reports.check.notFound events:reports.check.distance {"distance":273.5}',
    );
    expect(checkLine(shift, 'Atc', t)).toBe('events:reports.check.controlled {"minutes":40}');
    expect(checkLine({ ...shift, verified: false, minutes: 0 }, 'Atc', t)).toBe(
      'events:reports.check.tooShort {"minutes":0}',
    );
    expect(checkLine({ ...flight, verified: null }, 'Pilot', t)).toBe('events:reports.check.unavailable');
    expect(reportLines([flight], 'Pilot', t, 'en')).toHaveLength(1);
  });

  it('send a flight and a shift as the server takes them, the other kind left empty', () => {
    expect(
      pilotItems({
        items: [
          { callsign: 'T', departureIcao: 'XEA1', arrivalIcao: 'XEA2', flightDay: '2026-10-15T00:00:00Z' },
        ],
      }),
    ).toEqual([
      {
        callsign: 'T',
        departureIcao: 'XEA1',
        arrivalIcao: 'XEA2',
        flightDay: '2026-10-15T00:00:00Z',
        fromUtc: null,
        toUtc: null,
      },
    ]);
    expect(atcItems({ items: [{ callsign: 'XEA1_TWR' }] })).toEqual([
      {
        callsign: 'XEA1_TWR',
        departureIcao: null,
        arrivalIcao: null,
        flightDay: null,
        fromUtc: null,
        toUtc: null,
      },
    ]);
  });

  it('start a new report on the event and load a sent one again', () => {
    expect(emptyPilotReport('2026-10-15T10:00:00Z').items[0]?.flightDay).toBe('2026-10-15T00:00:00Z');
    expect(emptyAtcReport('2026-10-15T10:00:00Z', '2026-10-15T12:00:00Z').items[0]).toEqual({
      callsign: '',
      fromUtc: '2026-10-15T10:00:00Z',
      toUtc: '2026-10-15T12:00:00Z',
    });
    expect(pilotReportValues([flight]).items).toEqual([
      { callsign: 'TST004', departureIcao: 'XEA1', arrivalIcao: 'XEA2', flightDay: '2026-10-15T00:00:00Z' },
    ]);
  });

  it('offer both decisions on a report waiting, the other one on a decided report', () => {
    expect(decidableReportStates('Submitted')).toEqual(['Accepted', 'Rejected']);
    expect(decidableReportStates('Accepted')).toEqual(['Rejected']);
    expect(decidableReportStates('Rejected')).toEqual(['Accepted']);
  });
});
