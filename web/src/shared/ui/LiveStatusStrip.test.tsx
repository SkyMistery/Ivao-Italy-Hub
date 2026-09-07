import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { LiveStatusStrip } from './LiveStatusStrip';

/**
 * The strip across the top of the public site (design M1 §6.2). Two states are worth a test and
 * they are the two the design argues about: what it draws when the network answered, and what it
 * draws when it could not be asked.
 *
 * The words come from the language files the application ships, never from a copy typed here: a
 * test carrying its own sentence passes while the screen shows a raw key.
 */

test('draws what the network answered, in the words the block already uses', () => {
  renderWithProviders(
    <LiveStatusStrip
      status={{
        updatedAt: '2026-09-07T09:00:00Z',
        figures: [
          { figure: 'divisionAtc', value: 4 },
          { figure: 'divisionPilots', value: 1234 },
        ],
      }}
    />,
  );

  expect(screen.getByText(englishCommon.liveStatus.title)).toBeInTheDocument();

  // The figures, and the captions of `networkStats`: one set of numbers deserves one set of words,
  // so the strip borrows the block's rather than owning a second copy of them.
  expect(screen.getByText('4')).toBeInTheDocument();
  expect(screen.getByText(englishCommon.blocks.networkStats.captions.divisionAtc)).toBeInTheDocument();

  // Grouped by the language on screen, which is what an English reader expects of a four figure
  // number — and the reason this is not `toString()`.
  expect(screen.getByText('1,234')).toBeInTheDocument();
  expect(screen.getByText(englishCommon.blocks.networkStats.captions.divisionPilots)).toBeInTheDocument();
});

test('LiveStatusDegradesWhenIvaoIsDown: draws nothing at all rather than four zeroes', () => {
  // `updatedAt` of null is the server saying "I could not ask", which is a different thing from
  // nobody being connected. A strip that drew zeroes for it would be the site answering a question
  // it never got an answer to — and it sits on every public page, so it would do it everywhere.
  const { container } = renderWithProviders(<LiveStatusStrip status={{ updatedAt: null, figures: [] }} />);

  expect(container).toBeEmptyDOMElement();
  expect(screen.queryByText(englishCommon.liveStatus.title)).not.toBeInTheDocument();
});
