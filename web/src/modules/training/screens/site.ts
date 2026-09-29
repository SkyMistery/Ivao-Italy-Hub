import type { PublicSessionDto } from '../api';

/**
 * What the public pages of the training (design M3 §4.1, A10b) work out from the sessions the server answered: their addresses, and
 * how a session is named. Which sessions are public, and whether the reader sees who is in them, are the server's answers (note
 * il-training-in-pubblico): these only read them.
 */

/** The public page of the training: the sessions still to be held, and «Request training». */
export const TRAINING = '/training';

/** The page of one session, where its entry of the calendar points (`Training.SessionPath` on the server). */
export function sessionHref(id: number): string {
  return `${TRAINING}/sessions/${String(id)}`;
}

/**
 * A session by its rating and its position, as its entry of the calendar is titled: the rating alone for a pilot's; its number when
 * the hub knows neither.
 */
export function sessionTitle(session: Pick<PublicSessionDto, 'id' | 'ratingShortName' | 'position'>): string {
  const parts = [session.ratingShortName, session.position].filter(
    (part): part is string => part !== null && part !== '',
  );

  return parts.length === 0 ? `#${String(session.id)}` : parts.join(' · ');
}
