import type { TFunction } from 'i18next';
import { describe, expect, it } from 'vitest';

import type { RosterRowDto } from '../api';

import { isClosed } from './atcPaths';
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
  free: 0,
  editable: true,
};

describe('the words of a row of the roster', () => {
  it('say what decided a proposed shift, how many could take it and the connections, and the penalty only when there is one', () => {
    expect(rosterLines(base, t, 'en')).toEqual({
      why: [
        'events:atc.roster.why.decidedBy.Experience',
        'events:atc.roster.why.candidates {"count":2}',
        'events:atc.roster.why.connections {"callsign":3,"type":4}',
      ],
      alerts: [],
    });

    // The penalty as a number of the language on screen (the review of #262, point 4).
    const penalised = { ...base, reason: { ...base.reason!, penalty: 0.08333 } };
    expect(rosterLines(penalised, t, 'en').why.at(-1)).toBe('events:atc.roster.why.penalty {"value":"0.08"}');
    expect(rosterLines(penalised, t, 'it').why.at(-1)).toBe('events:atc.roster.why.penalty {"value":"0,08"}');
  });

  it('say the experience was not counted when the archive did not answer', () => {
    const unknown = {
      ...base,
      reason: { ...base.reason!, callsignConnections: null, typeConnections: null },
    };

    expect(rosterLines(unknown, t, 'en').why).toContain('events:atc.roster.why.noExperience');
  });

  it('say a shift of the staff is theirs, and of an uncovered piece who is free for it, who is busy, or that nobody could', () => {
    expect(rosterLines({ ...base, state: 'Staff', reason: null }, t, 'en').why).toEqual([
      'events:atc.roster.why.staff',
    ]);

    const uncovered = { ...base, state: 'Uncovered' as const, shiftId: null, controller: null, reason: null };
    expect(rosterLines({ ...uncovered, candidates: 0 }, t, 'en').why).toEqual([
      'events:atc.roster.why.uncoveredNobody',
    ]);
    expect(rosterLines(uncovered, t, 'en').why).toEqual(['events:atc.roster.why.uncoveredBusy {"count":2}']);
    expect(rosterLines({ ...uncovered, free: 1 }, t, 'en').why).toEqual([
      'events:atc.roster.why.uncoveredFree {"count":1}',
    ]);
  });

  it("write each warning, the position's minimum in the core's own words", () => {
    const warned = {
      ...base,
      warnings: ['NotAvailable', 'BelowPositionMinimum', 'TooManyInARow'] as RosterRowDto['warnings'],
    };

    expect(rosterLines(warned, t, 'en').alerts).toEqual([
      'events:atc.roster.warnings.NotAvailable',
      'atcPositions.belowMinimum',
      'events:atc.roster.warnings.TooManyInARow',
    ]);
  });
});

describe('an event closed to the ATC', () => {
  const now = Date.parse('2026-11-22T00:00:00Z');

  it('is cancelled or over, and a draft whose end has passed too (E11a third reading on #256, point 2)', () => {
    expect(isClosed({ state: 'Cancelled', endsAtUtc: '2026-12-01T00:00:00Z' }, now)).toBe(true);
    expect(isClosed({ state: 'Ended', endsAtUtc: '2026-11-21T23:00:00Z' }, now)).toBe(true);
    expect(isClosed({ state: 'Draft', endsAtUtc: '2026-11-21T23:00:00Z' }, now)).toBe(true);
    expect(isClosed({ state: 'Draft', endsAtUtc: '2026-11-23T23:00:00Z' }, now)).toBe(false);
  });
});
