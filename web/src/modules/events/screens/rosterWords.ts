import type { TFunction } from 'i18next';

import type { EventAtcTransferDto, RosterRowDto } from '../api';

/**
 * The words of a row of the roster (design M4 §4.3, §4.4, E11b; note 2026-10-13-la-proposta-del-roster, readings 4 and 6), in the
 * language on screen: the server sends the why of a proposed shift and the warnings of a shift as data — a closed set of words and some
 * numbers —, and the page writes them (Carmine's answer 6 on #255). Pure, so that a test reads it without a page.
 *
 * - **The why**: what decided, how many could take the shift, the connections on the position and on its kind — or that the experience was
 *   not counted —, and the penalty when there is one; a shift of the staff says so; an uncovered piece says whether nobody could take it or
 *   those who could were already busy.
 * - **The warnings**: one line each; the position's minimum on the network in the core's own words, which name the network.
 */
export function rosterLines(row: RosterRowDto, t: TFunction): { why: string[]; alerts: string[] } {
  return { why: why(row, t), alerts: row.warnings.map((warning) => alert(warning, t)) };
}

/**
 * The warnings of a request to hand a shift over (E12, §4.4-bis), in the language on screen: that its shift is no longer theirs, when the
 * staff gave it to somebody else meanwhile, and the rules of the proposal whoever would take it does not keep — the roster's own words.
 */
export function transferLines(row: EventAtcTransferDto, t: TFunction): string[] {
  return [
    ...(row.stale ? [t('events:atc.transfers.stale')] : []),
    ...row.warnings.map((warning) => alert(warning, t)),
  ];
}

function why(row: RosterRowDto, t: TFunction): string[] {
  if (row.state === 'Uncovered') {
    return [
      row.candidates === 0
        ? t('events:atc.roster.why.uncoveredNobody')
        : t('events:atc.roster.why.uncoveredBusy', { count: row.candidates }),
    ];
  }

  const reason = row.reason;
  if (row.state === 'Staff' || reason === null) {
    return [t('events:atc.roster.why.staff')];
  }

  return [
    t(`events:atc.roster.why.decidedBy.${reason.decidedBy}`),
    t('events:atc.roster.why.candidates', { count: reason.candidates }),
    reason.callsignConnections === null || reason.typeConnections === null
      ? t('events:atc.roster.why.noExperience')
      : t('events:atc.roster.why.connections', {
          callsign: reason.callsignConnections,
          type: reason.typeConnections,
        }),
    ...(reason.penalty > 0 ? [t('events:atc.roster.why.penalty', { value: reason.penalty.toFixed(2) })] : []),
  ];
}

function alert(warning: RosterRowDto['warnings'][number], t: TFunction): string {
  return warning === 'BelowPositionMinimum'
    ? t('atcPositions.belowMinimum')
    : t(`events:atc.roster.warnings.${warning}`);
}
