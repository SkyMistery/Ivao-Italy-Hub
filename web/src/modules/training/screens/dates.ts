import type { LocalizedString } from '../../../shared/api/bootstrap';
import type {
  DateConflictsDto,
  DateWarning,
  StaffTrainingDto,
  TraineeSlotDto,
  TraineeTrainingDto,
} from '../api';
import type { RatingKind } from '../schemas';

import { staffTrainingHref } from './trainings';

/**
 * What the pages of the dates of a training (design M3 §2.5, §4.1, §4.2; A8b) work out from what the server answered: the dates a
 * trainee is offered, how a span of time is said, what a set of dates meets and whether that asks for a confirmation, what a
 * warning is about, what the staff may do on the dates, and who closed a training. Every rule is the server's (A8a,
 * `TrainingDates`): these only read what it answered, and never decide what it would refuse.
 */

/**
 * The dates a trainee chooses among (§2.5, d1): while the training waits for its date, the ones still to come — the server sends
 * only those, and one that goes by while the page is open is offered no more —, the soonest first.
 */
export function choosableSlots(training: TraineeTrainingDto, now: number): TraineeSlotDto[] {
  if (training.state !== 'Assigned') {
    return [];
  }

  return training.slots
    .filter((slot) => Date.parse(slot.startsAtUtc) > now)
    .sort((one, other) => Date.parse(one.startsAtUtc) - Date.parse(other.startsAtUtc) || one.id - other.id);
}

/** How the pages format an instant (`useMoment`), handed in so that what is said of a span stays a function of its data. */
export type FormatMoment = (
  value: string,
  options?: { readonly timeZone?: string; readonly time?: boolean; readonly date?: boolean },
) => string;

/**
 * A span of time in one zone, as a line says it: the day once and the two times when it starts and ends on the same day there,
 * both moments whole when it does not — a session at 23:00 UTC ends on the next day in UTC, and maybe not where the division
 * lives. Without an end, its start. The zone is the sentence's to name.
 */
export function spanText(
  startsAtUtc: string,
  endsAtUtc: string | null,
  moment: FormatMoment,
  timeZone?: string,
): string {
  const zone = timeZone === undefined ? {} : { timeZone };

  if (endsAtUtc === null) {
    return moment(startsAtUtc, zone);
  }

  return moment(startsAtUtc, { ...zone, time: false }) === moment(endsAtUtc, { ...zone, time: false })
    ? `${moment(startsAtUtc, zone)}–${moment(endsAtUtc, { ...zone, date: false })}`
    : `${moment(startsAtUtc, zone)} – ${moment(endsAtUtc, zone)}`;
}

/**
 * A date as a form of the staff writes it: its start, and — for a date proposed — its end; a box left empty is `null`. The date
 * set by hand has no end at all.
 */
export interface WrittenDate {
  readonly startsAtUtc: string | null;
  readonly endsAtUtc?: string | null;
}

/** Whether a date is written whole: what is asked about before it is written. A box left empty is the server's to refuse. */
export function isWhole(date: WrittenDate): boolean {
  return date.startsAtUtc !== null && date.endsAtUtc !== null;
}

/** Whether the dates on screen are still the ones asked about: a confirmation is of those dates, and of no others. */
export function sameDates(one: readonly WrittenDate[], other: readonly WrittenDate[]): boolean {
  return (
    one.length === other.length &&
    one.every(
      (date, index) =>
        date.startsAtUtc === other[index]?.startsAtUtc &&
        (date.endsAtUtc ?? null) === (other[index]?.endsAtUtc ?? null),
    )
  );
}

/**
 * What a set of dates meets (§2.5), as the server answered for each of them before anybody writes them: the division's policy,
 * and each date's warnings — none for a date that is not whole, which is not asked about.
 */
export interface Met {
  readonly dates: readonly WrittenDate[];
  readonly policy: DateConflictsDto['policy'] | null;
  readonly warnings: readonly (readonly DateWarning[])[];
}

/** Asks what each whole date meets (`GET …/conflicts`), all at once. */
export async function whatTheyMeet(
  dates: readonly WrittenDate[],
  ask: (startsAtUtc: string, endsAtUtc: string | null) => Promise<DateConflictsDto>,
): Promise<Met> {
  const answers = await Promise.all(
    dates.map((date) =>
      date.startsAtUtc === null || !isWhole(date)
        ? Promise.resolve(null)
        : ask(date.startsAtUtc, date.endsAtUtc ?? null),
    ),
  );

  return {
    dates,
    policy: answers.find((answer) => answer !== null)?.policy ?? null,
    warnings: answers.map((answer) => answer?.warnings ?? []),
  };
}

function found(met: Met): boolean {
  return met.warnings.some((warnings) => warnings.length > 0);
}

/**
 * Whether what the dates meet asks for a confirmation before they are written: the division warns (`Warn`), and something was
 * found. With `Block` it is a refusal, which the server says on each date's field; with `None` nothing is looked at.
 */
export function asksConfirmation(met: Met): boolean {
  return met.policy === 'Warn' && found(met);
}

/** Whether what the dates meet is a refusal: the division blocks (`Block`), and something was found. */
export function isRefused(met: Met): boolean {
  return met.policy === 'Block' && found(met);
}

/**
 * What a warning is about (§2.5), as its line says it: another training by what the public calendar shows of it — its ladder, its
 * rating and position, never whose it is — with the staff's page of it; an entry of the calendar by its kind and its title in the
 * language on screen, with where it is read. The words of the ladder and of the kind are the page's.
 */
export type WarningSays =
  | {
      readonly about: 'training';
      readonly ladder: RatingKind | null;
      readonly what: string;
      readonly startsAtUtc: string;
      readonly href: string | null;
    }
  | {
      readonly about: 'calendar';
      readonly calendarKind: string | null;
      readonly what: string;
      readonly startsAtUtc: string;
      readonly endsAtUtc: string | null;
      readonly href: string | null;
    };

export function warningSays(
  warning: DateWarning,
  read: (value: LocalizedString | null) => string,
): WarningSays {
  if (warning.kind === 'Training') {
    return {
      about: 'training',
      ladder: warning.trainingKind,
      what: [warning.ratingShortName, warning.position]
        .filter((part) => part !== null && part !== '')
        .join(' · '),
      startsAtUtc: warning.startsAtUtc,
      href: warning.trainingId === null ? null : staffTrainingHref(warning.trainingId),
    };
  }

  return {
    about: 'calendar',
    calendarKind: warning.calendarKind,
    what: read(warning.title),
    startsAtUtc: warning.startsAtUtc,
    endsAtUtc: warning.endsAtUtc,
    href: warning.url,
  };
}

/**
 * Whether an address is one of this hub's own — a path — rather than another site's: the page follows the first without leaving,
 * and opens the second beside it. A path starting with two slashes is another site.
 */
export function isHubAddress(href: string): boolean {
  return href.startsWith('/') && !href.startsWith('//');
}

/**
 * What the staff may do on the dates of a training (§2.5), as the server's answer on the row allows (`canConduct`): propose dates
 * and take one back while it waits for its date, set the date by hand then and once it has one.
 */
export function dateSteps(training: StaffTrainingDto): {
  readonly propose: boolean;
  readonly setByHand: boolean;
} {
  return {
    propose: training.actions.canConduct && training.state === 'Assigned',
    setByHand: training.actions.canConduct,
  };
}

/**
 * Who closed a training and why (§2.5, R.3): the staff, with the reason the trainee reads; or the hub, because the trainee chose
 * no date in the time the division gives — its closing has no reason. None for a training that is not closed.
 */
export type Closing =
  | { readonly by: 'staff'; readonly at: string; readonly reason: string }
  | { readonly by: 'hub'; readonly at: string };

export function closingOf(training: {
  readonly state: TraineeTrainingDto['state'];
  readonly closedAt: string | null;
  readonly closeReason: string | null;
}): Closing | null {
  if (training.state !== 'Closed' || training.closedAt === null) {
    return null;
  }

  return training.closeReason === null
    ? { by: 'hub', at: training.closedAt }
    : { by: 'staff', at: training.closedAt, reason: training.closeReason };
}
