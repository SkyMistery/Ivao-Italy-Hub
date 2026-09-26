import { z } from 'zod';

import { NEW_ROW_VERSION } from '../../shared/api/rowVersion';
import type { components } from '../../shared/api/schema';
import { localized, type ChoiceOption, type Suggestion } from '../../shared/forms';
import { listSearchSchema } from '../../shared/list';

/**
 * The forms of the training (M3), as zod schemas: types and what is required. The rules — a rating the division trains for,
 * a position it trains on, every language of the division, the ranges — are the server's (design M0 §7.5).
 *
 * A rating is chosen, never typed: the choices are the ones the server offers from the core's vocabulary, so this module
 * writes no number of a rating (design M3 §1.7). A position is a **closed** suggestion for the same reason.
 */

export type RatingKind = components['schemas']['RatingKind'];

/** The ladders of the core's vocabulary, as `RatingKind` spells them. */
export const RATING_KINDS = ['Atc', 'Pilot'] as const satisfies readonly RatingKind[];

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

/** The ladder and the number a value of `ratingChoice` carries, as the server takes them. */
export function fromRatingChoice(choice: string): { kind: RatingKind; rating: number } {
  const [kind, number] = choice.split(':');
  return { kind: kind as RatingKind, rating: Number(number) };
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
    minimumHours: values.minimumHours.map(({ rating, hours }) => ({ ...fromRatingChoice(rating), hours })),
    maxResponseDays: values.maxResponseDays ?? null,
    hiddenPositions: values.hiddenPositions.map(({ callsign }) => callsign),
    theoryExamUrl: address === '' ? null : address,
  };
}

// ---- the evaluation sheet (A5) ------------------------------------------------------------------------------------------

type SheetItemDto = components['schemas']['SheetItemDto'];
type SheetItemWriteDto = components['schemas']['SheetItemWriteDto'];

/** Where an item of the sheet sits, as `SheetSection` spells it: it says how the trainer marks it (design M3 §1.4). */
export const SHEET_SECTIONS = ['Practice', 'Theory'] as const satisfies readonly SheetItemDto['section'][];

/**
 * An item of the evaluation sheet (design M3 §1.4): the rating whose sheet it is on — chosen, with its ladder in the value of
 * the choice —, its section, its title in every language of the division, its place in the sheet, and whether new reports
 * mark it.
 */
export function sheetItemSchema(ratings: readonly ChoiceOption[]) {
  return z.object({
    rating: z.string().min(1).meta({ choices: ratings }),
    section: z.enum(SHEET_SECTIONS),
    title: localized(),
    sort: z.number().int(),
    isActive: z.boolean(),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type SheetItemFormValues = z.output<ReturnType<typeof sheetItemSchema>>;

/** A new item on the sheet of that rating, if one is known, in that place: practice, and marked by new reports. */
export function emptySheetItem(
  rating: string,
  sort: number,
  locales: readonly string[],
): SheetItemFormValues {
  return {
    rating,
    section: 'Practice',
    title: Object.fromEntries(locales.map((locale) => [locale, ''])),
    sort,
    isActive: true,
    rowVersion: NEW_ROW_VERSION,
  };
}

export function sheetItemToFormValues(item: SheetItemDto, locales: readonly string[]): SheetItemFormValues {
  return {
    rating: ratingChoice(item.kind, item.rating),
    section: item.section,
    title: Object.fromEntries(locales.map((locale) => [locale, item.title[locale] ?? ''])),
    sort: item.sort,
    isActive: item.isActive,
    rowVersion: item.rowVersion,
  };
}

export function sheetItemFromFormValues(values: SheetItemFormValues): SheetItemWriteDto {
  return {
    ...fromRatingChoice(values.rating),
    section: values.section,
    title: values.title,
    sort: values.sort,
    isActive: values.isActive,
    rowVersion: values.rowVersion,
  };
}

/** `/staff/training/sheets`: the five of every list, and the ladder and the rating whose sheet it is narrowed to. */
export const sheetItemsSearchSchema = listSearchSchema.extend({
  kind: z.enum(RATING_KINDS).optional(),
  rating: z.coerce.number().int().optional(),
});

export type SheetItemsSearch = z.output<typeof sheetItemsSearchSchema>;

/** `/staff/training/sheets/new`: the sheet a new item starts on, the one the list was narrowed to. */
export const sheetItemFormSearchSchema = z.object({
  kind: z.enum(RATING_KINDS).optional(),
  rating: z.coerce.number().int().optional(),
});

/** The filters of the list, as the engine reads them: `filter[kind]`, and `filter[rating]` only with its ladder. */
export function sheetItemFilters(search: {
  readonly kind?: RatingKind | undefined;
  readonly rating?: number | undefined;
}): Record<string, string> {
  if (search.kind === undefined) {
    return {};
  }

  return search.rating === undefined
    ? { kind: search.kind }
    : { kind: search.kind, rating: String(search.rating) };
}
