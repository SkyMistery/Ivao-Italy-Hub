import { useTranslation } from 'react-i18next';

import type { PublicQuestionDto } from '../api';

import { answerKey } from './registrationForm';

/**
 * The answers of a registration and the sums of a question, in words of the language on screen (E16; Carmine's answer 6 on #255: a
 * page read in English never shows cells in another language). The server sends the data — the question, the value, the keys of the
 * choices, the counts — and these write it, for the staff's two lists and the member's own registrations; the mails keep the server's
 * words, in the language of whoever receives them. A value that does not fit its question — a type changed in an instant — is skipped,
 * as the server skips it (`RegistrationAnswers.Fits`).
 */

/** The words these need, in the language on screen: the page reads them from the module's language file. */
export interface AnswerWords {
  readonly yes: string;
  readonly no: string;
  readonly total: string;
  readonly answered: string;
}

/** A question as both lists carry it: its type, its words, its choices. */
type Question = Pick<PublicQuestionDto, 'id' | 'type' | 'label' | 'choices'>;

/** The sums of a question as the list of the questions carries them (`QuestionSum`). */
export interface Sum {
  readonly answered: number;
  readonly yes: number;
  readonly no: number;
  readonly total: number;
  readonly choices: Readonly<Record<string, number>>;
}

/** The words, in the language on screen. */
export function useAnswerWords(): AnswerWords {
  const { t } = useTranslation();

  return {
    yes: t('events:registrations.answers.yes'),
    no: t('events:registrations.answers.no'),
    total: t('events:registrations.sums.total'),
    answered: t('events:registrations.sums.answered'),
  };
}

type Read = (text: Record<string, string>) => string;

function choiceLabel(question: Question, key: string, read: Read): string {
  const choice = question.choices.find((candidate) => candidate.key === key);
  return choice === undefined ? key : read(choice.label) || key;
}

/** One answer in words, or null when it does not fit its question. */
function answerWords(question: Question, value: unknown, read: Read, words: AnswerWords): string | null {
  switch (question.type) {
    case 'YesNo':
      return typeof value === 'boolean' ? (value ? words.yes : words.no) : null;
    case 'Choice':
      return typeof value === 'string' ? choiceLabel(question, value, read) : null;
    case 'MultiChoice':
      return Array.isArray(value) && value.every((key) => typeof key === 'string')
        ? value.map((key) => choiceLabel(question, key, read)).join(', ')
        : null;
    case 'Quantity':
      return typeof value === 'number' && Number.isInteger(value) ? String(value) : null;
    default:
      return typeof value === 'string' ? value : null;
  }
}

/**
 * The answers in words, one line per question answered, in the order of the questions: «the question: the answer». An answer to a
 * question the event no longer has is not shown.
 */
export function answerLines(
  questions: readonly Question[],
  answers: Readonly<Record<string, unknown>>,
  read: Read,
  words: AnswerWords,
): string[] {
  return questions.flatMap((question) => {
    const said = answerWords(question, answers[answerKey(question.id)], read, words);
    return said === null ? [] : [`${read(question.label)}: ${said}`];
  });
}

/**
 * The sum of a question in words, one entry each: «Yes 12», «No 3»; «PC 4», «Monitor 6», in the order of the choices; «Total 23» of a
 * number; how many answered a text.
 */
export function sumWords(question: Question, sum: Sum, read: Read, words: AnswerWords): string[] {
  switch (question.type) {
    case 'YesNo':
      return [`${words.yes} ${sum.yes}`, `${words.no} ${sum.no}`];
    case 'Choice':
    case 'MultiChoice':
      return question.choices.map(
        (choice) => `${read(choice.label) || choice.key} ${sum.choices[choice.key] ?? 0}`,
      );
    case 'Quantity':
      return [`${words.total} ${sum.total}`];
    default:
      return [`${words.answered} ${sum.answered}`];
  }
}
