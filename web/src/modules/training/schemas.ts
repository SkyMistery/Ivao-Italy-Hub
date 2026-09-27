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

// ---- the request (A6) ---------------------------------------------------------------------------------------------------

type TrainingRequestWriteDto = components['schemas']['TrainingRequestWriteDto'];

/**
 * A request as the trainee writes it (design M3 §2.2): on a ladder trained on positions the position, a **closed** suggestion
 * among the ones the server offers — `null` for the other ladder, where it is carried empty and never drawn —, and the two
 * texts in their own words. The ladder and the rating are not fields: the page chose the ladder, and the rating is the one the
 * server proposes. Nothing is required here: a position left out, a text too long are the server's to refuse, field by field,
 * in the words of the language files.
 */
export function requestSchema(positions: readonly Suggestion[] | null) {
  return z.object({
    position:
      positions === null
        ? z.string().meta({ hidden: true })
        : z.string().meta({ suggestions: positions, suggestionsOnly: true }),
    availabilityText: z.string().meta({ multiline: true }),
    notesText: z.string().meta({ multiline: true }),
  });
}

export type RequestFormValues = z.output<ReturnType<typeof requestSchema>>;

export const EMPTY_REQUEST: RequestFormValues = { position: '', availabilityText: '', notesText: '' };

/** The request as the server takes it: the ladder, the rating the page proposed, and the answer on the theory if it was asked. */
export function requestFromFormValues(
  values: RequestFormValues,
  proposed: { readonly kind: RatingKind; readonly number: number },
  theoryPassed: boolean | null,
): TrainingRequestWriteDto {
  return {
    kind: proposed.kind,
    rating: proposed.number,
    position: written(values.position),
    availabilityText: written(values.availabilityText),
    notesText: written(values.notesText),
    theoryPassed,
  };
}

/** `/training/request`: the ladder the request is on, when a link chose it — `/training/mine` does. */
export const requestSearchSchema = z.object({
  kind: z.enum(RATING_KINDS).optional(),
});

export type RequestSearch = z.output<typeof requestSearchSchema>;

/** An empty box is no value, which is what the server reads as «not written». */
function written(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}

// ---- the staff's side (A7) ----------------------------------------------------------------------------------------------

type TrainingAssignmentDto = components['schemas']['TrainingAssignmentDto'];

/** The views of the staff's list (design M3 §4.2), as `StaffQueue` names them on the server, in the order they are offered. */
export const STAFF_QUEUES = ['toApprove', 'toAssign', 'inProgress', 'toClose', 'history'] as const;

export type StaffQueue = (typeof STAFF_QUEUES)[number];

/** `/staff/training`: the five of every list, the view and the ladder it is narrowed to. */
export const staffTrainingsSearchSchema = listSearchSchema.extend({
  queue: z.enum(STAFF_QUEUES).optional(),
  kind: z.enum(RATING_KINDS).optional(),
});

export type StaffTrainingsSearch = z.output<typeof staffTrainingsSearchSchema>;

/** The filters of the list, as the engine reads them: `filter[queue]` and `filter[kind]`, each when it is chosen. */
export function staffTrainingsFilters(search: {
  readonly queue?: StaffQueue | undefined;
  readonly kind?: RatingKind | undefined;
}): Record<string, string> {
  return {
    ...(search.queue === undefined ? {} : { queue: search.queue }),
    ...(search.kind === undefined ? {} : { kind: search.kind }),
  };
}

/**
 * A refusal of the staff (design M3 §2.3): the reason, as the trainee will read it. No rule here: a reason left out or too long
 * is the server's to refuse, in the words of the language files.
 */
export const rejectSchema = z.object({
  reason: z.string().meta({ multiline: true }),
});

export type RejectValues = z.output<typeof rejectSchema>;

/**
 * An assignment (§2.4): the trainer, chosen among the candidates the server offers — by VID, the value of the choice —, at the
 * version of the training the page read. Nothing chosen is the server's to refuse.
 */
export function assignSchema(trainers: readonly ChoiceOption[]) {
  return z.object({
    trainerVid: z.string().meta({ choices: trainers }),
    rowVersion: z.string().meta({ hidden: true }),
  });
}

export type AssignValues = z.output<ReturnType<typeof assignSchema>>;

export function assignFromFormValues(values: AssignValues): TrainingAssignmentDto {
  return { trainerVid: Number(values.trainerVid), rowVersion: values.rowVersion };
}

// ---- the dates (A8) -----------------------------------------------------------------------------------------------------

type TrainingSlotWriteDto = components['schemas']['TrainingSlotWriteDto'];

/**
 * The trainer's dates (design M3 §2.5), proposed together: each when it would start and end, an instant of the form — ISO in UTC,
 * with the division's own time under it. The version of the training is not a field: it is the one the page has when the dates
 * go, so that a page read again after somebody else's step keeps what was written. Nothing is required here: a box left empty, a
 * date gone by, one too long are the server's to refuse, each on its own field.
 */
export const proposalSchema = z.object({
  slots: z.array(
    z.object({
      startsAtUtc: z.string().optional().meta({ datetime: true }),
      endsAtUtc: z.string().optional().meta({ datetime: true }),
    }),
  ),
});

export type ProposalValues = z.output<typeof proposalSchema>;

/** A proposal starts with one date to write; the list adds the others. */
export const EMPTY_PROPOSAL: ProposalValues = { slots: [{}] };

/** The dates as the server takes them: a box left empty travels empty, and the server says it is required. */
export function proposalFromFormValues(values: ProposalValues): TrainingSlotWriteDto[] {
  return values.slots.map((slot) => ({
    startsAtUtc: slot.startsAtUtc ?? null,
    endsAtUtc: slot.endsAtUtc ?? null,
  }));
}

/**
 * The date set by hand (§2.5, d2): when the session starts — among the dates proposed or not, and gone by too, for a session
 * held earlier than planned. Like the proposal, the version is the page's.
 */
export const dateSchema = z.object({
  startsAtUtc: z.string().optional().meta({ datetime: true }),
});

export type DateValues = z.output<typeof dateSchema>;

/** A closing of the staff (§2.5, R.3) asks what a refusal asks (A7): the reason, as the trainee will read it. */
export const closeSchema = rejectSchema;

export type CloseValues = RejectValues;

// ---- after the session (A9) ---------------------------------------------------------------------------------------------

type TrainingReportDto = components['schemas']['TrainingReportDto'];
type TrainingEvaluationWriteDto = components['schemas']['TrainingEvaluationWriteDto'];

/**
 * A session rescheduled for too little traffic (design M3 §2.6, R.5): the internal notes, for the staff and the trainers only —
 * the trainee never reads them. They may be left empty; one too long is the server's to refuse.
 */
export const rescheduleSchema = z.object({
  notes: z.string().meta({ multiline: true }),
});

export type RescheduleValues = z.output<typeof rescheduleSchema>;

/** The notes as the server takes them: empty ones are none. */
export function notesFromFormValues(values: RescheduleValues): string | null {
  return written(values.notes);
}

/**
 * The report as a whole (§2.7), the part of it the generated form draws: the comment for the trainee and the one for the staff,
 * and the trainer's three boxes. The sheet is not a field of this form: its rows are the items of the training's rating, each
 * with a label that is the item's title and a mark that is a grade or a tick by its section, which the page draws beside the
 * form (A9b). On a mock exam «ready for the mock exam» is carried unticked and never drawn: its next training is not one again.
 */
export function reportSchema(isMockExam: boolean) {
  return z.object({
    generalComment: z.string().meta({ multiline: true }),
    staffComment: z.string().meta({ multiline: true }),
    readyForMockExam: z.boolean().meta({ hidden: isMockExam }),
    readyForExam: z.boolean(),
    cooldownWaived: z.boolean(),
  });
}

export type ReportValues = z.output<ReturnType<typeof reportSchema>>;

export const EMPTY_REPORT: ReportValues = {
  generalComment: '',
  staffComment: '',
  readyForMockExam: false,
  readyForExam: false,
  cooldownWaived: false,
};

/** The report as the server takes it: the sheet the page wrote, the form's comments — an empty one as none — and its boxes. */
export function reportFromFormValues(
  values: ReportValues,
  sheet: readonly TrainingEvaluationWriteDto[],
  rowVersion: string,
): TrainingReportDto {
  return {
    sheet: [...sheet],
    generalComment: written(values.generalComment),
    staffComment: written(values.staffComment),
    readyForMockExam: values.readyForMockExam,
    readyForExam: values.readyForExam,
    cooldownWaived: values.cooldownWaived,
    rowVersion,
  };
}
