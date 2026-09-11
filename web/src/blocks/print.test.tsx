import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { AccordionBlock, TabsBlock } from './blocks';
import { PrintContext } from './print';

/**
 * On paper there is nothing to click (G14): the blocks that hide part of themselves behind a
 * gesture draw every panel while `PrintContext` says the page is being printed, and only then.
 */

const en = (value: string) => ({ en: value, it: value });

test('tabs and accordions unfold for paper, and fold again on screen', () => {
  const tabs = {
    tabs: [
      { label: en('Pilots'), body: en('What a pilot reads.') },
      { label: en('Controllers'), body: en('What a controller reads.') },
    ],
  };
  const accordion = {
    items: [
      { question: en('When?'), answer: en('At the top of the hour.') },
      { question: en('Where?'), answer: en('On the ground frequency.') },
    ],
  };

  const { unmount } = renderWithProviders(
    <PrintContext.Provider value={true}>
      <TabsBlock props={tabs} />
      <AccordionBlock props={accordion} />
    </PrintContext.Provider>,
  );

  expect(screen.getByText('What a pilot reads.')).toBeVisible();
  expect(screen.getByText('What a controller reads.')).toBeVisible();
  expect(screen.getByText('At the top of the hour.')).toBeVisible();
  expect(screen.getByText('On the ground frequency.')).toBeVisible();
  unmount();

  renderWithProviders(
    <>
      <TabsBlock props={tabs} />
      <AccordionBlock props={accordion} />
    </>,
  );

  // The second tab and every answer are behind a gesture again.
  expect(screen.queryByText('What a controller reads.')).not.toBeInTheDocument();
  expect(screen.queryByText('At the top of the hour.')).not.toBeInTheDocument();
});
