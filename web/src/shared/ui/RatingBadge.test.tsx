import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { RatingBadge } from './badges';

/**
 * The twenty-fourth component of the closed list (M3, A1). Its key is built at runtime — `ratings.${kind}.${shortName}`,
 * the same as the server's `Rating.NameKey` — so the language check of the build cannot see it: this test reads the
 * text back instead.
 */

test('the short name is the badge, and the full name its title and what a screen reader hears', () => {
  renderWithProviders(<RatingBadge kind="Atc" shortName="ADC" />);

  expect(screen.getByText('ADC')).toBeInTheDocument();
  expect(screen.getByTitle(englishCommon.ratings.Atc.ADC)).toBeInTheDocument();
  expect(screen.getByText(englishCommon.ratings.Atc.ADC)).toHaveClass('sr-only');
});

test('every rating of the language files is drawn with its own name', () => {
  for (const [kind, ladder] of Object.entries(englishCommon.ratings)) {
    for (const [shortName, name] of Object.entries(ladder)) {
      const { unmount } = renderWithProviders(<RatingBadge kind={kind} shortName={shortName} />);

      expect(screen.getByTitle(name)).toHaveTextContent(shortName);
      unmount();
    }
  }
});

test('a short name the language files do not know is still drawn, with itself as its name', () => {
  renderWithProviders(<RatingBadge kind="Atc" shortName="XYZ" />);

  expect(screen.getByTitle('XYZ')).toHaveTextContent('XYZ');
});
