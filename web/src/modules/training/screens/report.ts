import { ApiError } from '../../../shared/api/problem';
import type {
  MyTrainingPathDto,
  SessionOutcome,
  SheetSection,
  StaffEvaluationDto,
  StaffSessionDto,
  StaffTrainingDto,
  TheoryMark,
  TrainingEvaluationWriteDto,
  TrainingMemberDto,
} from '../api';
import type { RatingKind } from '../schemas';

import { splitRefusal } from './trainee';

/**
 * What the pages after the session (design M3 §2.6, §2.7, §4.1, §4.2; A9b) work out from what the server answered: whether the
 * reader records what the session came to, what a row of the sheet offers, the sheet as the report sends it, how an evaluation
 * reads, where a refusal of the report belongs, and who published it. Every rule is the server's (A9a, `TrainingSessions`,
 * `EvaluationSheet`): these only read what it answered, and never decide what it would refuse.
 */

/**
 * Whether the reader records what the session came to (§2.6): the training is dated and the server says they may now — its
 * session has started, and the reader conducts it. Rescheduled, not attended, or reported.
 */
export function recordsOutcome(training: StaffTrainingDto): boolean {
  return training.state === 'Scheduled' && training.actions.canRecordOutcome;
}

/** A row of the sheet the session did not touch (d4): neither a grade nor a mark. */
export const NOT_APPLICABLE = 'na';

/** The grades of an item of practice (R.5), lowest first. */
export const GRADES = ['1', '2', '3', '4', '5'] as const;

/** The marks of an item of theory (R.5), as `TheoryMark` spells them. */
export const MARKS = ['Done', 'NotDone', 'ToImprove'] as const satisfies readonly NonNullable<TheoryMark>[];

/** What a row of the sheet offers, by its section: not applicable first, then the grades of practice or the marks of theory. */
export function choicesOf(section: SheetSection): readonly string[] {
  return [NOT_APPLICABLE, ...(section === 'Practice' ? GRADES : MARKS)];
}

/** A row of the sheet as the trainer writes it: what they chose, the comment for the trainee, and the note for the staff. */
export interface RowWritten {
  readonly choice: string;
  readonly traineeComment: string;
  readonly staffNote: string;
}

/** A row nobody has written in yet: not applicable, and nothing said. */
export const UNWRITTEN_ROW: RowWritten = { choice: NOT_APPLICABLE, traineeComment: '', staffNote: '' };

/**
 * The sheet as the report sends it (§2.7): one entry for every row the page shows, in its order — so a refusal of `sheet[2]` is
 * about the third row on screen —, with the grade of an item of practice or the mark of one of theory, neither when the session
 * did not touch it (d4), and the two texts, an empty one as none. A row nobody wrote in goes as not applicable.
 */
export function sheetEntries(
  sheet: readonly StaffEvaluationDto[],
  written: Readonly<Record<number, RowWritten>>,
): TrainingEvaluationWriteDto[] {
  return sheet.map((item) => {
    const row = written[item.itemId] ?? UNWRITTEN_ROW;
    const chosen = row.choice === NOT_APPLICABLE ? null : row.choice;

    return {
      itemId: item.itemId,
      grade: item.section === 'Practice' && chosen !== null ? Number(chosen) : null,
      mark: item.section === 'Theory' && chosen !== null ? (chosen as NonNullable<TheoryMark>) : null,
      traineeComment: text(row.traineeComment),
      staffNote: text(row.staffNote),
    };
  });
}

/** How an item of a report reads (§1.4): its grade, its mark, or not applicable when the session did not touch it (d4). */
export type EvaluationSays =
  | { readonly kind: 'grade'; readonly grade: number }
  | { readonly kind: 'mark'; readonly mark: NonNullable<TheoryMark> }
  | { readonly kind: 'notApplicable' };

export function evaluationSays(item: {
  readonly section: SheetSection;
  readonly grade: number | null;
  readonly mark: TheoryMark | null;
}): EvaluationSays {
  if (item.section === 'Practice') {
    return item.grade === null ? { kind: 'notApplicable' } : { kind: 'grade', grade: item.grade };
  }

  return item.mark === null ? { kind: 'notApplicable' } : { kind: 'mark', mark: item.mark };
}

/** The fields of the report the generated form draws: a refusal of one of them lands on it. */
export const REPORT_FORM_FIELDS = [
  'generalComment',
  'staffComment',
  'readyForMockExam',
  'readyForExam',
  'cooldownWaived',
] as const;

/** A field of a row of the sheet, as the server names it in a refusal: `sheet[2].grade`, by the row of the report sent. */
const ROW_FIELD = /^sheet\[\d+\]\.(grade|mark|traineeComment|staffNote)$/;

/** The refusal of a sheet that changed meanwhile — an item switched off since the page was read —: the page reads it again. */
export const SHEET_CHANGED = 'training:errors.sheetChanged';

/**
 * A refusal of the report split by where it belongs: on the fields of the generated form; on the rows of the sheet, each on its
 * own control; or above the form, about the report as a whole — the sheet that changed, the state of the training. Anything that
 * is not a refusal with fields is the form's, whose banner says it.
 */
export function splitReportRefusal(error: unknown): {
  readonly form: Error | null;
  readonly sheet: ApiError | null;
  readonly page: ApiError | null;
} {
  const { form, page } = splitRefusal(error, REPORT_FORM_FIELDS);
  if (page === null) {
    return { form, sheet: null, page: null };
  }

  const rows = splitRefusal(page, (field) => ROW_FIELD.test(field));
  return { form, sheet: rows.form instanceof ApiError ? rows.form : null, page: rows.page };
}

/** The part of a refusal about one field, `sheet[2].grade` for one, as a refusal of its own; none when it says nothing of it. */
export function refusalOn(error: ApiError | null, field: string): ApiError | null {
  const keys = error?.problem?.errors?.[field];
  if (error === null || keys === undefined) {
    return null;
  }

  return new ApiError(error.status, { ...error.problem, errors: { [field]: keys } });
}

/**
 * Whether a refusal asks the page to be read again: somebody else moved the training (409), or its sheet changed meanwhile. The
 * page then shows the training and its sheet as they are now, keeping what was written.
 */
export function asksRereading(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    (error.status === 409 || (error.problem?.errors?.sheet ?? []).includes(SHEET_CHANGED))
  );
}

/** Who published the report and when (§2.7): whoever recorded the session held. None before a report. */
export function publishedBy(
  sessions: readonly StaffSessionDto[],
): { readonly by: TrainingMemberDto; readonly at: string } | null {
  const held = sessions.filter((session) => session.outcome === 'Held').at(-1);
  return held === undefined ? null : { by: held.recordedBy, at: held.recordedAt };
}

/**
 * What the trainee may do next on the ladder of a training that ended with its session — reported, or not attended (§2.2 point
 * 3, §4.1) —, as the answer of their trainings says it: wait until when the waiting runs, or ask for the next training — a mock
 * exam when the server says so (§2.8). Nothing when something else refuses a request now — a ban, another training open, the
 * hours —, which their trainings page says in full.
 */
export type NextOnTheLadder =
  | { readonly kind: 'wait'; readonly until: string }
  | { readonly kind: 'ask'; readonly ladder: RatingKind; readonly mockExam: boolean };

export function nextOnTheLadder(path: MyTrainingPathDto | undefined): NextOnTheLadder | null {
  if (path === undefined) {
    return null;
  }

  if (path.waitUntil !== null) {
    return { kind: 'wait', until: path.waitUntil };
  }

  return path.refusal === null && path.next !== null
    ? { kind: 'ask', ladder: path.kind, mockExam: path.isMockExam }
    : null;
}

/** The colour of what a session came to: held in green, as a completed training; rescheduled going on; a no-show in orange. */
export const OUTCOME_COLOURS: Readonly<Record<SessionOutcome, 'green' | 'indigo' | 'orange'>> = {
  Held: 'green',
  Rescheduled: 'indigo',
  NoShow: 'orange',
};

/** An empty box is no value, which is what the server reads as «not written». */
function text(value: string): string | null {
  const trimmed = value.trim();
  return trimmed === '' ? null : trimmed;
}
