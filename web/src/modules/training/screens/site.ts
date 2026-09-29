import type { PublicExamDto, PublicSessionDto } from '../api';

/**
 * What the public pages of the training (design M3 §4.1, A10b) work out from the sessions and the exams the server answered: their
 * addresses, how a session or an exam is named, and their order. Which ones are public, and whether the reader sees who is in them,
 * are the server's answers (note il-training-in-pubblico): these only read them.
 */

/** The public page of the training: the sessions still to be held, and «Request training». */
export const TRAINING = '/training';

/** The page of one session, where its entry of the calendar points (`Training.SessionPath` on the server). */
export function sessionHref(id: number): string {
  return `${TRAINING}/sessions/${String(id)}`;
}

/**
 * A session by its rating and its position, as its entry of the calendar is titled: the rating alone for a pilot's; its number when
 * the hub knows neither. An exam is titled the same way (A10c).
 */
export function sessionTitle(session: Pick<PublicSessionDto, 'id' | 'ratingShortName' | 'position'>): string {
  const parts = [session.ratingShortName, session.position].filter(
    (part): part is string => part !== null && part !== '',
  );

  return parts.length === 0 ? `#${String(session.id)}` : parts.join(' · ');
}

/** A line of the list of what is still to come (A10c): the session of a training, or an exam. */
export type UpcomingLine =
  | { readonly type: 'session'; readonly key: string; readonly session: PublicSessionDto }
  | { readonly type: 'exam'; readonly key: string; readonly exam: PublicExamDto };

/**
 * The sessions still to be held and the exams still to come in one list, the soonest first — a session before an exam at the same
 * moment —, and no more than `limit` of them when a block asks for fewer (none, or zero: all). The server answered each list already
 * narrowed and in order: this only puts the two together.
 */
export function upcomingLines(
  sessions: readonly PublicSessionDto[],
  exams: readonly PublicExamDto[],
  limit?: number,
): UpcomingLine[] {
  const lines = [
    ...sessions.map((session) => ({
      at: Date.parse(session.startsAtUtc),
      line: { type: 'session', key: `session-${String(session.id)}`, session } as const,
    })),
    ...exams.map((exam) => ({
      at: Date.parse(exam.startsAtUtc),
      line: { type: 'exam', key: `exam-${String(exam.id)}`, exam } as const,
    })),
  ]
    // A stable sort: at the same moment the sessions, which come first, stay first.
    .sort((one, other) => one.at - other.at)
    .map(({ line }): UpcomingLine => line);

  return limit !== undefined && limit > 0 ? lines.slice(0, limit) : lines;
}
