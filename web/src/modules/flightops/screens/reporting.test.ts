import { describe, expect, it } from 'vitest';

import { ApiError } from '../../../shared/api/problem';
import type { MyTourDto, PirepDto, PublicLegDto } from '../api';

import {
  contactsToSend,
  declarationOf,
  exemptionsToSend,
  mapLeg,
  mergeSessions,
  reportActions,
  sessionsOf,
  splitRefusal,
} from './reporting';

const leg = (id: number, released = true): PublicLegDto => ({
  id,
  number: id,
  kind: 'Normal',
  rotationId: null,
  departureIcao: 'LIRF',
  departureIata: null,
  departureLatitude: 41.8,
  departureLongitude: 12.25,
  arrivalIcao: 'LIMC',
  arrivalIata: null,
  arrivalLatitude: 45.63,
  arrivalLongitude: 8.72,
  distanceNm: 319,
  estimatedMinutes: null,
  callsigns: [],
  flightNumbers: [],
  aircraft: { types: [], groups: [] },
  releaseAt: null,
  released,
});

const mine = (legs: MyTourDto['legs']): MyTourDto => ({
  tourId: 1,
  legs,
  flyable: [],
  next: null,
  finished: false,
  blocked: null,
  goal: null,
  reports: [],
});

const session = (id: number) => ({
  id,
  callsign: 'ITY123',
  startedAt: '2026-09-22T10:00:00Z',
  endedAt: '2026-09-22T11:00:00Z',
  departureIcao: 'LIRF',
  arrivalIcao: 'LIMC',
  aircraft: 'A320',
});

describe('the colour of a leg on the map', () => {
  it('is the visitor colour for somebody who is not signed in', () => {
    expect(mapLeg(leg(1), undefined).status).toBe('todo');
    expect(mapLeg(leg(2, false), undefined).status).toBe('locked');
  });

  it('is the server answer for a pilot, in the four states of the map', () => {
    const answer = mine([
      { id: 1, progress: 'Done' },
      { id: 2, progress: 'Pending' },
      { id: 3, progress: 'Locked' },
      { id: 4, progress: 'Todo' },
    ]);

    expect([1, 2, 3, 4].map((id) => mapLeg(leg(id), answer).status)).toEqual([
      'done',
      'pending',
      'locked',
      'todo',
    ]);
  });

  it('keeps the visitor colour for a leg the server did not mention', () => {
    expect(mapLeg(leg(9, false), mine([])).status).toBe('locked');
  });
});

describe('what a pilot can do with a report', () => {
  it('withdraws only from the queue and corrects only what was sent back', () => {
    expect(reportActions('Queued')).toEqual({ withdraw: true, correct: false });
    expect(reportActions('ToModify')).toEqual({ withdraw: false, correct: true });
    for (const status of ['InReview', 'Accepted', 'Rejected', 'Withdrawn'] as const) {
      expect(reportActions(status)).toEqual({ withdraw: false, correct: false });
    }
  });
});

describe('the flights a correction starts from', () => {
  it('offers the flight the report already holds, once, ahead of what the tracker found', () => {
    const report = {
      flights: [
        {
          seq: 1,
          trackerSessionId: 7,
          callsign: 'ITY123',
          aircraft: 'A320',
          departureIcao: 'LIRF',
          arrivalIcao: 'LIMC',
          takeoffAt: '2026-09-22T10:05:00Z',
          landingAt: null,
          flightRules: 'I',
        },
      ],
    } as unknown as PirepDto;

    const kept = sessionsOf(report);
    expect(kept[0]).toMatchObject({ id: 7, endedAt: '2026-09-22T10:05:00Z' });
    expect(mergeSessions(kept, [session(8), session(7)]).map((entry) => entry.id)).toEqual([7, 8]);
  });
});

describe('a refusal split across the page', () => {
  const refusal = new ApiError(400, {
    title: 'Invalid',
    errors: {
      sessionIds: ['flightops:errors.reportSessionClaimed'],
      star: ['flightops:errors.reportProcedureRequired'],
    },
  });

  it('sends the details form its own fields and the flight half the rest', () => {
    const { details, flight } = splitRefusal(refusal, ['sid', 'star', 'approach', 'pilotRemarks']);

    expect((details as ApiError).problem?.errors).toEqual({
      star: ['flightops:errors.reportProcedureRequired'],
    });
    expect(flight?.problem?.errors).toEqual({ sessionIds: ['flightops:errors.reportSessionClaimed'] });
  });

  it('tells the details form nothing when nothing is about it', () => {
    const only = new ApiError(400, { errors: { tour: ['flightops:errors.reportBanned'] } });

    expect(splitRefusal(only, ['sid']).details).toBeNull();
  });

  it('leaves anything that is not a refusal to the details form', () => {
    const { details, flight } = splitRefusal(new Error('network'), ['sid']);

    expect(details?.message).toBe('network');
    expect(flight).toBeNull();
  });
});

describe('the controllers a report sends', () => {
  const proposed = [
    { callsign: 'LIRF_TWR', frequency: '118.705', origin: 'Proposed' as const },
    { callsign: 'LIRR_CTR', frequency: null, origin: 'Proposed' as const },
  ];

  it('sends the proposed ones kept, then the added ones once, and never the removed', () => {
    const sent = contactsToSend(
      proposed,
      ['LIRR_CTR'],
      [
        { callsign: ' limm_ctr ', frequency: '134.205' },
        { callsign: 'LIRF_TWR', frequency: '' },
        { callsign: '', frequency: '' },
      ],
    );

    expect(sent).toEqual([
      { callsign: 'LIRF_TWR', frequency: '118.705' },
      { callsign: 'LIMM_CTR', frequency: '134.205' },
    ]);
  });

  it('sends an exemption only once its controller is chosen, with no empty note', () => {
    expect(
      exemptionsToSend([
        { callsign: 'LIRF_TWR', kind: 'FreeSpeed', note: '  ' },
        { callsign: '', kind: 'Other', note: 'vectors' },
      ]),
    ).toEqual([{ callsign: 'LIRF_TWR', kind: 'FreeSpeed', note: null }]);
  });

  it('starts a correction from what the report declared', () => {
    const report = {
      atcContacts: [
        { callsign: 'LIRF_TWR', frequency: '118.705', origin: 'Proposed' },
        { callsign: 'LIRR_CTR', frequency: null, origin: 'Removed' },
        { callsign: 'LIMM_CTR', frequency: null, origin: 'Added' },
      ],
      exemptions: [{ callsign: 'LIMM_CTR', kind: 'Other', note: 'vectors', status: 'Online', softens: [] }],
    } as unknown as PirepDto;

    expect(declarationOf(report)).toEqual({
      removed: ['LIRR_CTR'],
      declaration: {
        added: [{ callsign: 'LIMM_CTR', frequency: '' }],
        exemptions: [{ callsign: 'LIMM_CTR', kind: 'Other', note: 'vectors' }],
      },
    });
    expect(declarationOf(null)).toEqual({ removed: [], declaration: { added: [], exemptions: [] } });
  });
});
