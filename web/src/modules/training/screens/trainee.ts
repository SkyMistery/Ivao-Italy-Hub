import { ApiError } from '../../../shared/api/problem';
import type { MyTrainingDto, MyTrainingPathDto, TraineeTrainingDto, TrainingState } from '../api';
import type { RatingKind } from '../schemas';

/**
 * What the trainee's two pages work out from the one answer they read, `GET /api/training/mine` (design M3 §4.1): which
 * ladder the request is on, what to say beside a refusal, where a refusal of the request belongs on the page, how a state
 * looks, and what the trainer's boxes of a report mean now. Every rule is the server's (A6a, `RequestRules`): these only read
 * what it answered.
 */

/** The trainee's two pages, under the segment the module reserves; the mail of a request received points at the first. */
export const MINE = '/training/mine';
export const REQUEST = '/training/request';

/** The refusals the pages say more about, as the server sends them: a refusal is a bare key, and the details are the page's. */
export const REFUSALS = {
  banned: 'training:errors.requestBanned',
  open: 'training:errors.requestOpen',
  waiting: 'training:errors.requestWaiting',
  hoursUnknown: 'training:errors.requestHoursUnknown',
  hoursTooFew: 'training:errors.requestHoursTooFew',
} as const;

/**
 * The ladder the request is on: the one the address asks for, or else the first a request may be made on, or else the first
 * — whose refusal the page then says.
 */
export function chosenPath(mine: MyTrainingDto, wanted: RatingKind | undefined): MyTrainingPathDto | null {
  return (
    mine.paths.find((path) => path.kind === wanted) ??
    mine.paths.find((path) => path.refusal === null && path.next !== null) ??
    mine.paths[0] ??
    null
  );
}

/** What a refusal needs beside its sentence, taken from the same answer: the key alone says what, never until when. */
export type RefusalDetail =
  | { readonly kind: 'bannedUntil'; readonly until: string }
  | { readonly kind: 'bannedForever' }
  | { readonly kind: 'open'; readonly trainingId: number }
  | { readonly kind: 'waitUntil'; readonly until: string }
  | { readonly kind: 'hours'; readonly minimum: number; readonly hours: number | null };

/** The detail of the refusal of a ladder, when it has one; none for the refusals whose sentence says it all. */
export function refusalDetail(path: MyTrainingPathDto): RefusalDetail | null {
  switch (path.refusal) {
    case REFUSALS.banned:
      // Without an end the ban holds until somebody lifts it.
      return path.bannedUntil === null
        ? { kind: 'bannedForever' }
        : { kind: 'bannedUntil', until: path.bannedUntil };
    case REFUSALS.open:
      return path.openTrainingId === null ? null : { kind: 'open', trainingId: path.openTrainingId };
    case REFUSALS.waiting:
      return path.waitUntil === null ? null : { kind: 'waitUntil', until: path.waitUntil };
    case REFUSALS.hoursUnknown:
    case REFUSALS.hoursTooFew:
      return path.minimumHours === null
        ? null
        : { kind: 'hours', minimum: path.minimumHours, hours: path.hours };
    default:
      return null;
  }
}

/**
 * A refusal of the request split by where it belongs. The form draws its own fields — the position and the two texts —, and
 * the rest is about the request as a whole — the ladder, the rating proposed, the answer on the theory —, which would
 * otherwise land on a field the form does not have and be shown nowhere. Anything that is not a refusal with fields is the
 * form's, whose banner says it.
 */
export function splitRefusal(
  error: unknown,
  formFields: readonly string[],
): { form: Error | null; page: ApiError | null } {
  if (!(error instanceof ApiError) || error.problem?.errors === undefined) {
    return { form: error instanceof Error ? error : new Error(String(error)), page: null };
  }

  const inTheForm: Record<string, string[]> = {};
  const onThePage: Record<string, string[]> = {};
  for (const [field, keys] of Object.entries(error.problem.errors)) {
    (formFields.includes(field) ? inTheForm : onThePage)[field] = keys;
  }

  return {
    form:
      Object.keys(inTheForm).length === 0
        ? null
        : new ApiError(error.status, { ...error.problem, errors: inTheForm }),
    page:
      Object.keys(onThePage).length === 0
        ? null
        : new ApiError(error.status, { ...error.problem, errors: onThePage }),
  };
}

/** Whether the hub refused the training by itself, because the trainee said the theory is not passed (§2.2). */
export function isTheoryRefusal(training: TraineeTrainingDto): boolean {
  return training.state === 'Rejected' && training.rejection === 'TheoryNotPassed';
}

/** Only a request nobody has accepted yet is taken back by its trainee (§2.2, d2). */
export function isCancellable(training: TraineeTrainingDto): boolean {
  return training.state === 'Requested';
}

/** The colour of a state: going on in blue and indigo, done in green, refused in red, a no-show in orange, over in grey. */
export const STATE_COLOURS: Readonly<
  Record<TrainingState, 'blue' | 'indigo' | 'green' | 'red' | 'orange' | 'gray'>
> = {
  Requested: 'blue',
  Accepted: 'indigo',
  Assigned: 'indigo',
  Scheduled: 'indigo',
  Completed: 'green',
  Rejected: 'red',
  Cancelled: 'gray',
  Closed: 'gray',
  NoShow: 'orange',
};

/**
 * The moment a state says of a training, beside when it was asked for: the decision, the session, the report or the
 * closure. None for a request still waiting and for a training whose date is still to be fixed.
 */
export function stateMoment(training: TraineeTrainingDto): string | null {
  switch (training.state) {
    case 'Accepted':
    case 'Rejected':
      return training.decidedAt;
    case 'Scheduled':
      return training.scheduledStartUtc;
    case 'Completed':
      return training.completedAt;
    case 'Cancelled':
    case 'Closed':
    case 'NoShow':
      return training.closedAt;
    default:
      return null;
  }
}

/**
 * «Ready for the exam» on a ladder: the trainer's box on the last report of the ladder, while its rating is still the one the
 * trainee would train for next. Once the exam is passed the rating moves on, and the box is history: it stays on its training.
 * The mock exam needs no reading of this kind: whether the next training is one, the server says (`isMockExam`).
 */
export function readyForExam(path: MyTrainingPathDto, trainings: readonly TraineeTrainingDto[]): boolean {
  const last = trainings
    .filter((training) => training.kind === path.kind && training.state === 'Completed')
    .sort(
      (one, other) => (other.completedAt ?? '').localeCompare(one.completedAt ?? '') || other.id - one.id,
    )[0];

  return last !== undefined && last.readyForExam && path.next !== null && last.rating === path.next.number;
}

const DAY = 24 * 60 * 60 * 1000;

/** The days left before a moment, counted up: a wait that ends tomorrow morning is one more day, never none. */
export function daysUntil(until: string, now: number): number {
  return Math.max(1, Math.ceil((Date.parse(until) - now) / DAY));
}

/** Hours of connection as a reader reads them, to a tenth at most; none when the network has said nothing, which is not zero. */
export function formatHours(hours: number | null, language: string): string | null {
  return hours === null ? null : new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(hours);
}
