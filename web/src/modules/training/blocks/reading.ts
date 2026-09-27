import type { MyTrainingDto, MyTrainingPathDto, TraineeTrainingDto } from '../api';
import type { StaffQueue } from '../schemas';
import { choosableSlots } from '../screens/dates';
import { REFUSALS, lastReported } from '../screens/trainee';
import { STAFF_TRAININGS } from '../screens/trainings';

/**
 * What the blocks of the training (design M3 §4.3, A10b) work out from what their providers answered, and nothing the server did not
 * say: which training is open on a ladder and what it waits for, the last report on it, and where the staff's queues are listed.
 * Every rule is the server's — which training is open, what refuses a request, which trainings wait for whom —: these only read it.
 */

/** One ladder as the trainee's block says it. */
export interface LadderNow {
  readonly path: MyTrainingPathDto;
  /** The training open on the ladder, the one the rules name (`openTrainingId`); none when the ladder is free. */
  readonly open: TraineeTrainingDto | null;
  /** How many dates its trainer proposed that are still to choose, while it waits for its date. */
  readonly toChoose: number;
  /** Whether what may be asked for, or the rule that refuses, is said too: always, but when the open training is the refusal. */
  readonly saysStanding: boolean;
  /** The last training of the ladder whose report is published. */
  readonly lastReport: TraineeTrainingDto | null;
}

/**
 * The trainee's ladders now (§4.3): on each, the open request or training and the dates to choose on it; what may be asked for next,
 * or why not — a ban says so even beside an open training, whose request it came before —; and the last report.
 */
export function laddersNow(mine: Pick<MyTrainingDto, 'paths' | 'trainings'>, now: number): LadderNow[] {
  return mine.paths.map((path) => {
    const open =
      path.openTrainingId === null
        ? null
        : (mine.trainings.find((training) => training.id === path.openTrainingId) ?? null);

    return {
      path,
      open,
      toChoose: open === null ? 0 : choosableSlots(open, now).length,
      saysStanding: open === null || path.refusal !== REFUSALS.open,
      lastReport: lastReported(path.kind, mine.trainings) ?? null,
    };
  });
}

/** The staff's list narrowed to one queue of work (A7): where a block's «see all» goes. */
export function queueHref(queue: StaffQueue): string {
  return `${STAFF_TRAININGS}?queue=${queue}`;
}
