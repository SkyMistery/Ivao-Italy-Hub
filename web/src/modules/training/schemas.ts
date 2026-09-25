import { z } from 'zod';

import type { components } from '../../shared/api/schema';
import type { ChoiceOption, Suggestion } from '../../shared/forms';

/**
 * The forms of the training's skeleton (M3, A4), as zod schemas: types and what is required. The rules — a rating the
 * division trains for, a position it trains on, the ranges — are the server's (design M0 §7.5).
 *
 * A rating is chosen, never typed: the choices are the ones the server offers from the core's vocabulary, so this module
 * writes no number of a rating (design M3 §1.7). A position is a **closed** suggestion for the same reason.
 */

export type RatingKind = components['schemas']['RatingKind'];

/** What happens when a proposed date meets the calendar (design M3 §2.5), as `ConflictPolicy` spells it. */
export const CONFLICT_POLICIES = ['Warn', 'Block', 'None'] as const;

/** The settings as the server keeps them (`TrainingSettings`, design M3 §1.6). */
export interface TrainingSettings {
  readonly minimumHours: readonly {
    readonly kind: RatingKind;
    readonly rating: number;
    readonly hours: number;
  }[];
  readonly cooldownDays: number;
  readonly noShowCooldownDays: number;
  readonly maxResponseDays: number | null;
  readonly responseReminderDays: number;
  readonly conflictPolicy: (typeof CONFLICT_POLICIES)[number];
  readonly conflictKinds: readonly string[];
  readonly reminderLeadHours: number;
  readonly hiddenPositions: readonly string[];
  readonly theoryExamUrl: string | null;
}

/** What the fields of the settings choose from, already in the language on screen. */
export interface SettingsChoices {
  readonly ratings: readonly ChoiceOption[];
  readonly kinds: readonly ChoiceOption[];
  readonly positions: readonly Suggestion[];
}

export function settingsSchema(choices: SettingsChoices) {
  return z.object({
    // A row is a rating and its hours; the ladder travels inside the value of the choice (`ratingChoice`).
    minimumHours: z.array(
      z.object({
        rating: z.string().min(1).meta({ choices: choices.ratings }),
        hours: z.number().int(),
      }),
    ),
    cooldownDays: z.number().int(),
    noShowCooldownDays: z.number().int(),
    // Empty: a training never closes by itself (§12 n.9).
    maxResponseDays: z.number().int().optional(),
    responseReminderDays: z.number().int(),
    conflictPolicy: z.enum(CONFLICT_POLICIES),
    conflictKinds: z.array(z.string()).meta({ multi: true, choices: choices.kinds }),
    reminderLeadHours: z.number().int(),
    hiddenPositions: z.array(
      z.object({
        callsign: z.string().min(1).meta({ suggestions: choices.positions, suggestionsOnly: true }),
      }),
    ),
    // Empty: no site of an exam (§12 n.12).
    theoryExamUrl: z.string(),
  });
}

export type SettingsFormValues = z.output<ReturnType<typeof settingsSchema>>;

/**
 * A rating as the value of a choice: its ladder and its number, which only together say which rating it is — the two
 * ladders number their rungs alike.
 */
export function ratingChoice(kind: RatingKind, rating: number): string {
  return `${kind}:${rating}`;
}

/**
 * The form's values. A kind of the calendar the division no longer has is left out, because the form draws no box to take
 * it off with; a position is kept, so that the server can say it is gone and somebody takes it out.
 */
export function settingsToFormValues(
  settings: TrainingSettings,
  knownKinds: readonly string[],
): SettingsFormValues {
  const { minimumHours, maxResponseDays, conflictKinds, hiddenPositions, theoryExamUrl, ...rest } = settings;

  return {
    ...rest,
    minimumHours: minimumHours.map(({ kind, rating, hours }) => ({
      rating: ratingChoice(kind, rating),
      hours,
    })),
    ...(maxResponseDays === null ? {} : { maxResponseDays }),
    conflictKinds: conflictKinds.filter((kind) => knownKinds.includes(kind)),
    hiddenPositions: hiddenPositions.map((callsign) => ({ callsign })),
    theoryExamUrl: theoryExamUrl ?? '',
  };
}

export function settingsFromFormValues(values: SettingsFormValues): TrainingSettings {
  const address = values.theoryExamUrl.trim();

  return {
    ...values,
    minimumHours: values.minimumHours.map(({ rating, hours }) => {
      const [kind, number] = rating.split(':');
      return { kind: kind as RatingKind, rating: Number(number), hours };
    }),
    maxResponseDays: values.maxResponseDays ?? null,
    hiddenPositions: values.hiddenPositions.map(({ callsign }) => callsign),
    theoryExamUrl: address === '' ? null : address,
  };
}
