import { useTranslation } from 'react-i18next';

/**
 * An instant as a reader reads one. UTC unless a zone is named, because UTC is what the network
 * runs on and what the hub stores; a local time is shown next to it and never instead of it
 * (plan §9.5). An empty string means "there was no usable instant", so a caller can leave it out.
 *
 * It lives here rather than beside the blocks because two very different things format an instant
 * the same way — a data block and `CalendarView` — and a second copy is how one of them quietly
 * starts showing a local time as if it were UTC.
 */
export function useMoment(): (
  value: unknown,
  options?: { timeZone?: string; time?: boolean; dateStyle?: 'medium' | 'short' | 'long' },
) => string {
  const { i18n } = useTranslation();

  return (value, options = {}) => {
    if (typeof value !== 'string' || value === '') {
      return '';
    }

    const moment = new Date(value);
    if (Number.isNaN(moment.getTime())) {
      return '';
    }

    return new Intl.DateTimeFormat(i18n.language, {
      dateStyle: options.dateStyle ?? 'medium',
      ...(options.time === false ? {} : { timeStyle: 'short' }),
      timeZone: options.timeZone ?? 'UTC',
    }).format(moment);
  };
}
