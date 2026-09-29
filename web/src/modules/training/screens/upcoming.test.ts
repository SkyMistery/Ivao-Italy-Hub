import { expect, test } from 'vitest';

import type { PublicExamDto, PublicSessionDto } from '../api';

import { upcomingLines } from './site';

/**
 * The sessions still to be held and the exams still to come in one list (A10c): the server answers the two already narrowed, each the
 * soonest first; the page puts them together by when they start, and keeps as many as a block asks for. The ratings and the positions
 * are made up.
 */

function session(id: number, startsAtUtc: string): PublicSessionDto {
  return {
    id,
    kind: 'Atc',
    ratingShortName: 'R3',
    position: 'XXAA_BOX',
    startsAtUtc,
    held: false,
    trainee: null,
    trainer: null,
  };
}

function exam(id: number, startsAtUtc: string): PublicExamDto {
  return {
    id,
    kind: 'Pilot',
    ratingShortName: 'P3',
    position: null,
    startsAtUtc,
    candidateVid: null,
    examinerVid: null,
  };
}

const keys = (lines: ReturnType<typeof upcomingLines>) => lines.map((line) => line.key);

test('the sessions and the exams are one list, the soonest first, a session before an exam at the same moment', () => {
  const lines = upcomingLines(
    [session(1, '2026-10-02T18:00:00Z'), session(2, '2026-10-05T18:00:00Z')],
    [exam(1, '2026-10-01T09:00:00Z'), exam(2, '2026-10-02T18:00:00Z')],
  );

  expect(keys(lines)).toEqual(['exam-1', 'session-1', 'exam-2', 'session-2']);
  expect(lines.map((line) => line.type)).toEqual(['exam', 'session', 'exam', 'session']);
});

test('a block keeps as many as it asks for, of the two together; none or zero is all of them', () => {
  const sessions = [session(1, '2026-10-02T18:00:00Z'), session(2, '2026-10-05T18:00:00Z')];
  const exams = [exam(1, '2026-10-01T09:00:00Z')];

  expect(keys(upcomingLines(sessions, exams, 2))).toEqual(['exam-1', 'session-1']);
  expect(upcomingLines(sessions, exams, 0)).toHaveLength(3);
  expect(upcomingLines(sessions, exams)).toHaveLength(3);
  expect(upcomingLines([], [])).toEqual([]);
});
