import { ApiError } from '../../../shared/api/problem';
import type { ChoiceOption } from '../../../shared/forms';
import { memberLabel, type StaffTrainingDto, type TrainerCandidateDto, type TrainingMemberDto } from '../api';
import type { StaffTrainingsSearch } from '../schemas';

/**
 * What the staff's pages of the trainings (design M3 §4.2, A7) work out from what the server answered: the order of the list
 * when the reader chose none, how the trainers are offered, and what the page says of the decision. Who may do what, and who
 * may train a training, are the server's answers (`actions`, the candidates): these only read them.
 */

/** The list of the trainings; the page of one is below it, by its identifier. */
export const STAFF_TRAININGS = '/staff/training';

export function staffTrainingHref(id: number): string {
  return `${STAFF_TRAININGS}/${String(id)}`;
}

/** The views that are a queue of work: the one that has waited longest comes first. */
const OLDEST_FIRST: readonly StaffTrainingsSearch['queue'][] = ['toApprove', 'toAssign', 'toClose'];

/**
 * The order of the list when the reader has not sorted it: a queue of work oldest first — the request that has waited longest
 * is the one to take —, every other view, and the whole list, newest first.
 */
export function listOrder(search: StaffTrainingsSearch): StaffTrainingsSearch {
  if (search.sort !== undefined) {
    return search;
  }

  return { ...search, dir: OLDEST_FIRST.includes(search.queue) ? 'asc' : 'desc' };
}

/**
 * The trainers the form offers: every candidate but the trainer already assigned, each with their name and VID, their rating
 * on the ladder and their positions of the staff of the training, which say who they are.
 */
export function trainerChoices(candidates: readonly TrainerCandidateDto[]): ChoiceOption[] {
  return candidates
    .filter((candidate) => !candidate.isCurrent)
    .map((candidate) => ({
      value: String(candidate.vid),
      label: [
        memberLabel({ vid: candidate.vid, name: candidate.name }),
        candidate.ratingShortName,
        candidate.positions.join(', '),
      ]
        .filter((part) => part !== null && part !== '')
        .join(' · '),
    }));
}

/**
 * Whether a step came back as a conflict: somebody else moved the training since the page was read. The page is read again
 * then, and the form it draws anew keeps no sentence of the one it replaced: a conflict is said in a notice instead.
 */
export function isConflict(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}

/** What the page says of the decision on a request. */
export type Decision =
  | { readonly kind: 'none' }
  | { readonly kind: 'accepted'; readonly by: TrainingMemberDto | null; readonly at: string }
  | {
      readonly kind: 'rejected';
      readonly by: TrainingMemberDto | null;
      readonly at: string;
      readonly reason: string | null;
    }
  | { readonly kind: 'theory'; readonly at: string }
  | { readonly kind: 'cancelled'; readonly at: string };

/**
 * The decision on a training, as its state tells it: none while it waits; the hub's own refusal — the theory not passed — or
 * the staff's, with its reason; the trainee's cancellation; or its acceptance, which every state after it carries.
 */
export function decisionOf(training: StaffTrainingDto): Decision {
  if (training.state === 'Cancelled' && training.closedAt !== null) {
    return { kind: 'cancelled', at: training.closedAt };
  }

  if (training.decidedAt === null || training.state === 'Requested' || training.state === 'Cancelled') {
    return { kind: 'none' };
  }

  if (training.state !== 'Rejected') {
    return { kind: 'accepted', by: training.decidedBy, at: training.decidedAt };
  }

  return training.rejection === 'TheoryNotPassed'
    ? { kind: 'theory', at: training.decidedAt }
    : { kind: 'rejected', by: training.decidedBy, at: training.decidedAt, reason: training.rejectionReason };
}
