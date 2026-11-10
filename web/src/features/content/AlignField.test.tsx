import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { BlockEnvelope } from '../../blocks';
import { renderWithProviders } from '../../test/harness';

import { BlockProperties } from './BlockProperties';

/**
 * Where a block stands in its column, chosen in its panel beside "Section" (PR 3.9, design of the editor's phase 3,
 * §5.6): three icons, applied at once, written on the envelope. And the group of buttons that kept its alignment in its
 * props before: shown as the envelope's, and moved there by the first thing the panel writes — never by opening it.
 */

const words = englishCommon.content.editor.align;
const en = (value: string) => ({ en: value, it: value });

function blockOf(
  type: string,
  props: Record<string, unknown>,
  align: BlockEnvelope['align'] = null,
): BlockEnvelope {
  return {
    id: 'b_1',
    type,
    version: 1,
    props,
    renderMode: null,
    frozen: null,
    column: 0,
    source: null,
    span: null,
    align,
  };
}

function draw(block: BlockEnvelope) {
  const onEnvelope = vi.fn<(patch: Partial<BlockEnvelope>) => void>();
  const onApplyProps = vi.fn<(props: Record<string, unknown>) => void>();

  renderWithProviders(
    <BlockProperties
      block={block}
      section={{
        id: 's_1',
        layout: 'stacked',
        background: 'none',
        padding: 'md',
        width: 'default',
        blocks: [block],
        sections: [],
      }}
      sections={[{ value: 's_1', label: 'Intro' }]}
      locales={['it', 'en']}
      division={{ defaultLocale: 'it', timezone: 'Europe/Rome' }}
      mediaLibrary={{
        queryKey: ['media'],
        queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 20, total: 0 }),
      }}
      onApplyProps={onApplyProps}
      onEnvelope={onEnvelope}
      onMoveTo={vi.fn()}
    />,
  );

  return { onEnvelope, onApplyProps };
}

test('three icons say where the block stands, and a click writes the envelope at once', async () => {
  const user = userEvent.setup();
  const { onEnvelope } = draw(blockOf('heading', { level: 2, text: en('A heading') }));

  const group = screen.getByRole('group', { name: words.label });
  expect(group).toBeInTheDocument();
  expect(screen.getByRole('button', { name: words.options.left })).toHaveAttribute('aria-pressed', 'true');

  await user.click(screen.getByRole('button', { name: words.options.center }));
  expect(onEnvelope).toHaveBeenLastCalledWith({
    align: 'center',
    props: { level: 2, text: en('A heading') },
  });

  // The left is the absence, as the envelope says.
  await user.click(screen.getByRole('button', { name: words.options.left }));
  expect(onEnvelope).toHaveBeenLastCalledWith({ align: null, props: { level: 2, text: en('A heading') } });
});

test('a block that may not be aligned offers no choice', () => {
  draw(blockOf('table', { columns: [], rows: [] }));

  expect(screen.queryByRole('group', { name: words.label })).not.toBeInTheDocument();
});

test('a group of buttons written before shows its alignment, and opening it writes nothing', () => {
  const { onEnvelope, onApplyProps } = draw(blockOf('buttonGroup', { align: 'right', buttons: [] }));

  expect(screen.getByRole('button', { name: words.options.right })).toHaveAttribute('aria-pressed', 'true');
  // Opening a block never changes a page by itself (design §2, principle 4).
  expect(onEnvelope).not.toHaveBeenCalled();
  expect(onApplyProps).not.toHaveBeenCalled();
  // And the form has no alignment of its own any more: one would be a second place for the same thing.
  expect(
    screen.queryByText(englishCommon.content.editor.align.label, { selector: 'label' }),
  ).not.toBeInTheDocument();
});

test('its first write moves the old alignment onto the envelope, with the props being written', async () => {
  const user = userEvent.setup();
  const { onEnvelope, onApplyProps } = draw(
    blockOf('buttonGroup', {
      align: 'center',
      buttons: [{ label: en('Go'), href: '/go', variant: 'primary' }],
    }),
  );

  const label = screen.getAllByRole('textbox')[0]!;
  await user.clear(label);
  await user.type(label, 'Away');

  await vi.waitFor(() => expect(onEnvelope).toHaveBeenCalled());
  expect(onApplyProps).not.toHaveBeenCalled();

  const written = onEnvelope.mock.calls.at(-1)![0];
  expect(written.align).toBe('center');
  expect(written.props).not.toHaveProperty('align');
  expect(written.props?.buttons).toHaveLength(1);
});
