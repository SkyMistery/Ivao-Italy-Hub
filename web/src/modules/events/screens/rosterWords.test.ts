import type { TFunction } from 'i18next';
import { describe, expect, it } from 'vitest';

import type { RosterRowDto } from '../api';

import { rosterLines } from './rosterWords';

/** A `t` that writes the key and what it was given, so a test reads which words a row asks for. */
const t = ((key: string, options?: Record<string, unknown>) =>
  options === undefined ? key : `${key} ${JSON.stringify(options)}`) as unknown as TFunction;

const base: RosterRowDto = {
  positionId: 1,
  callsign: 'XXAA_TWR',
  fir: 'XXAA',
  fromUtc: '2026-11-21T17:00:00Z',
  toUtc: '2026-11-21T18:00:00Z',
  state: 'Proposed',
  shiftId: 7,
  controller: { vid: 761116, name: 'Test Roster' },
  rating: 'Q3',
  reason: {
    candidates: 2,
    decidedBy: 'Experience',
    preferred: true,
    callsignConnections: 3,
    typeConnections: 4,
    penalty: 0,
    shiftsBefore: 0,
  },
  warnings: [],
  candidates: 2,
};

describe('the words of a row of the roster', () => {
  it('say what decided a proposed shift, how many could take it and the connections, and the penalty only when there is one', () => {
    expect(rosterLines(base, t)).toEqual({
      why: [
        'events:atc.roster.why.decidedBy.Experience',
        'events:atc.roster.why.candidates {"count":2}',
        'events:atc.roster.why.connections {"callsign":3,"type":4}',
      ],
      alerts: [],
    });

    const penalised = { ...base, reason: { ...base.reason!, penalty: 0.08333 } };
    expect(rosterLines(penalised, t).why.at(-1)).toBe('events:atc.roster.why.penalty {"value":"0.08"}');
  });

  it('say the experience was not counted when the archive did not answer', () => {
    const unknown = {
      ...base,
      reason: { ...base.reason!, callsignConnections: null, typeConnections: null },
    };

    expect(rosterLines(unknown, t).why).toContain('events:atc.roster.why.noExperience');
  });

  it('say a shift of the staff is theirs, and an uncovered piece whether nobody could take it or those who could are busy', () => {
    expect(rosterLines({ ...base, state: 'Staff', reason: null }, t).why).toEqual([
      'events:atc.roster.why.staff',
    ]);

    const uncovered = { ...base, state: 'Uncovered' as const, shiftId: null, controller: null, reason: null };
    expect(rosterLines({ ...uncovered, candidates: 0 }, t).why).toEqual([
      'events:atc.roster.why.uncoveredNobody',
    ]);
    expect(rosterLines(uncovered, t).why).toEqual(['events:atc.roster.why.uncoveredBusy {"count":2}']);
  });

  it("write each warning, the position's minimum in the core's own words", () => {
    const warned = {
      ...base,
      warnings: ['NotAvailable', 'BelowPositionMinimum', 'TooManyInARow'] as RosterRowDto['warnings'],
    };

    expect(rosterLines(warned, t).alerts).toEqual([
      'events:atc.roster.warnings.NotAvailable',
      'atcPositions.belowMinimum',
      'events:atc.roster.warnings.TooManyInARow',
    ]);
  });
});
