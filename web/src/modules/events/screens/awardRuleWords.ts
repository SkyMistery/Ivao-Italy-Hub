import type { TFunction } from 'i18next';

/** What the words of a rule read of it: its criterion and its parameters, as the server sends them. */
export interface AwardRuleWords {
  readonly criterion: string;
  readonly departureIcao?: string | null;
  readonly arrivalIcao?: string | null;
  readonly count?: number | null;
  readonly distanceNm?: number | null;
  readonly minutes?: number | null;
  readonly positionType?: string | null;
}

/**
 * The parameters of a rule of award in words (design M4 §1.9, E14b): the server sends the criterion and the numbers, the page writes them in
 * the language on screen — «LIRF → LIRN», «at least 3 flights», «at least 120 minutes on TWR». A criterion without parameters has none to say.
 */
export function awardRuleParameters(rule: AwardRuleWords, t: TFunction, language: string): string {
  const number = (value: number | null | undefined) => new Intl.NumberFormat(language).format(value ?? 0);
  const route = `${rule.departureIcao ?? ''} → ${rule.arrivalIcao ?? ''}`;

  switch (rule.criterion) {
    case 'MinFlights':
      return t('events:awardRules.parameters.flights', {
        count: rule.count ?? 0,
        number: number(rule.count),
      });
    case 'Route':
      return route;
    case 'RouteTimes':
      return t('events:awardRules.parameters.routeTimes', {
        route,
        count: rule.count ?? 0,
        number: number(rule.count),
      });
    case 'MinLegDistance':
      return t('events:awardRules.parameters.distance', { number: number(rule.distanceNm) });
    case 'MinAtcMinutes':
      return rule.positionType
        ? t('events:awardRules.parameters.minutesOn', {
            number: number(rule.minutes),
            type: rule.positionType,
          })
        : t('events:awardRules.parameters.minutes', { number: number(rule.minutes) });
    default:
      return '';
  }
}
