import { z } from 'zod';

import type { ChoiceOption } from '../../shared/forms';

/**
 * The forms of the events' skeleton (M4, E2), as zod schemas: types and what is required. The rules — a kind the calendar
 * has, the ranges — are the server's (design M0 §7.5).
 *
 * A kind of event is chosen, never typed: it is a word of the division's calendar, which the bootstrap carries, so this
 * module writes no kind of its own (note 2026-09-29-i-tipi-di-evento).
 */

/** What a kind switches on when the staff chooses it for an event (`KindPreset`, design M4 §1.12). */
export interface KindPreset {
  readonly kind: string;
  readonly publicSlots: boolean;
  readonly privateSlots: boolean;
  readonly hasRoster: boolean;
  readonly wholeDivision: boolean;
  readonly inPerson: boolean;
}

/** The settings as the server keeps them (`EventsSettings`, design M4 §1.12): the ones of M4a. */
export interface EventsSettings {
  readonly kindPresets: readonly KindPreset[];
  readonly bookingGapMinutes: number;
  readonly pilotRetentionMonths: number;
  readonly reminderLeadHours: number;
}

/** What the fields of the settings choose from, already in the language on screen. */
export interface SettingsChoices {
  readonly kinds: readonly ChoiceOption[];
}

export function settingsSchema(choices: SettingsChoices) {
  return z.object({
    // A row is a kind and the five switches it presets; the staff changes them on the event.
    kindPresets: z.array(
      z.object({
        kind: z.string().min(1).meta({ choices: choices.kinds }),
        publicSlots: z.boolean(),
        privateSlots: z.boolean(),
        hasRoster: z.boolean(),
        wholeDivision: z.boolean(),
        inPerson: z.boolean(),
      }),
    ),
    bookingGapMinutes: z.number().int(),
    pilotRetentionMonths: z.number().int(),
    reminderLeadHours: z.number().int(),
  });
}

/** The form's values are the settings themselves, the lists copied: what the form sends back is what the server keeps. */
export type SettingsFormValues = z.output<ReturnType<typeof settingsSchema>>;

export function settingsToFormValues(settings: EventsSettings): SettingsFormValues {
  return {
    ...settings,
    kindPresets: settings.kindPresets.map((preset) => ({ ...preset })),
  };
}

/**
 * The kinds a row of the presets chooses from: the calendar's, and a kind already on the list that the calendar no longer
 * has, so that it can be seen — the server refuses it on its row — and taken out, rather than drawn as a select with
 * nothing chosen.
 */
export function kindChoices(
  calendar: readonly ChoiceOption[],
  presets: readonly KindPreset[],
): ChoiceOption[] {
  const gone = [...new Set(presets.map((preset) => preset.kind))]
    .filter((kind) => kind !== '' && !calendar.some((choice) => choice.value === kind))
    .map((kind) => ({ value: kind, label: kind }));

  return [...calendar, ...gone];
}
