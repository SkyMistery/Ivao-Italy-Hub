import type { TFunction } from 'i18next';
import { describe, expect, it } from 'vitest';

import { AWARD_CRITERIA, AWARD_PARAMETERS, awardRuleSchema, emptyAwardRule } from '../schemas';

import { awardRuleParameters } from './awardRuleWords';

/** A `t` that writes the key and what it was given, so a test reads which words a rule asks for. */
const t = ((key: string, options?: Record<string, unknown>) =>
  options === undefined ? key : `${key} ${JSON.stringify(options)}`) as unknown as TFunction;

describe('the words of a rule of award', () => {
  it('writes the parameters each criterion takes, the numbers in the language on screen', () => {
    expect(awardRuleParameters({ criterion: 'AnyFlight' }, t, 'en')).toBe('');
    expect(
      awardRuleParameters({ criterion: 'Route', departureIcao: 'XEA1', arrivalIcao: 'XEA2' }, t, 'en'),
    ).toBe('XEA1 → XEA2');
    expect(awardRuleParameters({ criterion: 'MinFlights', count: 3 }, t, 'en')).toBe(
      'events:awardRules.parameters.flights {"count":3,"number":"3"}',
    );
    expect(
      awardRuleParameters(
        { criterion: 'RouteTimes', departureIcao: 'XEA1', arrivalIcao: 'XEA2', count: 2 },
        t,
        'en',
      ),
    ).toBe('events:awardRules.parameters.routeTimes {"route":"XEA1 → XEA2","count":2,"number":"2"}');
    expect(awardRuleParameters({ criterion: 'MinLegDistance', distanceNm: 10800 }, t, 'it')).toBe(
      'events:awardRules.parameters.distance {"number":"10.800"}',
    );
    expect(
      awardRuleParameters({ criterion: 'MinAtcMinutes', minutes: 120, positionType: 'TWR' }, t, 'en'),
    ).toBe('events:awardRules.parameters.minutesOn {"number":"120","type":"TWR"}');
    expect(
      awardRuleParameters({ criterion: 'MinAtcMinutes', minutes: 120, positionType: null }, t, 'en'),
    ).toBe('events:awardRules.parameters.minutes {"number":"120"}');
  });

  it('draws only the parameters of the criterion chosen', () => {
    const choices = {
      awards: [{ value: '1', label: 'Pilot support' }],
      positionTypes: [{ value: 'TWR', label: 'TWR' }],
    };
    const drawn = (criterion: (typeof AWARD_CRITERIA)[number]) =>
      Object.entries(awardRuleSchema(criterion, choices).shape)
        .filter(([, field]) => (field.meta() as { hidden?: boolean } | undefined)?.hidden !== true)
        .map(([name]) => name);

    expect(drawn('AnyShift')).toEqual(['awardId']);
    expect(drawn('RouteTimes')).toEqual(['awardId', 'departureIcao', 'arrivalIcao', 'count']);
    expect(drawn('MinAtcMinutes')).toEqual(['awardId', 'minutes', 'positionType']);

    // Every criterion of the server's list has its parameters, and a new rule parses empty.
    expect(Object.keys(AWARD_PARAMETERS)).toEqual([...AWARD_CRITERIA]);
    expect(awardRuleSchema('AnyFlight', choices).safeParse(emptyAwardRule(7)).success).toBe(true);
  });
});
