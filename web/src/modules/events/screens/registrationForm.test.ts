import { expect, test } from 'vitest';

import { readFields } from '../../../shared/forms';
import type { MyRegistrationDto, PublicQuestionDto } from '../api';

import { registrationDefaults, registrationSchema } from './registrationForm';

/**
 * The member's form of an event in person (M4, E16) is built from the event's questions: one field per question, of the kind the
 * generator draws for its type, under `answers.q{id}` — the field the server files a refusal under —, starting from the member's
 * answers or from nothing answered.
 */

const read = (text: Record<string, string>) => text.en ?? '';

const questions: PublicQuestionDto[] = [
  { id: 11, type: 'YesNo', label: { en: 'Dinner' }, choices: [], required: true },
  {
    id: 12,
    type: 'Choice',
    label: { en: 'Seat' },
    choices: [
      { key: '1', label: { en: 'Window' } },
      { key: '2', label: { en: 'Aisle' } },
    ],
    required: true,
  },
  {
    id: 13,
    type: 'MultiChoice',
    label: { en: 'Equipment' },
    choices: [
      { key: '1', label: { en: 'PC' } },
      { key: '3', label: { en: 'Monitor' } },
    ],
    required: false,
  },
  { id: 14, type: 'Quantity', label: { en: 'Chairs' }, choices: [], required: false },
  { id: 15, type: 'Text', label: { en: 'Anything else' }, choices: [], required: false },
];

test('each question is a field of the kind its type draws, by its answer key, with its choices in words', () => {
  const [answers] = readFields(registrationSchema(questions, read));

  expect(answers?.kind).toBe('object');
  const fields = answers?.kind === 'object' ? answers.children : [];

  expect(fields.map((field) => [field.path, field.kind])).toEqual([
    ['answers.q11', 'boolean'],
    ['answers.q12', 'text'],
    ['answers.q13', 'multi'],
    ['answers.q14', 'number'],
    ['answers.q15', 'text'],
  ]);
  expect(fields[1]?.kind === 'text' ? fields[1].choices : null).toEqual([
    { value: '1', label: 'Window' },
    { value: '2', label: 'Aisle' },
  ]);
  expect(fields[2]?.kind === 'multi' ? fields[2].options : null).toEqual([
    { value: '1', label: 'PC' },
    { value: '3', label: 'Monitor' },
  ]);
  expect(fields[4]?.meta.multiline).toBe(true);
});

test('a form with nothing answered passes its schema: every rule is the server’s', () => {
  const schema = registrationSchema(questions, read);
  const blank = registrationDefaults(questions, undefined);

  expect(blank).toEqual({ answers: { q11: false, q12: '', q13: [], q15: '' } });
  expect(schema.safeParse(blank).success).toBe(true);
});

test('a member registered finds their answers, without the ones to questions the event no longer has', () => {
  const registration = {
    answers: { q11: true, q12: '2', q13: ['3'], q14: 4, q15: 'vegetarian', q99: 'gone' },
  } as unknown as MyRegistrationDto;

  expect(registrationDefaults(questions, registration)).toEqual({
    answers: { q11: true, q12: '2', q13: ['3'], q14: 4, q15: 'vegetarian' },
  });
});
