import { expect, test } from 'vitest';

import { ApiError } from '../../../shared/api/problem';
import type { MyTrainingDto, MyTrainingPathDto, TraineeTrainingDto, TrainingState } from '../api';

import {
  REFUSALS,
  chosenPath,
  daysUntil,
  formatHours,
  isCancellable,
  isTheoryRefusal,
  readyForExam,
  refusalDetail,
  splitRefusal,
  stateMoment,
} from './trainee';

/**
 * What the trainee's pages read out of `GET /api/training/mine` (A6b): the server decides every rule (A6a), and these only
 * say where its answer goes on the screen. The ratings here are made up — a test may build its own (design M3 §10) —, and
 * the refusals are the keys the server sends.
 */

function path(overrides: Partial<MyTrainingPathDto> = {}): MyTrainingPathDto {
  return {
    kind: 'Atc',
    ratingShortName: 'R2',
    hours: 120,
    next: { kind: 'Atc', number: 3, shortName: 'R3', nameKey: 'ratings.Atc.R3' },
    isMockExam: false,
    asksPosition: true,
    positions: [{ callsign: 'XXAA_TWR', name: 'Example Tower', ratingShortName: 'R3' }],
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
    position: 'XXAA_TWR',
    state,
    rejection: null,
    rejectionReason: null,
    availabilityText: null,
    notesText: null,
    requestedAt: '2026-09-01T10:00:00Z',
    decidedAt: null,
    scheduledStartUtc: null,
    completedAt: null,
    closedAt: null,
    readyForMockExam: false,
    readyForExam: false,
    rowVersion: '2026-09-01T10:00:00.000001Z',
    ...overrides,
  };
}

function mine(paths: MyTrainingPathDto[]): MyTrainingDto {
  return { vid: 790099, name: 'Test Trainee', asksTheory: true, theoryExamUrl: null, paths, trainings: [] };
}

const waiting = path({ refusal: REFUSALS.waiting, waitUntil: '2026-10-01T12:00:00Z' });
const pilot = path({ kind: 'Pilot', asksPosition: false, positions: [] });

test('the request is on the ladder the address chose, or else on the first one a request may be made on', () => {
  expect(chosenPath(mine([waiting, pilot]), 'Atc')?.kind).toBe('Atc');
  expect(chosenPath(mine([waiting, pilot]), undefined)?.kind).toBe('Pilot');

  // Nothing may be asked anywhere: the first ladder, whose refusal the page says.
  expect(chosenPath(mine([waiting, { ...pilot, refusal: REFUSALS.banned }]), undefined)?.kind).toBe('Atc');
  expect(chosenPath(mine([]), 'Atc')).toBeNull();
});

test('a refusal carries the detail its sentence cannot, from the same answer', () => {
  expect(refusalDetail(path({ refusal: REFUSALS.banned, bannedUntil: '2026-12-01T00:00:00Z' }))).toEqual({
    kind: 'bannedUntil',
    until: '2026-12-01T00:00:00Z',
  });
  // A ban with no end holds until somebody lifts it.
  expect(refusalDetail(path({ refusal: REFUSALS.banned }))).toEqual({ kind: 'bannedForever' });
  expect(refusalDetail(path({ refusal: REFUSALS.open, openTrainingId: 12 }))).toEqual({
    kind: 'open',
    trainingId: 12,
  });
  expect(refusalDetail(waiting)).toEqual({ kind: 'waitUntil', until: '2026-10-01T12:00:00Z' });
  expect(refusalDetail(path({ refusal: REFUSALS.hoursTooFew, minimumHours: 150, hours: 120 }))).toEqual({
    kind: 'hours',
    minimum: 150,
    hours: 120,
  });
  // Hours the network never said are not zero: the threshold alone.
  expect(refusalDetail(path({ refusal: REFUSALS.hoursUnknown, minimumHours: 150, hours: null }))).toEqual({
    kind: 'hours',
    minimum: 150,
    hours: null,
  });
});

test('a refusal whose sentence says it all has no detail, and a threshold met is no refusal', () => {
  expect(refusalDetail(path({ refusal: 'training:errors.requestNoPosition' }))).toBeNull();
  expect(refusalDetail(path({ refusal: 'training:errors.requestNothingToAsk', next: null }))).toBeNull();
  expect(refusalDetail(path({ minimumHours: 50 }))).toBeNull();
});

test('a refusal of the request lands on the form for its fields, and on the page for the request as a whole', () => {
  const refused = new ApiError(400, {
    title: 'One or more validation errors occurred.',
    errors: {
      kind: ['training:errors.requestOpen'],
      position: ['training:errors.requestPositionUnknown'],
      notesText: ['errors.text.tooLong'],
    },
  });

  const { form, page } = splitRefusal(refused, ['position', 'availabilityText', 'notesText']);

  expect(form).toBeInstanceOf(ApiError);
  expect((form as ApiError).problem?.errors).toEqual({
    position: ['training:errors.requestPositionUnknown'],
    notesText: ['errors.text.tooLong'],
  });
  expect(page?.problem?.errors).toEqual({ kind: ['training:errors.requestOpen'] });
  expect(page?.status).toBe(400);
});

test('a refusal on the rating alone is the page’s, and the form hears nothing', () => {
  const { form, page } = splitRefusal(
    new ApiError(400, { errors: { rating: ['training:errors.requestRatingNotNext'] } }),
    ['position'],
  );

  expect(form).toBeNull();
  expect(page?.problem?.errors).toEqual({ rating: ['training:errors.requestRatingNotNext'] });
});

test('what is not a refusal with fields is the form’s, whose banner says it', () => {
  const conflict = new ApiError(409, { title: 'Conflict' });
  expect(splitRefusal(conflict, ['position'])).toEqual({ form: conflict, page: null });

  const broken = new TypeError('Failed to fetch');
  expect(splitRefusal(broken, ['position'])).toEqual({ form: broken, page: null });
});

test('the hub’s own refusal is the theory not passed, and only a request nobody accepted is cancelled', () => {
  expect(isTheoryRefusal(training(1, 'Rejected', { rejection: 'TheoryNotPassed' }))).toBe(true);
  expect(isTheoryRefusal(training(2, 'Rejected', { rejection: 'Staff' }))).toBe(false);
  expect(isTheoryRefusal(training(3, 'Requested'))).toBe(false);

  expect(isCancellable(training(4, 'Requested'))).toBe(true);
  for (const state of ['Accepted', 'Assigned', 'Scheduled', 'Completed', 'Rejected', 'Cancelled'] as const) {
    expect(isCancellable(training(5, state))).toBe(false);
  }
});

test('each state says its own moment beside the request, and a waiting one says none', () => {
  const moments = {
    decidedAt: '2026-09-02T10:00:00Z',
    scheduledStartUtc: '2026-09-10T18:00:00Z',
    completedAt: '2026-09-10T20:00:00Z',
    closedAt: '2026-09-03T10:00:00Z',
  };

  expect(stateMoment(training(1, 'Requested', moments))).toBeNull();
  expect(stateMoment(training(1, 'Assigned', moments))).toBeNull();
  expect(stateMoment(training(1, 'Accepted', moments))).toBe(moments.decidedAt);
  expect(stateMoment(training(1, 'Rejected', moments))).toBe(moments.decidedAt);
  expect(stateMoment(training(1, 'Scheduled', moments))).toBe(moments.scheduledStartUtc);
  expect(stateMoment(training(1, 'Completed', moments))).toBe(moments.completedAt);
  expect(stateMoment(training(1, 'Cancelled', moments))).toBe(moments.closedAt);
  expect(stateMoment(training(1, 'Closed', moments))).toBe(moments.closedAt);
  expect(stateMoment(training(1, 'NoShow', moments))).toBe(moments.closedAt);
});

test('«ready for the exam» is the last report of the ladder, while its rating is still the one trained next', () => {
  const ready = training(7, 'Completed', { completedAt: '2026-09-10T20:00:00Z', readyForExam: true });
  const earlier = training(6, 'Completed', { completedAt: '2026-09-01T20:00:00Z', readyForExam: false });

  expect(readyForExam(path(), [earlier, ready])).toBe(true);

  // A later report without the box is the one that counts.
  const later = training(8, 'Completed', { completedAt: '2026-09-20T20:00:00Z', readyForExam: false });
  expect(readyForExam(path(), [ready, later])).toBe(false);

  // The exam passed, the rating moved on: the box is history.
  expect(
    readyForExam(path({ next: { kind: 'Atc', number: 4, shortName: 'R4', nameKey: 'ratings.Atc.R4' } }), [
      ready,
    ]),
  ).toBe(false);

  // The other ladder's report, and a training not completed, say nothing here.
  expect(readyForExam(pilot, [ready])).toBe(false);
  expect(readyForExam(path(), [training(9, 'Scheduled', { readyForExam: true })])).toBe(false);
});

test('the days left are counted up, and a wait ending today is still one day', () => {
  const now = Date.parse('2026-09-26T12:00:00Z');

  expect(daysUntil('2026-10-01T12:00:00Z', now)).toBe(5);
  expect(daysUntil('2026-10-01T12:00:01Z', now)).toBe(6);
  expect(daysUntil('2026-09-26T18:00:00Z', now)).toBe(1);
  expect(daysUntil('2026-09-26T11:00:00Z', now)).toBe(1);
});

test('hours are read to a tenth, and hours the network never said are none', () => {
  expect(formatHours(120, 'en')).toBe('120');
  expect(formatHours(120.25, 'en')).toBe('120.3');
  expect(formatHours(120.5, 'it')).toBe('120,5');
  expect(formatHours(null, 'en')).toBeNull();
});
