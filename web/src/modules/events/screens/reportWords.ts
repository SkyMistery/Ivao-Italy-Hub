import type { TFunction } from 'i18next';

import { formatMoment } from '../../../shared/i18n/useMoment';
import type { ReportItemDto, ReportKind, ReportItemRequest } from '../api';
import type { AtcReportFormValues, PilotReportFormValues } from '../schemas';

/**
 * The words of the reports of support (design M4 §5.2, §5.3, E14a): the server sends data — what an item declared, what the check found —
 * and these write them in the language on screen, for the member's page and for the staff's list alike. Times in UTC, as every hour of an
 * event (`formatMoment`).
 */

/** What an item declared: a flight's callsign and its airports on its day, or a shift's position and hours. */
export function itemLine(item: ReportItemDto, kind: ReportKind, t: TFunction, locale: string): string {
  if (kind === 'Pilot') {
    return t('events:reports.flight', {
      callsign: item.callsign,
      departure: item.departureIcao ?? '',
      arrival: item.arrivalIcao ?? '',
      day: formatMoment(locale, item.flightDay, { time: false }),
    });
  }

  return t('events:reports.shift', {
    callsign: item.callsign,
    from: formatMoment(locale, item.fromUtc),
    to: formatMoment(locale, item.toUtc),
  });
}

/**
 * What the check found of an item: found — with the minutes and, for a flight, the distance between its airports —, not found, or not
 * asked because the source did not answer.
 */
export function checkLine(item: ReportItemDto, kind: ReportKind, t: TFunction): string {
  if (item.verified === null) {
    return t('events:reports.check.unavailable');
  }

  const distance =
    item.distanceNm === null ? '' : ` ${t('events:reports.check.distance', { distance: item.distanceNm })}`;

  if (!item.verified) {
    return kind === 'Pilot'
      ? `${t('events:reports.check.notFound')}${distance}`
      : t('events:reports.check.tooShort', { minutes: item.minutes ?? 0 });
  }

  return kind === 'Pilot'
    ? `${t('events:reports.check.flown', { minutes: item.minutes ?? 0 })}${distance}`
    : t('events:reports.check.controlled', { minutes: item.minutes ?? 0 });
}

/** An item and its check on one line, as the staff's list writes it in its cell. */
export function reportLines(
  items: readonly ReportItemDto[],
  kind: ReportKind,
  t: TFunction,
  locale: string,
): string[] {
  return items.map((item) => `${itemLine(item, kind, t, locale)} — ${checkLine(item, kind, t)}`);
}

/** A pilot's form as the server takes it: the airports and the callsign as written, the shift's fields left out. */
export function pilotItems(values: PilotReportFormValues): ReportItemRequest[] {
  return values.items.map((item) => ({
    callsign: item.callsign,
    departureIcao: item.departureIcao,
    arrivalIcao: item.arrivalIcao,
    flightDay: item.flightDay ?? null,
    fromUtc: null,
    toUtc: null,
  }));
}

/** A controller's form as the server takes it: the position and its hours, the flight's fields left out. */
export function atcItems(values: AtcReportFormValues): ReportItemRequest[] {
  return values.items.map((item) => ({
    callsign: item.callsign,
    departureIcao: null,
    arrivalIcao: null,
    flightDay: null,
    fromUtc: item.fromUtc ?? null,
    toUtc: item.toUtc ?? null,
  }));
}
