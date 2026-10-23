import { render } from '@testing-library/react';
import { expect, test } from 'vitest';

import { MarkdownContent } from './MarkdownContent';

/**
 * Prose drawn again is the same prose, not a new copy of it (issue #351).
 *
 * The elements the library draws used to be handed over as new functions at every render, which
 * React reads as new components: each paragraph was taken out of the document and put back whenever
 * anything above it drew. In the editor's preview that is every keystroke and every drag, and
 * Chromium, asked by the drag and drop to measure the page in between, left the whole preview
 * without a box (`e2e/full/drop.spec.ts`).
 */
test('drawing the same prose again keeps the elements it drew', () => {
  const { container, rerender } = render(
    <MarkdownContent source={'A paragraph with a [link](https://example.com).\n\n- and a list'} />,
  );
  const drawn = [...container.querySelectorAll('p, a, ul, li')];
  expect(drawn).toHaveLength(4);

  rerender(<MarkdownContent source={'A paragraph with a [link](https://example.com).\n\n- and a list'} />);

  expect([...container.querySelectorAll('p, a, ul, li')]).toEqual(drawn);
  for (const element of drawn) {
    expect(element.isConnected).toBe(true);
  }
});
