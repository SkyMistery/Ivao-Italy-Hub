import { expect, test } from 'vitest';

import type { PublicQuestionDto } from '../api';

import { answerLines, sumWords, type AnswerWords } from './answers';

/**
 * The answers and the sums of an event in person are written in words by the page, in the language on screen (M4, E16; Carmine's
 * answer 6 on #255): the server sends the values, the keys of the choices and the counts. A value that does not fit its question is
 * skipped, as the server skips it (the review of #255, point 3).
 */

const words: AnswerWords = { yes: 'Yes', no: 'No', total: 'Total', answered: 'Answered' };
const read = (text: Record<string, string>) => text.en ?? '';

const questions: PublicQuestionDto[] = [
  { id: 1, type: 'YesNo', label: { en: 'Dinner' }, choices: [], required: true },
  {
    id: 2,
    type: 'Choice',
    label: { en: 'Seat' },
    choices: [
      { key: '1', label: { en: 'Window' } },
      { key: '2', label: { en: 'Aisle' } },
    ],
    required: false,
  },
  {
    id: 3,
    type: 'MultiChoice',
    label: { en: 'Equipment' },
    choices: [
      { key: '1', label: { en: 'PC' } },
      { key: '3', label: { en: 'Monitor' } },
    ],
    required: false,
  },
  { id: 4, type: 'Quantity', label: { en: 'Chairs' }, choices: [], required: false },
  { id: 5, type: 'Text', label: { en: 'Anything else' }, choices: [], required: false },
];

test('the answers are one line per question answered, in the order of the questions, in the words on screen', () => {
  expect(
    answerLines(
      questions,
      {
        q5: 'vegetarian',
        q1: false,
        q3: ['1', '3'],
        q4: 2,
        q2: '2',
        q99: 'a question the event no longer has',
      },
      read,
      words,
    ),
  ).toEqual([
    'Dinner: No',
    'Seat: Aisle',
    'Equipment: PC, Monitor',
    'Chairs: 2',
    'Anything else: vegetarian',
  ]);
});

test('a value that does not fit its question is skipped, not thrown on', () => {
  expect(answerLines(questions, { q1: 'yes', q2: 4, q3: 'PC', q4: 2.5, q5: ['a'] }, read, words)).toEqual([]);
});

test('the sums of each kind of question, in the words on screen', () => {
  const sum = { answered: 3, yes: 2, no: 1, total: 7, choices: { '1': 2, '3': 1 } };

  expect(sumWords(questions[0]!, sum, read, words)).toEqual(['Yes 2', 'No 1']);
  expect(sumWords(questions[2]!, sum, read, words)).toEqual(['PC 2', 'Monitor 1']);
  expect(sumWords(questions[1]!, sum, read, words)).toEqual(['Window 2', 'Aisle 0']);
  expect(sumWords(questions[3]!, sum, read, words)).toEqual(['Total 7']);
  expect(sumWords(questions[4]!, sum, read, words)).toEqual(['Answered 3']);
});
