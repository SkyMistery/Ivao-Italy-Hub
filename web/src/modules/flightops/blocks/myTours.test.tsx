import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ComponentProps } from 'react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import englishFlightOps from '../locales/en/flightops.json';
import italianFlightOps from '../locales/it/flightops.json';

import { type MyToursData, MyToursBlock } from './myTours';

/**
 * The summary of `flightops.myTours` (#532): legs accepted, time flown, tours completed — each figure with its own plural, so
 * one leg and one tour read in the singular in every language of the division, and zero and many in the plural.
 */

// The bootstrap never answers: the summary does not wait for it.
const api = vi.hoisted(() => ({ get: vi.fn(() => new Promise(() => undefined)) }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

// The block's links need no router to be read: what is under test is the summary's words.
vi.mock('../../../app/layouts/RouterAnchor', () => ({
  RouterAnchor: (props: ComponentProps<'a'>) => <a {...props} />,
}));

function draw(language: 'en' | 'it', legsAccepted: number, toursCompleted: number) {
  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishFlightOps);
  i18n.addResourceBundle('it', 'flightops', italianFlightOps);
  void i18n.changeLanguage(language);

  const data: MyToursData = {
    signedIn: true,
    tours: [],
    toModify: [],
    answered: [],
    summary: { legsAccepted, minutesFlown: 65, toursCompleted },
  };

  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient()}>
        <MyToursBlock id="b1" props={{}} data={data} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe('flightops.myTours summary', () => {
  test.each([
    ['en', 1, '1 leg accepted · 1 h 05 min flown · 1 tour completed'],
    ['en', 2, '2 legs accepted · 1 h 05 min flown · 2 tours completed'],
    ['en', 0, '0 legs accepted · 1 h 05 min flown · 0 tours completed'],
    ['it', 1, '1 leg accettata · 1 h 05 min volati · 1 tour completato'],
    ['it', 2, '2 leg accettate · 1 h 05 min volati · 2 tour completati'],
    ['it', 0, '0 leg accettate · 1 h 05 min volati · 0 tour completati'],
  ] as const)('in %s, %i of each reads «%s»', (language, count, words) => {
    draw(language, count, count);

    expect(screen.getByText(words)).toBeInTheDocument();
  });

  test('the two figures take their plural apart', () => {
    draw('en', 1, 2);

    expect(screen.getByText('1 leg accepted · 1 h 05 min flown · 2 tours completed')).toBeInTheDocument();
  });
});
