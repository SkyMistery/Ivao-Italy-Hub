import { expect, test } from 'vitest';

import { readFields } from '../../shared/forms';

import {
  ratingChoice,
  settingsFromFormValues,
  settingsSchema,
  settingsToFormValues,
  type TrainingSettings,
} from './schemas';

/**
 * The form of the settings mirrors `TrainingSettings` (design M3 §1.6), which the core keeps as the module's own JSON and
 * the contract therefore does not describe: so the two are held together here, field by field and value by value. The
 * rating of a threshold is the one field that changes shape on the way, a choice carrying its ladder with its number.
 */

const saved: TrainingSettings = {
  minimumHours: [
    { kind: 'Atc', rating: 3, hours: 40 },
    { kind: 'Pilot', rating: 3, hours: 25 },
  ],
  cooldownDays: 5,
  noShowCooldownDays: 14,
  maxResponseDays: 10,
  responseReminderDays: 3,
  conflictPolicy: 'Block',
  conflictKinds: ['event', 'meeting'],
  reminderLeadHours: 24,
  hiddenPositions: ['XX_TWR'],
  theoryExamUrl: 'https://exam.example.org/theory',
};

const known = ['event', 'meeting'];

test('the form has a field for every setting, and nothing else', () => {
  const fields = readFields(settingsSchema({ ratings: [], kinds: [], positions: [] })).map(
    (field) => field.path,
  );

  expect(fields.sort()).toEqual(Object.keys(saved).sort());
});

test('the settings come back from the form as they went in', () => {
  expect(settingsFromFormValues(settingsToFormValues(saved, known))).toEqual(saved);
});

test('the same rung of the two ladders is two different choices', () => {
  const values = settingsToFormValues(saved, known);

  expect(values.minimumHours.map((row) => row.rating)).toEqual([
    ratingChoice('Atc', 3),
    ratingChoice('Pilot', 3),
  ]);
  expect(new Set(values.minimumHours.map((row) => row.rating)).size).toBe(2);
});

test('nothing chosen is sent as nothing: no time limit, and no site of the exam', () => {
  const values = settingsToFormValues({ ...saved, maxResponseDays: null, theoryExamUrl: null }, known);

  expect(values.maxResponseDays).toBeUndefined();
  expect(values.theoryExamUrl).toBe('');

  const back = settingsFromFormValues({ ...values, theoryExamUrl: '   ' });
  expect(back.maxResponseDays).toBeNull();
  expect(back.theoryExamUrl).toBeNull();
});

test('a kind the calendar no longer has is left out, because the form has no box to take it off with', () => {
  expect(settingsToFormValues(saved, ['event']).conflictKinds).toEqual(['event']);
});
