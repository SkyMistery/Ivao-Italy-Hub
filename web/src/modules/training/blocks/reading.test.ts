import { expect, test } from 'vitest';

import type { MyTrainingPathDto, TraineeTrainingDto, TrainingState } from '../api';
import { REFUSALS } from '../screens/trainee';

import { laddersNow, queueHref } from './reading';

/**
 * What the blocks of the training read out of their providers' answers (A10b): the server decides every rule — which training is
 * open, what refuses a request —, and these only say which training a ladder's line is about. The ratings are made up, as a test may
 * build its own (design M3 §10).
 */

const NOW = Date.parse('2026-10-10T12:00:00Z');

function path(overrides: Partial<MyTrainingPathDto> = {}): MyTrainingPathDto {
  return {
    kind: 'Atc',
    ratingShortName: 'R2',
    hours: 120,
    next: { kind: 'Atc', number: 3, shortName: 'R3', nameKey: 'ratings.Atc.R3' },
    isMockExam: false,
    asksPosition: true,
    positions: [],
    refusal: null,
    bannedUntil: null,
    openTrainingId: null,
    waitUntil: null,
    minimumHours: null,
    ...overrides,
  };
}

function training(
  id: number,
  state: TrainingState,
  overrides: Partial<TraineeTrainingDto> = {},
): TraineeTrainingDto {
  return {
    id,
    kind: 'Atc',
    rating: 3,
    ratingShortName: 'R3',
    isMockExam: false,
    position: 'XXAA_BOX',
    state,
    rejection: null,
    rejectionReason: null,
    availabilityText: null,
    notesText: null,
    requestedAt: '2026-09-01T10:00:00Z',
    decidedAt: null,
    trainer: null,
    slots: [],
    scheduledStartUtc: null,
    held: false,
    completedAt: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    cooldownWaived: false,
    generalComment: null,
    sheet: [],
    sessions: [],
    rowVersion: '2026-09-01T10:00:00.000001Z',
    ...overrides,
  };
}

test('a ladder with a training open says that training and the dates still to choose on it, not the refusal it is', () => {
  const open = training(7, 'Assigned', {
    slots: [
      { id: 1, startsAtUtc: '2026-10-09T18:00:00Z', endsAtUtc: '2026-10-09T20:00:00Z' }, // gone by
      { id: 2, startsAtUtc: '2026-10-12T18:00:00Z', endsAtUtc: '2026-10-12T20:00:00Z' },
      { id: 3, startsAtUtc: '2026-10-14T18:00:00Z', endsAtUtc: '2026-10-14T20:00:00Z' },
    ],
  });
  const [atc] = laddersNow(
    { paths: [path({ refusal: REFUSALS.open, openTrainingId: 7 })], trainings: [open] },
    NOW,
  );

  expect(atc?.open?.id).toBe(7);
  expect(atc?.toChoose).toBe(2);
  expect(atc?.saysStanding).toBe(false);
});

test('a ban is said beside the training open on a ladder, and a free ladder says what may be asked for', () => {
  const banned = path({ refusal: REFUSALS.banned, openTrainingId: 7 });
  const free = path({ kind: 'Pilot', asksPosition: false });
  const ladders = laddersNow(
    {
      paths: [banned, free],
      trainings: [training(7, 'Scheduled', { scheduledStartUtc: '2026-10-11T18:00:00Z' })],
    },
    NOW,
  );

  expect(ladders.map((ladder) => [ladder.open?.id ?? null, ladder.toChoose, ladder.saysStanding])).toEqual([
    [7, 0, true],
    [null, 0, true],
  ]);
});

test('the last report of a ladder is its latest, on that ladder alone', () => {
  const trainings = [
    training(1, 'Completed', { completedAt: '2026-09-01T20:00:00Z' }),
    training(2, 'Completed', { completedAt: '2026-09-20T20:00:00Z' }),
    training(3, 'Completed', { kind: 'Pilot', completedAt: '2026-10-01T20:00:00Z' }),
    training(4, 'NoShow', { closedAt: '2026-10-02T20:00:00Z' }),
  ];

  const [atc, pilot] = laddersNow({ paths: [path(), path({ kind: 'Pilot' })], trainings }, NOW);
  expect(atc?.lastReport?.id).toBe(2);
  expect(pilot?.lastReport?.id).toBe(3);
  expect(laddersNow({ paths: [path()], trainings: [] }, NOW)[0]?.lastReport).toBeNull();
});

test('a queue of the staff is the list narrowed to it', () => {
  expect(queueHref('toApprove')).toBe('/staff/training?queue=toApprove');
  expect(queueHref('toAssign')).toBe('/staff/training?queue=toAssign');
});
