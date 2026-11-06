import { fireEvent, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { PanelColumns } from './PanelSplitter';

/**
 * The two lines between the columns of the editor, as a screen reader and a keyboard meet them, and what the browser
 * remembers of them. A drag, and the page that keeps its minimum, need real geometry: `e2e/editor.spec.ts` measures those.
 */

const words = englishCommon.content.editor.panelWidth;
const STORAGE_KEY = 'hub.content.editor.panels';

function draw() {
  renderWithProviders(
    <PanelColumns palette={<div>palette</div>} page={<div>page</div>} properties={<div>properties</div>} />,
  );

  return {
    palette: screen.getByRole('separator', { name: words.palette }),
    properties: screen.getByRole('separator', { name: words.properties }),
  };
}

const widthsOf = () => JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as unknown;

beforeEach(() => localStorage.clear());
afterEach(() => vi.restoreAllMocks());

test('each line is a separator that says the width of its panel, and within what', () => {
  const { palette, properties } = draw();

  expect(palette).toHaveAttribute('aria-orientation', 'vertical');
  expect(palette).toHaveAttribute('aria-valuenow', '13');
  expect(palette).toHaveAttribute('aria-valuemin', '10');
  expect(palette).toHaveAttribute('aria-valuemax', '20');

  expect(properties).toHaveAttribute('aria-valuenow', '19');
  expect(properties).toHaveAttribute('aria-valuemin', '16');
  expect(properties).toHaveAttribute('aria-valuemax', '36');
});

test('the arrows move the line: to the right widens the first panel and narrows the second', async () => {
  const user = userEvent.setup();
  const { palette, properties } = draw();

  palette.focus();
  await user.keyboard('{ArrowRight}{ArrowRight}{ArrowLeft}');
  expect(palette).toHaveAttribute('aria-valuenow', '14');

  properties.focus();
  await user.keyboard('{ArrowRight}');
  expect(properties).toHaveAttribute('aria-valuenow', '18');

  await user.keyboard('{ArrowLeft}{ArrowLeft}');
  expect(properties).toHaveAttribute('aria-valuenow', '20');

  // Each panel moved by its own line, and what is remembered is the last of both.
  expect(widthsOf()).toEqual({ palette: 14, properties: 20 });
});

test('the arrows stop at the limits', async () => {
  const user = userEvent.setup();
  const { palette } = draw();

  palette.focus();
  await user.keyboard('{ArrowLeft>10/}');
  expect(palette).toHaveAttribute('aria-valuenow', '10');

  await user.keyboard('{ArrowRight>20/}');
  expect(palette).toHaveAttribute('aria-valuenow', '20');
});

test('a double click puts its own panel back, and the other stays', async () => {
  const user = userEvent.setup();
  const { palette, properties } = draw();

  palette.focus();
  await user.keyboard('{ArrowRight}');
  properties.focus();
  await user.keyboard('{ArrowLeft}');

  fireEvent.doubleClick(palette);

  expect(palette).toHaveAttribute('aria-valuenow', '13');
  expect(properties).toHaveAttribute('aria-valuenow', '20');
  expect(widthsOf()).toEqual({ palette: 13, properties: 20 });
});

test('the widths come back from the browser, held to the limits', () => {
  localStorage.setItem(STORAGE_KEY, JSON.stringify({ palette: 15, properties: 99 }));

  const { palette, properties } = draw();

  expect(palette).toHaveAttribute('aria-valuenow', '15');
  expect(properties).toHaveAttribute('aria-valuenow', '36');
});

test.each([
  ['not JSON', '{'],
  ['another shape', '{"palette":"wide"}'],
])('what the browser kept is ignored when it is %s', (_, kept) => {
  localStorage.setItem(STORAGE_KEY, kept);

  expect(draw().palette).toHaveAttribute('aria-valuenow', '13');
});

test('a browser that refuses the storage still has panels that move', async () => {
  const user = userEvent.setup();
  vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
    throw new Error('refused');
  });
  vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
    throw new Error('refused');
  });

  const { palette } = draw();
  expect(palette).toHaveAttribute('aria-valuenow', '13');

  palette.focus();
  await user.keyboard('{ArrowRight}');
  expect(palette).toHaveAttribute('aria-valuenow', '14');
});
