import { expect, test } from 'vitest';

import type { components } from '../../shared/api/schema';
import { readFields } from '../../shared/forms';

import { emptyExam, examFromFormValues, examSchema, examToFormValues, ratingChoice } from './schemas';

/**
 * The form of an exam (A10c): what it draws from the choices the server offers, and the exam as the server takes it back — the rating
 * with its ladder in the value of the choice, an empty box as none, the examiner by VID. The ratings, the positions and the people are
 * made up.
 */

type ExamDto = components['schemas']['ExamDto'];

const choices = {
  ratings: [{ value: ratingChoice('Atc', 12), label: 'ATC · A2 — Tower' }],
  positions: [{ value: 'XXAA_BOX', label: 'XXAA_BOX — Box', group: 'A2' }],
  examiners: [{ value: '100005', label: 'Sam Examiner (100005)' }],
};

test('the form draws the rating, the position, when, the candidate and the examiner, and keeps the version hidden', () => {
  const fields = readFields(examSchema(choices));

  expect(fields.map((field) => field.path)).toEqual([
    'rating',
    'position',
    'startsAtUtc',
    'candidateVid',
    'examinerVid',
    'rowVersion',
  ]);
});

test('a new exam has the reader as its examiner when they are offered, and nobody otherwise', () => {
  expect(emptyExam(100005).examinerVid).toBe('100005');
  expect(emptyExam(undefined).examinerVid).toBe('');
});

test('an exam goes to the server with its ladder, its rating, and none for an empty box', () => {
  const exam: ExamDto = {
    id: 7,
    kind: 'Atc',
    rating: 12,
    position: 'XXAA_BOX',
    startsAtUtc: '2026-10-02T18:00:00.000Z',
    candidateVid: 100003,
    examinerVid: 100005,
    rowVersion: '2026-09-28T18:00:00.000000Z',
  };

  expect(examFromFormValues(examToFormValues(exam))).toEqual({
    kind: 'Atc',
    rating: 12,
    position: 'XXAA_BOX',
    startsAtUtc: '2026-10-02T18:00:00.000Z',
    candidateVid: 100003,
    examinerVid: 100005,
    rowVersion: '2026-09-28T18:00:00.000000Z',
  });

  expect(
    examFromFormValues({ ...examToFormValues({ ...exam, kind: 'Pilot', position: null }), position: '  ' }),
  ).toMatchObject({ kind: 'Pilot', position: null });
  expect(examFromFormValues({ ...emptyExam(undefined), rating: ratingChoice('Pilot', 22) })).toMatchObject({
    startsAtUtc: null,
    candidateVid: 0,
    examinerVid: 0,
  });
});
