import { expect, test } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { TrainingHistoryEntryDto } from '../api';
import englishTraining from '../locales/en/training.json';

import type { FormatMoment } from './dates';
import { historySays } from './history';

/**
 * What a line of the history of a training says (A13b): the sentence of each step with who took it, the hub when nobody did, a deleted
 * person as one, the dates in UTC and the reason apart. What each step was is the server's reading of the audit log, which
 * `TrainingHistoryRulesTests` and `TrainingStaffTests.History` prove; here the words are the module's own, read from its language file,
 * so a sentence that names a value the line does not give shows.
 */

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'training', englishTraining);
const t = i18n.getFixedT('en', null);

/** `useMoment` as the pages have it, in English: twenty four hours, UTC unless named. */
const moment: FormatMoment = (value, options = {}) =>
  new Intl.DateTimeFormat('en', {
    ...(options.date === false ? {} : { dateStyle: 'medium' }),
    ...(options.time === false ? {} : { timeStyle: 'short', hour12: false }),
    timeZone: options.timeZone ?? 'UTC',
  }).format(new Date(value));

const advisor = { vid: 790023, name: 'Test Advisor' };
const trainee = { vid: 790022, name: 'Test Trainee' };
const first = { vid: 790025, name: 'First Trainer' };
const second = { vid: 790026, name: 'Second Trainer' };

function line(
  event: TrainingHistoryEntryDto['event'],
  overrides: Partial<TrainingHistoryEntryDto> = {},
): TrainingHistoryEntryDto {
  return {
    at: '2026-09-30T10:00:00Z',
    by: advisor,
    event,
    trainer: null,
    previousTrainer: null,
    date: null,
    previousDate: null,
    reason: null,
    ...overrides,
  };
}

const says = (entry: TrainingHistoryEntryDto) => historySays(entry, t, moment);

test('every step says what happened and who took it, the trainers and the dates it names in UTC', () => {
  expect(says(line('Requested', { by: trainee })).text).toBe('Test Trainee (790022) asked for the training.');
  expect(says(line('Accepted')).text).toBe('Test Advisor (790023) accepted the request.');
  expect(says(line('Assigned', { trainer: first })).text).toBe(
    'Test Advisor (790023) assigned the trainer: First Trainer (790025).',
  );
  expect(says(line('TrainerChanged', { previousTrainer: first, trainer: second })).text).toBe(
    'Test Advisor (790023) changed the trainer: First Trainer (790025) → Second Trainer (790026).',
  );
  expect(says(line('DatesChanged', { by: first })).text).toBe(
    'First Trainer (790025) changed the dates proposed.',
  );
  expect(says(line('DateChosen', { by: trainee, date: '2026-10-08T13:00:00Z' })).text).toBe(
    'Test Trainee (790022) chose the date among the ones proposed: Oct 8, 2026, 13:00 UTC.',
  );
  expect(says(line('DateSet', { by: second, date: '2026-10-12T17:00:00Z' })).text).toBe(
    'Second Trainer (790026) set the date by hand: Oct 12, 2026, 17:00 UTC.',
  );
  expect(
    says(
      line('DateMoved', { by: second, previousDate: '2026-10-08T13:00:00Z', date: '2026-10-09T18:30:00Z' }),
    ).text,
  ).toBe('Second Trainer (790026) moved the date: Oct 8, 2026, 13:00 UTC → Oct 9, 2026, 18:30 UTC.');
  expect(says(line('Rescheduled', { by: second, date: '2026-10-09T18:30:00Z' })).text).toBe(
    'Second Trainer (790026) rescheduled the session: Oct 9, 2026, 18:30 UTC.',
  );
  expect(says(line('NoShow', { by: second, date: '2026-10-09T18:30:00Z' })).text).toBe(
    'Second Trainer (790026) marked the session as a no-show: Oct 9, 2026, 18:30 UTC.',
  );
  expect(says(line('Completed', { by: second })).text).toBe('Second Trainer (790026) published the report.');
  expect(says(line('Cancelled', { by: trainee })).text).toBe('Test Trainee (790022) cancelled the request.');
});

test('a refusal and a closing of the staff say why on a line of their own; the hub closes with no reason, and nobody else', () => {
  expect(says(line('Rejected', { reason: 'More hours first.' }))).toEqual({
    text: 'Test Advisor (790023) refused the request.',
    reason: 'The reason: More hours first.',
  });
  expect(says(line('Closed', { reason: 'No answer to the dates.' }))).toEqual({
    text: 'Test Advisor (790023) closed the training.',
    reason: 'The reason: No answer to the dates.',
  });

  // Its reason gone with the data of an erased trainee (A12b): the closing is still the staff's.
  expect(says(line('Closed'))).toEqual({ text: 'Test Advisor (790023) closed the training.', reason: null });

  // Nobody closed it: the hub, because the trainee chose no date in time.
  expect(says(line('Closed', { by: null }))).toEqual({
    text: 'The hub closed the training: the trainee chose no date in the time the division gives to choose one.',
    reason: null,
  });

  // The theory not passed is the hub's refusal, whoever wrote the row.
  expect(says(line('RejectedForTheory', { by: null })).text).toBe(
    'The hub refused the request: the trainee answered that the theory exam was not passed yet.',
  );

  // Any other step written by nobody is the hub's too: the installation, on a bench.
  expect(
    says(line('DateMoved', { by: null, previousDate: '2026-10-08T13:00:00Z', date: '2026-09-30T09:50:00Z' }))
      .text,
  ).toBe('The hub moved the date: Oct 8, 2026, 13:00 UTC → Sep 30, 2026, 09:50 UTC.');
});

test('a deleted person is one, and a training an erasure emptied says what happened without what', () => {
  const erased = { vid: -3, name: null };

  expect(says(line('Requested', { by: erased })).text).toBe('Deleted person asked for the training.');
  expect(says(line('TrainerChanged', { previousTrainer: first, trainer: erased })).text).toBe(
    'Test Advisor (790023) changed the trainer: First Trainer (790025) → Deleted person.',
  );
  expect(says(line('Changed')).text).toBe(
    'Test Advisor (790023) changed the training: what changed is no longer on record.',
  );
  expect(says(line('Erased')).text).toBe(
    "Test Advisor (790023) erased the trainee's data: the training stays in the register, without its texts.",
  );
});
