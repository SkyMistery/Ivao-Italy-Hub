import { describe, expect, test } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { EventAtcTransferDto } from '../api';
import englishEvents from '../locales/en/events.json';

import { transferLines } from './rosterWords';

/**
 * The warnings of a request to hand a shift over (E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 7), in the
 * language on screen: that the shift is no longer theirs first, then the rules whoever would take it does not keep, in the roster's words.
 */

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'events', englishEvents);
const t = i18n.getFixedT('en');

function request(overrides: Partial<EventAtcTransferDto>): EventAtcTransferDto {
  return {
    id: 1,
    eventId: 2,
    ownerDepartment: 'ED',
    shiftId: 3,
    callsign: 'XXAA_TWR',
    fromUtc: '2099-11-21T17:00:00Z',
    toUtc: '2099-11-21T18:00:00Z',
    from: { vid: 10, name: 'A' },
    to: { vid: 11, name: 'B' },
    note: null,
    status: 'Pending',
    decidedBy: null,
    decidedAt: null,
    decisionNote: null,
    createdAt: '2099-11-20T17:00:00Z',
    stale: false,
    warnings: [],
    ...overrides,
  };
}

describe('the warnings of a request to hand a shift over', () => {
  test('none, for a request whoever takes it keeps every rule of', () => {
    expect(transferLines(request({}), t)).toEqual([]);
  });

  test('the shift no longer theirs first, then the rules in the roster’s words', () => {
    expect(transferLines(request({ stale: true, warnings: ['NotAvailable', 'Overlapping'] }), t)).toEqual([
      englishEvents.atc.transfers.stale,
      englishEvents.atc.roster.warnings.NotAvailable,
      englishEvents.atc.roster.warnings.Overlapping,
    ]);
  });
});
