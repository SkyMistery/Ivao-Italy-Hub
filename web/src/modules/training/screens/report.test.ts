import { expect, test } from 'vitest';

import { ApiError } from '../../../shared/api/problem';
import type {
  MyTrainingPathDto,
  StaffEvaluationDto,
  StaffSessionDto,
  StaffTrainingDto,
  TrainingState,
} from '../api';

import {
  NOT_APPLICABLE,
  asksRereading,
  choicesOf,
  evaluationSays,
  nextOnTheLadder,
  publishedBy,
  recordsOutcome,
  refusalOn,
  sheetEntries,
  splitReportRefusal,
} from './report';

/**
 * What the pages after the session read out of what the server answered (A9b): whether the reader records the session, what a
 * row of the sheet offers, the sheet as the report sends it, how an evaluation reads, where a refusal of the report belongs, who
 * published it, and what the trainee may do next. Every rule is the server's (A9a); the ratings here are made up — a test may
 * build its own (design M3 §10).
 */

function staff(state: TrainingState, overrides: Partial<StaffTrainingDto> = {}): StaffTrainingDto {
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

function item(itemId: number, section: StaffEvaluationDto['section']): StaffEvaluationDto {
  return {
    itemId,
    section,
    title: { en: `Item ${String(itemId)}`, it: `Voce ${String(itemId)}` },
    grade: null,
    mark: null,
    traineeComment: null,
    staffNote: null,
  };
}

function path(overrides: Partial<MyTrainingPathDto> = {}): MyTrainingPathDto {
  return {
    kind: 'Pilot',
    ratingShortName: 'R2',
    hours: 150,
    next: { kind: 'Pilot', number: 3, shortName: 'R3', nameKey: 'ratings.Pilot.R3' },
    isMockExam: false,
    asksPosition: false,
    positions: [],
    refusal: null,
    bannedUntil: null,
    openTrainingId: null,
    waitUntil: null,
    minimumHours: null,
    ...overrides,
  };
}

const refused = (errors: Record<string, string[]>, status = 400) =>
  new ApiError(status, { title: 'One or more validation errors occurred.', status, errors });

test('the session is recorded on a dated training, when the server says the reader may now', () => {
  const recordable = { ...staff('Scheduled').actions, canRecordOutcome: true };

  expect(recordsOutcome(staff('Scheduled', { actions: recordable }))).toBe(true);
  // Not started yet, or not the reader's to conduct: the server said no.
  expect(recordsOutcome(staff('Scheduled'))).toBe(false);
  // Once it came to something, it is not recorded again.
  expect(recordsOutcome(staff('Completed', { actions: recordable }))).toBe(false);
});

test('a row offers «N/A» first, then the grades of practice or the marks of theory', () => {
  expect(choicesOf('Practice')).toEqual([NOT_APPLICABLE, '1', '2', '3', '4', '5']);
  expect(choicesOf('Theory')).toEqual([NOT_APPLICABLE, 'Done', 'NotDone', 'ToImprove']);
});

test('the report sends every row in the order of the page, a row nobody wrote in as not applicable', () => {
  const sheet = [item(7, 'Practice'), item(8, 'Theory'), item(9, 'Practice'), item(10, 'Theory')];

  const entries = sheetEntries(sheet, {
    7: { choice: '4', traineeComment: ' Clear readbacks. ', staffNote: '' },
    8: { choice: 'ToImprove', traineeComment: '', staffNote: ' Review the airspace classes. ' },
    // Written in and then set back to «N/A»: its comment still goes, with nothing marked.
    10: { choice: NOT_APPLICABLE, traineeComment: 'Not covered today.', staffNote: '   ' },
  });

  expect(entries).toEqual([
    { itemId: 7, grade: 4, mark: null, traineeComment: 'Clear readbacks.', staffNote: null },
    {
      itemId: 8,
      grade: null,
      mark: 'ToImprove',
      traineeComment: null,
      staffNote: 'Review the airspace classes.',
    },
    { itemId: 9, grade: null, mark: null, traineeComment: null, staffNote: null },
    { itemId: 10, grade: null, mark: null, traineeComment: 'Not covered today.', staffNote: null },
  ]);
});

test('a choice goes as what its section marks with, and never as the other', () => {
  // A mark on a row of practice, or a grade on one of theory, would be the server's to refuse: the page never sends one.
  const [practice, theory] = sheetEntries([item(1, 'Practice'), item(2, 'Theory')], {
    1: { choice: 'Done', traineeComment: '', staffNote: '' },
    2: { choice: '3', traineeComment: '', staffNote: '' },
  });

  expect(practice?.mark).toBeNull();
  expect(theory?.grade).toBeNull();
});

test('an evaluation reads as its grade, its mark, or not applicable', () => {
  expect(evaluationSays({ section: 'Practice', grade: 5, mark: null })).toEqual({ kind: 'grade', grade: 5 });
  expect(evaluationSays({ section: 'Practice', grade: null, mark: null })).toEqual({ kind: 'notApplicable' });
  expect(evaluationSays({ section: 'Theory', grade: null, mark: 'NotDone' })).toEqual({
    kind: 'mark',
    mark: 'NotDone',
  });
  expect(evaluationSays({ section: 'Theory', grade: null, mark: null })).toEqual({ kind: 'notApplicable' });
});

test('a refusal of the report lands on the form, on the rows of the sheet, or above the form', () => {
  const { form, sheet, page } = splitReportRefusal(
    refused({
      staffComment: ['errors.text.tooLong'],
      'sheet[1].mark': ['training:errors.evaluationMarkOnPractice'],
      'sheet[2].traineeComment': ['errors.text.tooLong'],
      sheet: ['training:errors.sheetChanged'],
      state: ['training:errors.sessionNotRecordable'],
    }),
  );

  expect(form).toBeInstanceOf(ApiError);
  expect((form as ApiError).problem?.errors).toEqual({ staffComment: ['errors.text.tooLong'] });
  expect(sheet?.problem?.errors).toEqual({
    'sheet[1].mark': ['training:errors.evaluationMarkOnPractice'],
    'sheet[2].traineeComment': ['errors.text.tooLong'],
  });
  expect(page?.problem?.errors).toEqual({
    sheet: ['training:errors.sheetChanged'],
    state: ['training:errors.sessionNotRecordable'],
  });

  // A refusal with no field — somebody else moved the training — is the form's, whose banner says it.
  const conflict = new ApiError(409, { title: 'Conflict', status: 409 });
  expect(splitReportRefusal(conflict)).toEqual({ form: conflict, sheet: null, page: null });
});

test('the refusal of one control of a row is that field alone', () => {
  const error = refused({
    'sheet[0].grade': ['errors.number.range'],
    'sheet[0].staffNote': ['errors.text.tooLong'],
  });

  expect(refusalOn(error, 'sheet[0].grade')?.problem?.errors).toEqual({
    'sheet[0].grade': ['errors.number.range'],
  });
  expect(refusalOn(error, 'sheet[0].traineeComment')).toBeNull();
  expect(refusalOn(null, 'sheet[0].grade')).toBeNull();
});

test('the page is read again when somebody else moved the training, or its sheet changed meanwhile', () => {
  expect(asksRereading(new ApiError(409, { title: 'Conflict', status: 409 }))).toBe(true);
  expect(asksRereading(refused({ sheet: ['training:errors.sheetChanged'] }))).toBe(true);
  expect(asksRereading(refused({ 'sheet[0].grade': ['errors.number.range'] }))).toBe(false);
  expect(asksRereading(new Error('offline'))).toBe(false);
});

test('the report was published by whoever recorded the session held', () => {
  const session = (outcome: StaffSessionDto['outcome'], vid: number, at: string): StaffSessionDto => ({
    id: vid,
    startsAtUtc: '2026-09-25T16:00:00Z',
    outcome,
    internalNotes: null,
    recordedBy: { vid, name: 'Test Trainer' },
    recordedAt: at,
  });

  expect(publishedBy([])).toBeNull();
  expect(publishedBy([session('Rescheduled', 790098, '2026-09-20T18:00:00Z')])).toBeNull();
  expect(
    publishedBy([
      session('Rescheduled', 790098, '2026-09-20T18:00:00Z'),
      session('Held', 790097, '2026-09-25T18:30:00Z'),
    ]),
  ).toEqual({ by: { vid: 790097, name: 'Test Trainer' }, at: '2026-09-25T18:30:00Z' });
});

test('after a training its session ended, the trainee waits while the waiting runs, or may ask again', () => {
  expect(nextOnTheLadder(undefined)).toBeNull();
  expect(
    nextOnTheLadder(path({ refusal: 'training:errors.requestWaiting', waitUntil: '2026-10-02T18:30:00Z' })),
  ).toEqual({ kind: 'wait', until: '2026-10-02T18:30:00Z' });
  expect(nextOnTheLadder(path())).toEqual({ kind: 'ask', ladder: 'Pilot', mockExam: false });
  // The trainer's box: the next training on the rating is a mock exam, as the server says.
  expect(nextOnTheLadder(path({ isMockExam: true }))).toEqual({
    kind: 'ask',
    ladder: 'Pilot',
    mockExam: true,
  });
  // Anything else that refuses a request now is the trainings page's to say in full.
  expect(nextOnTheLadder(path({ refusal: 'training:errors.requestOpen', openTrainingId: 42 }))).toBeNull();
});
