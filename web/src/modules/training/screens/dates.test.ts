import { expect, test } from 'vitest';

import { ApiError } from '../../../shared/api/problem';
import {
  shownState,
  type DateConflictsDto,
  type DateWarning,
  type StaffTrainingDto,
  type TraineeTrainingDto,
  type TrainingState,
} from '../api';
import { proposalFromFormValues } from '../schemas';

import {
  asksConfirmation,
  choosableSlots,
  closingOf,
  dateSteps,
  isHubAddress,
  isRefused,
  isWhole,
  sameDates,
  spanText,
  warningSays,
  whatTheyMeet,
  type FormatMoment,
} from './dates';
import { splitRefusal } from './trainee';

/**
 * What the pages of the dates read out of what the server answered (A8b): the dates a trainee is offered, how a span is said,
 * what a set of dates meets and when that asks for a confirmation, what a warning says, what the staff may do, and who closed a
 * training. Every rule is the server's (A8a); the ratings here are made up — a test may build its own (design M3 §10).
 */

function trainee(state: TrainingState, overrides: Partial<TraineeTrainingDto> = {}): TraineeTrainingDto {
  return {
    id: 41,
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

function warning(overrides: Partial<DateWarning>): DateWarning {
  return {
    kind: 'Training',
    startsAtUtc: '2026-10-03T10:00:00Z',
    endsAtUtc: null,
    trainingId: null,
    trainingKind: null,
    ratingShortName: null,
    position: null,
    calendarKind: null,
    title: null,
    url: null,
    ...overrides,
  };
}

/** `useMoment` as the pages have it, in English, so that a span reads as it would on screen: twenty four hours, UTC unless named. */
const moment: FormatMoment = (value, options = {}) =>
  new Intl.DateTimeFormat('en', {
    ...(options.date === false ? {} : { dateStyle: 'medium' }),
    ...(options.time === false ? {} : { timeStyle: 'short', hour12: false }),
    timeZone: options.timeZone ?? 'UTC',
  }).format(new Date(value));

const NOW = Date.parse('2026-10-01T12:00:00Z');

test('a trainee is offered the dates still to come while the training waits for its date, the soonest first', () => {
  const slots = [
    { id: 3, startsAtUtc: '2026-10-05T16:00:00Z', endsAtUtc: '2026-10-05T18:00:00Z' },
    { id: 1, startsAtUtc: '2026-09-30T16:00:00Z', endsAtUtc: '2026-09-30T18:00:00Z' },
    { id: 2, startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: '2026-10-03T18:00:00Z' },
  ];

  expect(choosableSlots(trainee('Assigned', { slots }), NOW).map((slot) => slot.id)).toEqual([2, 3]);
  // Dated or closed, there is nothing left to choose, whatever a stale answer still holds.
  expect(choosableSlots(trainee('Scheduled', { slots }), NOW)).toEqual([]);
  expect(choosableSlots(trainee('Closed', { slots }), NOW)).toEqual([]);
});

test('a span says its day once when it starts and ends on it, and both moments when it does not, in the zone asked', () => {
  expect(spanText('2026-10-03T16:00:00Z', '2026-10-03T18:00:00Z', moment)).toBe('Oct 3, 2026, 16:00–18:00');
  // Where the division lives, the same two hours.
  expect(spanText('2026-10-03T16:00:00Z', '2026-10-03T18:00:00Z', moment, 'Europe/Rome')).toBe(
    'Oct 3, 2026, 18:00–20:00',
  );
  // Across the midnight of UTC, two days; where the division lives it may be one.
  expect(spanText('2026-10-03T23:00:00Z', '2026-10-04T01:00:00Z', moment)).toBe(
    'Oct 3, 2026, 23:00 – Oct 4, 2026, 01:00',
  );
  expect(spanText('2026-10-03T23:00:00Z', '2026-10-04T01:00:00Z', moment, 'Europe/Rome')).toBe(
    'Oct 4, 2026, 01:00–03:00',
  );
  // A session has a start only.
  expect(spanText('2026-10-03T16:00:00Z', null, moment)).toBe('Oct 3, 2026, 16:00');
});

test('what the dates meet is asked only of whole dates, and a confirmation is of the very dates asked about', async () => {
  const asked: string[] = [];
  const ask = (start: string, end: string | null): Promise<DateConflictsDto> => {
    asked.push(`${start}|${end ?? ''}`);
    return Promise.resolve({
      policy: 'Warn',
      warnings: start.startsWith('2026-10-03') ? [warning({ trainingId: 7 })] : [],
    });
  };

  const dates = [
    { startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: '2026-10-03T18:00:00Z' },
    { startsAtUtc: '2026-10-04T16:00:00Z', endsAtUtc: '2026-10-04T18:00:00Z' },
  ];
  const met = await whatTheyMeet(dates, ask);

  expect(asked).toEqual([
    '2026-10-03T16:00:00Z|2026-10-03T18:00:00Z',
    '2026-10-04T16:00:00Z|2026-10-04T18:00:00Z',
  ]);
  expect(met.policy).toBe('Warn');
  expect(met.warnings.map((found) => found.length)).toEqual([1, 0]);
  expect(asksConfirmation(met)).toBe(true);
  expect(isRefused(met)).toBe(false);

  // The same dates, written again, are the ones confirmed; a date moved is not.
  expect(
    sameDates(
      met.dates,
      dates.map((date) => ({ ...date })),
    ),
  ).toBe(true);
  expect(sameDates(met.dates, [dates[0]!, { ...dates[1]!, endsAtUtc: '2026-10-04T19:00:00Z' }])).toBe(false);
  expect(sameDates(met.dates, [dates[0]!])).toBe(false);

  // A box left empty is not asked about: the server says what is missing. The date set by hand has a start only, and is whole.
  expect(isWhole({ startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: null })).toBe(false);
  expect(isWhole({ startsAtUtc: null, endsAtUtc: '2026-10-03T18:00:00Z' })).toBe(false);
  expect(isWhole({ startsAtUtc: '2026-10-03T16:00:00Z' })).toBe(true);
  asked.length = 0;
  const incomplete = await whatTheyMeet([{ startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: null }], ask);
  expect(asked).toEqual([]);
  expect(incomplete.policy).toBeNull();
  expect(asksConfirmation(incomplete)).toBe(false);
});

test('Block with something found is a refusal; Warn with nothing found and None ask for nothing', async () => {
  const answering = (answer: DateConflictsDto) => () => Promise.resolve(answer);
  const date = [{ startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: '2026-10-03T18:00:00Z' }];

  const blocked = await whatTheyMeet(date, answering({ policy: 'Block', warnings: [warning({})] }));
  expect(isRefused(blocked)).toBe(true);
  expect(asksConfirmation(blocked)).toBe(false);

  const clear = await whatTheyMeet(date, answering({ policy: 'Warn', warnings: [] }));
  expect(asksConfirmation(clear)).toBe(false);
  expect(isRefused(clear)).toBe(false);

  const unchecked = await whatTheyMeet(date, answering({ policy: 'None', warnings: [] }));
  expect(asksConfirmation(unchecked)).toBe(false);
  expect(isRefused(unchecked)).toBe(false);
});

test('a warning says another training by what the public calendar shows of it, and an entry by its title and address', () => {
  const read = (value: Record<string, string> | null) => value?.en ?? '';

  expect(
    warningSays(
      warning({ trainingId: 7, trainingKind: 'Pilot', ratingShortName: 'P1', position: null }),
      read,
    ),
  ).toEqual({
    about: 'training',
    ladder: 'Pilot',
    what: 'P1',
    startsAtUtc: '2026-10-03T10:00:00Z',
    href: '/staff/training/7',
  });
  expect(
    warningSays(
      warning({ trainingId: 8, trainingKind: 'Atc', ratingShortName: 'R3', position: 'XXAA_TWR' }),
      read,
    ).what,
  ).toBe('R3 · XXAA_TWR');

  expect(
    warningSays(
      warning({
        kind: 'Calendar',
        startsAtUtc: '2026-10-03T19:00:00Z',
        endsAtUtc: '2026-10-03T21:00:00Z',
        calendarKind: 'event',
        title: { en: 'Evening on the radio', it: 'Serata in radio' },
        url: '/events/evening',
      }),
      read,
    ),
  ).toEqual({
    about: 'calendar',
    calendarKind: 'event',
    what: 'Evening on the radio',
    startsAtUtc: '2026-10-03T19:00:00Z',
    endsAtUtc: '2026-10-03T21:00:00Z',
    href: '/events/evening',
  });

  // The page follows an address of the hub, and opens another site's beside it.
  expect(isHubAddress('/events/evening')).toBe(true);
  expect(isHubAddress('https://example.org/evening')).toBe(false);
  expect(isHubAddress('//example.org/evening')).toBe(false);
});

test('whoever conducts proposes while the training waits for its date, and sets it by hand then and once it has one', () => {
  const conduct = {
    canDecide: false,
    canAssign: false,
    canConduct: true,
    canClose: false,
    canRecordOutcome: false,
  };

  expect(dateSteps(staff('Assigned', { actions: conduct }))).toEqual({ propose: true, setByHand: true });
  expect(dateSteps(staff('Scheduled', { actions: conduct }))).toEqual({ propose: false, setByHand: true });
  // The server's «no» is the page's.
  expect(dateSteps(staff('Assigned'))).toEqual({ propose: false, setByHand: false });
});

test('a closing is the staff’s with its reason, or the hub’s with none, and a dated training shows as held from the day after', () => {
  expect(
    closingOf(trainee('Closed', { closedAt: '2026-10-02T04:15:00Z', closeReason: 'No answer.' })),
  ).toEqual({ by: 'staff', at: '2026-10-02T04:15:00Z', reason: 'No answer.' });
  expect(closingOf(trainee('Closed', { closedAt: '2026-10-02T04:15:00Z' }))).toEqual({
    by: 'hub',
    at: '2026-10-02T04:15:00Z',
  });
  expect(closingOf(trainee('Cancelled', { closedAt: '2026-10-02T04:15:00Z' }))).toBeNull();

  expect(shownState(trainee('Scheduled', { held: true }))).toBe('Held');
  expect(shownState(trainee('Scheduled'))).toBe('Scheduled');
  // Only a dated training is held; the server says `held` of nothing else.
  expect(shownState(trainee('Completed', { held: true }))).toBe('Completed');
});

test('the staff’s answer tells the two closings by who closed it, so the staff’s stays theirs once an erasure took its reason', () => {
  const coordinator = { vid: 790097, name: 'Test Coordinator' };
  const closedAt = '2026-10-02T04:15:00Z';

  expect(closingOf(staff('Closed', { closedAt, closedBy: coordinator, closeReason: 'No answer.' }))).toEqual({
    by: 'staff',
    at: closedAt,
    reason: 'No answer.',
  });
  // The trainee's data erased (A12b): the reason is gone with it, and somebody of the staff still closed it — not the hub.
  expect(closingOf(staff('Closed', { closedAt, closedBy: coordinator }))).toEqual({
    by: 'staff',
    at: closedAt,
    reason: null,
  });
  // Nobody closed the hub's.
  expect(closingOf(staff('Closed', { closedAt }))).toEqual({ by: 'hub', at: closedAt });
});

test('the dates of the form travel with a box left empty as nothing, and a refusal on a row lands on the form', () => {
  expect(
    proposalFromFormValues({
      slots: [{ startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: '2026-10-03T18:00:00Z' }, {}],
    }),
  ).toEqual([
    { startsAtUtc: '2026-10-03T16:00:00Z', endsAtUtc: '2026-10-03T18:00:00Z' },
    { startsAtUtc: null, endsAtUtc: null },
  ]);

  const refusal = new ApiError(400, {
    title: 'One or more validation errors occurred.',
    status: 400,
    errors: {
      'slots[1].startsAtUtc': ['errors.required'],
      slots: ['training:errors.slotsTooMany'],
      confirmed: ['training:errors.dateNotConfirmed'],
    },
  });
  const { form, page } = splitRefusal(refusal, (field) => /^slots\[\d+\]\./.test(field));

  expect(form instanceof ApiError ? form.problem?.errors : null).toEqual({
    'slots[1].startsAtUtc': ['errors.required'],
  });
  expect(page?.problem?.errors).toEqual({
    slots: ['training:errors.slotsTooMany'],
    confirmed: ['training:errors.dateNotConfirmed'],
  });
});
