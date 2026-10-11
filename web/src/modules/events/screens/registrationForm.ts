import { z } from 'zod';

import type { ChoiceOption } from '../../../shared/forms';
import type { MyRegistrationDto, PublicQuestionDto } from '../api';

/**
 * The member's form of an event in person (design M4 §4-bis: «the `SchemaForm` of always, with the schema built from the rows», E16):
 * one field per question, in the order of the event, each a field the generator already draws — a switch for a yes or no, a select
 * for one choice, a box per choice for several, a number, a text. The choices are offered with their words in the language on screen,
 * their key as the value. Every rule is the server's: a required question, the type of each answer (`RegistrationAnswers`). The answers
 * sit under `answers`, by `q{id}` — the field the server files a refusal under.
 */

/** The name of the answer to a question, as the server spells it (`EventQuestion.AnswerKey`): never a bare number, which a form reads as an index. */
export function answerKey(questionId: number): string {
  return `q${questionId}`;
}

export function registrationSchema(
  questions: readonly PublicQuestionDto[],
  read: (text: Record<string, string>) => string,
) {
  return z.object({
    answers: z.object(
      Object.fromEntries(questions.map((question) => [answerKey(question.id), field(question, read)])),
    ),
  });
}

export type RegistrationValues = { answers: Record<string, unknown> };

function field(question: PublicQuestionDto, read: (text: Record<string, string>) => string) {
  const choices: ChoiceOption[] = question.choices.map((choice) => ({
    value: choice.key,
    label: read(choice.label),
  }));

  switch (question.type) {
    case 'YesNo':
      return z.boolean();
    case 'Choice':
      return z.string().meta({ choices });
    case 'MultiChoice':
      return z.array(z.string()).meta({ multi: true, choices });
    case 'Quantity':
      return z.number().int().optional();
    default:
      return z.string().meta({ multiline: true });
  }
}

/**
 * What the form starts with: the member's answers when they are registered — an answer to a question the event no longer has left
 * out —, or nothing answered: a yes or no off, no choice, no number, no text.
 */
export function registrationDefaults(
  questions: readonly PublicQuestionDto[],
  registration: MyRegistrationDto | undefined,
): RegistrationValues {
  const given = (registration?.answers ?? {}) as Record<string, unknown>;
  const answers: Record<string, unknown> = {};

  for (const question of questions) {
    const key = answerKey(question.id);
    const value = given[key];

    switch (question.type) {
      case 'YesNo':
        answers[key] = value === true;
        break;
      case 'MultiChoice':
        answers[key] = Array.isArray(value) ? value.filter((item) => typeof item === 'string') : [];
        break;
      case 'Quantity':
        // No number is no key: an empty box, not a zero nobody wrote.
        if (typeof value === 'number') {
          answers[key] = value;
        }
        break;
      default:
        answers[key] = typeof value === 'string' ? value : '';
    }
  }

  return { answers };
}
