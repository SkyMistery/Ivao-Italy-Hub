import { expect, test } from 'vitest';

import { banStatus, type MyTrainingPathDto, type StaffTrainingDto, type TrainingState } from '../api';
import { banFromFormValues, emptyBan } from '../schemas';

import { BANS, banFormHref, ladderSays, traineeHref, trainingsByLadder } from './path';
import { REFUSALS, readyForExam } from './trainee';

/**
 * What the staff's pages of a trainee's path and of the bans read out of what the server answered (A10a): their addresses, what a
 * ladder says of the trainee, the trainings by ladder and rating, how a ban stands, and the ban as the form sends it. Where the
 * trainee stands, whether a ban holds and who may ban are the server's; the ratings here are made up — a test may build its own
 * (design M3 §10).
 */

const next = { kind: 'Atc' as const, number: 3, shortName: 'R3', nameKey: 'ratings.Atc.R3' };

function ladder(overrides: Partial<MyTrainingPathDto> = {}): MyTrainingPathDto {
  return {
    kind: 'Atc',
    ratingShortName: 'R2',
    hours: 120,
    next,
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
  overrides: Partial<StaffTrainingDto> = {},
): StaffTrainingDto {
  return {
    id,
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
    history: [],
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

test('the path of a trainee is under the page that asks for one, and a new ban carries the member it was opened for', () => {
  expect(traineeHref(790099)).toBe('/staff/training/trainees/790099');
  expect(BANS).toBe('/staff/training/bans');
  expect(banFormHref()).toBe('/staff/training/bans/new');
  expect(banFormHref(790099)).toBe('/staff/training/bans/new?vid=790099');
});

test('a person whose data was erased has no path to link to (A12b): the pseudonym in their place is nobody', () => {
  expect(traineeHref(-3)).toBeNull();
  expect(traineeHref(-790099)).toBeNull();
});

test('a ladder says what the trainee may ask for, or the first rule that refuses with what the answer says beside it', () => {
  expect(ladderSays(ladder())).toEqual({ kind: 'canAsk', next, isMockExam: false });
  expect(ladderSays(ladder({ isMockExam: true }))).toEqual({ kind: 'canAsk', next, isMockExam: true });

  // A ban: until when, or — without an end — until somebody lifts it.
  expect(ladderSays(ladder({ refusal: REFUSALS.banned, bannedUntil: '2026-12-01T00:00:00Z' }))).toEqual({
    kind: 'banned',
    until: '2026-12-01T00:00:00Z',
  });
  expect(ladderSays(ladder({ refusal: REFUSALS.banned }))).toEqual({ kind: 'banned', until: null });

  expect(ladderSays(ladder({ refusal: REFUSALS.open, openTrainingId: 12 }))).toEqual({
    kind: 'open',
    trainingId: 12,
  });
  expect(ladderSays(ladder({ refusal: REFUSALS.waiting, waitUntil: '2026-10-01T12:00:00Z' }))).toEqual({
    kind: 'waiting',
    until: '2026-10-01T12:00:00Z',
  });
  expect(ladderSays(ladder({ refusal: REFUSALS.hoursTooFew, minimumHours: 150, hours: 120 }))).toEqual({
    kind: 'hours',
    minimum: 150,
    hours: 120,
  });
  expect(ladderSays(ladder({ refusal: REFUSALS.hoursUnknown, minimumHours: 150, hours: null }))).toEqual({
    kind: 'hours',
    minimum: 150,
    hours: null,
  });
  expect(ladderSays(ladder({ refusal: REFUSALS.noPosition }))).toEqual({ kind: 'noPosition' });

  // Nothing after the trainee's rating: said the same, whether the server named the rule or proposed nothing.
  expect(ladderSays(ladder({ refusal: REFUSALS.nothingToAsk, next: null }))).toEqual({
    kind: 'nothingToAsk',
  });
  expect(ladderSays(ladder({ next: null }))).toEqual({ kind: 'nothingToAsk' });

  // A rule a later server adds is said as the trainee reads it, rather than not at all.
  expect(ladderSays(ladder({ refusal: 'training:errors.somethingNew' }))).toEqual({
    kind: 'other',
    refusal: 'training:errors.somethingNew',
  });
});

test('the trainings are by ladder, in the order of the ladders, and by rating where its newest training falls', () => {
  const pilot = training(5, 'Completed', { kind: 'Pilot', rating: 2, ratingShortName: 'P2', position: null });
  const newest = training(4, 'Requested', { rating: 4, ratingShortName: 'R4' });
  const mockExam = training(3, 'Completed', { isMockExam: true });
  const first = training(2, 'Completed');
  const refused = training(1, 'Rejected');

  const ladders = trainingsByLadder([pilot, newest, mockExam, first, refused]);

  expect(ladders.map((each) => each.kind)).toEqual(['Atc', 'Pilot']);
  expect(
    ladders[0]?.ratings.map((group) => [group.ratingShortName, group.trainings.map((each) => each.id)]),
  ).toEqual([
    ['R4', [4]],
    ['R3', [3, 2, 1]],
  ]);
  expect(ladders[1]?.ratings.map((group) => group.trainings.map((each) => each.id))).toEqual([[5]]);

  // A ladder with no training is left out, and so is everything with no training at all.
  expect(trainingsByLadder([pilot]).map((each) => each.kind)).toEqual(['Pilot']);
  expect(trainingsByLadder([])).toEqual([]);
});

test("ready for the exam is read off the staff's pages of the trainings as off the trainee's own", () => {
  const ready = training(2, 'Completed', { completedAt: '2026-09-20T12:00:00Z', readyForExam: true });

  expect(readyForExam(ladder(), [ready])).toBe(true);
  expect(
    readyForExam(ladder(), [training(3, 'Completed', { completedAt: '2026-09-25T12:00:00Z' }), ready]),
  ).toBe(false);
  expect(readyForExam(ladder({ kind: 'Pilot' }), [ready])).toBe(false);
});

test("a ban holds, is over, or was lifted: whether it holds is the server's", () => {
  expect(banStatus({ holds: true, liftedAt: null })).toBe('Holds');
  expect(banStatus({ holds: false, liftedAt: null })).toBe('Over');
  expect(banStatus({ holds: false, liftedAt: '2026-09-27T10:00:00Z' })).toBe('Lifted');
});

test('a ban is sent with the member, the reason written and the end, or none of them', () => {
  expect(emptyBan(undefined)).toEqual({ reason: '' });
  expect(emptyBan(790099)).toEqual({ vid: 790099, reason: '' });

  expect(
    banFromFormValues({ vid: 790099, reason: '  Repeated no-shows.  ', endsAt: '2026-12-01T00:00:00Z' }),
  ).toEqual({
    vid: 790099,
    reason: 'Repeated no-shows.',
    endsAt: '2026-12-01T00:00:00Z',
  });

  // Nothing written is nothing sent: the server says what is missing, on its field.
  expect(banFromFormValues({ reason: '   ' })).toEqual({ vid: 0, reason: null, endsAt: null });
  expect(banFromFormValues({ vid: 790099, reason: 'Why', endsAt: '' })).toEqual({
    vid: 790099,
    reason: 'Why',
    endsAt: null,
  });
});
