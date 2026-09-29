import { isErased } from '../../../shared/ui';
import type { BanStatus, MyTrainingPathDto, StaffTrainingDto, TrainingRatingDto } from '../api';
import { RATING_KINDS, type RatingKind } from '../schemas';

import { REFUSALS } from './trainee';

/**
 * What the staff's pages of a trainee's path and of the bans (design M3 §2.9, §4.2, A10a) work out from what the server answered:
 * their addresses, what a ladder says of the trainee to the staff, and the trainings by ladder and rating. Where the trainee stands,
 * whether a ban holds and who may ban are the server's answers: these only read them.
 */

/** The page that asks for a trainee by VID; the path of one is below it. */
export const TRAINEES = '/staff/training/trainees';

/** The bans, generated; a new one is `new`, with the member of the path it was opened from in `?vid=`. */
export const BANS = '/staff/training/bans';

/**
 * The path of a trainee; none for a person whose data was erased (design M3 §6.1, A12b): the pseudonym in their place is nobody's, it
 * has no path — the server answers 404 —, and a page draws the person without a link.
 */
export function traineeHref(vid: number): string | null {
  return isErased(vid) ? null : `${TRAINEES}/${String(vid)}`;
}

/** «Ban»: the form of a new ban, with the member written when the path of one opened it, and back there once it is given. */
export function banFormHref(vid?: number): string {
  return vid === undefined ? `${BANS}/new` : `${BANS}/new?vid=${String(vid)}`;
}

/** The colour of how a ban stands: one that holds in red, one over or lifted in grey. */
export const BAN_COLOURS: Readonly<Record<BanStatus, 'red' | 'gray'>> = {
  Holds: 'red',
  Over: 'gray',
  Lifted: 'gray',
};

/**
 * Where a trainee stands on one ladder, as the staff reads it (§2.2): what they may ask for now — a mock exam when the server says
 * so —, or the first rule that refuses, with what the answer says beside it. The refusals are the trainee's sentences on their own
 * pages; the staff's page says them of the trainee.
 */
export type LadderSays =
  | { readonly kind: 'canAsk'; readonly next: TrainingRatingDto; readonly isMockExam: boolean }
  | { readonly kind: 'banned'; readonly until: string | null }
  | { readonly kind: 'open'; readonly trainingId: number | null }
  | { readonly kind: 'waiting'; readonly until: string | null }
  | { readonly kind: 'hours'; readonly minimum: number | null; readonly hours: number | null }
  | { readonly kind: 'noPosition' }
  | { readonly kind: 'nothingToAsk' }
  | { readonly kind: 'other'; readonly refusal: string };

export function ladderSays(ladder: MyTrainingPathDto): LadderSays {
  switch (ladder.refusal) {
    case null:
      return ladder.next === null
        ? { kind: 'nothingToAsk' }
        : { kind: 'canAsk', next: ladder.next, isMockExam: ladder.isMockExam };
    case REFUSALS.banned:
      // Without an end the ban holds until somebody lifts it.
      return { kind: 'banned', until: ladder.bannedUntil };
    case REFUSALS.open:
      return { kind: 'open', trainingId: ladder.openTrainingId };
    case REFUSALS.waiting:
      return { kind: 'waiting', until: ladder.waitUntil };
    case REFUSALS.hoursUnknown:
    case REFUSALS.hoursTooFew:
      return { kind: 'hours', minimum: ladder.minimumHours, hours: ladder.hours };
    case REFUSALS.noPosition:
      return { kind: 'noPosition' };
    case REFUSALS.nothingToAsk:
      return { kind: 'nothingToAsk' };
    default:
      // A rule a later server adds, said as the trainee reads it rather than not at all.
      return { kind: 'other', refusal: ladder.refusal };
  }
}

/** The trainings of one rating of a ladder, in the server's order: the newest first. */
export interface RatingTrainings {
  readonly rating: number;
  readonly ratingShortName: string | null;
  readonly trainings: readonly StaffTrainingDto[];
}

/** The trainings of one ladder, by rating: the rating trained most recently first. */
export interface LadderTrainings {
  readonly kind: RatingKind;
  readonly ratings: readonly RatingTrainings[];
}

/**
 * Every training of the trainee by ladder and rating (§4.2), in the order of the core's ladders — a ladder without trainings left
 * out —, each rating where its newest training falls, and its trainings in the server's order, the newest first.
 */
export function trainingsByLadder(trainings: readonly StaffTrainingDto[]): LadderTrainings[] {
  return RATING_KINDS.map((kind) => {
    const ratings: { rating: number; ratingShortName: string | null; trainings: StaffTrainingDto[] }[] = [];

    for (const training of trainings.filter((each) => each.kind === kind)) {
      const group = ratings.find((each) => each.rating === training.rating);
      if (group === undefined) {
        ratings.push({
          rating: training.rating,
          ratingShortName: training.ratingShortName,
          trainings: [training],
        });
      } else {
        group.trainings.push(training);
      }
    }

    return { kind, ratings };
  }).filter((ladder) => ladder.ratings.length > 0);
}
