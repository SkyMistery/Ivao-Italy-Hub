import { expect, test } from 'vitest';

import { ApiError } from '../../../shared/api/problem';
import { listSearchSchema } from '../../../shared/list';
import { memberLabel, type StaffTrainingDto, type TrainerCandidateDto, type TrainingState } from '../api';
import type { StaffTrainingsSearch } from '../schemas';

import { decisionOf, isConflict, listOrder, staffTrainingHref, trainerChoices } from './trainings';

/**
 * What the staff's pages of the trainings read out of what the server answered (A7): the order of the list when the reader
 * chose none, how the trainers are offered, and what is said of the decision. Who may do what, and who may train, are the
 * server's; the ratings here are made up — a test may build its own (design M3 §10).
 */

function search(overrides: Partial<StaffTrainingsSearch> = {}): StaffTrainingsSearch {
  return { ...listSearchSchema.parse({}), ...overrides };
}

function training(state: TrainingState, overrides: Partial<StaffTrainingDto> = {}): StaffTrainingDto {
  return {
    id: 41,
    kind: 'Atc',
    rating: 3,
    ratingShortName: 'R3',
    ratingNameKey: 'ratings.Atc.R3',
    isMockExam: false,
    position: 'XXAA_TWR',
    airportIcao: 'XXAA',
    fir: 'XXXX',
    trainee: { vid: 790099, name: 'Test Trainee' },
    traineeRatingShortName: 'R2',
    traineeHoursAtRequest: 120,
    requestedAt: '2026-09-20T10:00:00Z',
    theoryConfirmedAt: '2026-09-20T10:00:00Z',
    theoryExamUrl: null,
    availabilityText: null,
    notesText: null,
    state,
    rejection: null,
    rejectionReason: null,
    decidedBy: null,
    decidedAt: null,
    trainer: null,
    assignedBy: null,
    assignedAt: null,
    slots: [],
    scheduledStartUtc: null,
    held: false,
    dateChosenByTrainee: false,
    completedAt: null,
    closedBy: null,
    closedAt: null,
    closeReason: null,
    readyForMockExam: false,
    readyForExam: false,
    cooldownWaived: false,
    generalComment: null,
    staffComment: null,
    sheet: [],
    sessions: [],
    reservedLeftOut: false,
    actions: {
      canDecide: false,
      canAssign: false,
      canConduct: false,
      canClose: false,
      canRecordOutcome: false,
    },
    rowVersion: '2026-09-20T10:00:00.123456Z',
    ...overrides,
  };
}

function candidate(vid: number, overrides: Partial<TrainerCandidateDto> = {}): TrainerCandidateDto {
  return {
    vid,
    name: `Trainer ${String(vid)}`,
    ratingShortName: 'R5',
    positions: ['XX-T01'],
    isCurrent: false,
    ...overrides,
  };
}

test('a queue of work is read oldest first, every other view and the whole list newest first', () => {
  expect(listOrder(search({ queue: 'toApprove' })).dir).toBe('asc');
  expect(listOrder(search({ queue: 'toAssign' })).dir).toBe('asc');
  expect(listOrder(search({ queue: 'toClose' })).dir).toBe('asc');
  expect(listOrder(search({ queue: 'inProgress' })).dir).toBe('desc');
  expect(listOrder(search({ queue: 'history' })).dir).toBe('desc');
  expect(listOrder(search()).dir).toBe('desc');

  // The reader's own order stands.
  const sorted = search({ queue: 'toApprove', sort: 'createdAt', dir: 'desc' });
  expect(listOrder(sorted)).toBe(sorted);
});

test('the trainers are offered with their name, VID, rating and positions, the one already assigned left out', () => {
  const choices = trainerChoices([
    candidate(790101, { positions: ['XX-DIR'] }),
    candidate(790102, { isCurrent: true }),
    candidate(790103, { ratingShortName: null, positions: [] }),
    candidate(790104, { name: '', positions: ['XX-TC', 'XX-T02'] }),
  ]);

  expect(choices).toEqual([
    { value: '790101', label: 'Trainer 790101 (790101) · R5 · XX-DIR' },
    { value: '790103', label: 'Trainer 790103 (790103)' },
    { value: '790104', label: '790104 · R5 · XX-TC, XX-T02' },
  ]);
});

test('a person is named by the name the hub has and the VID, or by the VID alone', () => {
  expect(memberLabel({ vid: 790099, name: 'Test Trainee' })).toBe('Test Trainee (790099)');
  expect(memberLabel({ vid: 790099, name: null })).toBe('790099');
  expect(memberLabel({ vid: 790099, name: '' })).toBe('790099');
});

test('the decision is read from the state: none, accepted, refused with its reason, the hub for the theory, cancelled', () => {
  const advisor = { vid: 790098, name: 'Test Advisor' };

  expect(decisionOf(training('Requested'))).toEqual({ kind: 'none' });

  for (const state of ['Accepted', 'Assigned', 'Scheduled', 'Completed'] as const) {
    expect(decisionOf(training(state, { decidedBy: advisor, decidedAt: '2026-09-21T09:00:00Z' }))).toEqual({
      kind: 'accepted',
      by: advisor,
      at: '2026-09-21T09:00:00Z',
    });
  }

  expect(
    decisionOf(
      training('Rejected', {
        rejection: 'Staff',
        rejectionReason: 'More hours first.',
        decidedBy: advisor,
        decidedAt: '2026-09-21T09:00:00Z',
      }),
    ),
  ).toEqual({ kind: 'rejected', by: advisor, at: '2026-09-21T09:00:00Z', reason: 'More hours first.' });

  expect(
    decisionOf(training('Rejected', { rejection: 'TheoryNotPassed', decidedAt: '2026-09-20T10:00:00Z' })),
  ).toEqual({ kind: 'theory', at: '2026-09-20T10:00:00Z' });

  expect(decisionOf(training('Cancelled', { closedAt: '2026-09-20T11:00:00Z' }))).toEqual({
    kind: 'cancelled',
    at: '2026-09-20T11:00:00Z',
  });
});

test('the page of a training is under the list, by its identifier', () => {
  expect(staffTrainingHref(41)).toBe('/staff/training/41');
});

test('a conflict is the server saying somebody else moved the training, and no other refusal', () => {
  expect(isConflict(new ApiError(409, { title: 'Conflict' }))).toBe(true);
  expect(
    isConflict(
      new ApiError(400, { title: 'Refused', errors: { trainerVid: ['training:errors.trainerAlready'] } }),
    ),
  ).toBe(false);
  expect(isConflict(new ApiError(403, { title: 'Forbidden' }))).toBe(false);
  expect(isConflict(new Error('offline'))).toBe(false);
});
