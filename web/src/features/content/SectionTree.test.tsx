import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { registry } from '../../app/registry';
import type { BlockEnvelope, Body, SectionEnvelope } from '../../blocks';
import { renderWithProviders } from '../../test/harness';

import { SectionTree } from './SectionTree';
import type { SectionRule } from './templateRules';

/**
 * The outline of the editor, and the one thing about it that a picture cannot show: that it still
 * works without a mouse.
 *
 * G11 put dnd-kit **above** the arrows rather than in their place (design M1 §9.3). Dragging is a
 * pointer and nothing else — no keyboard, no screen reader, no touch worth the name — so the arrows
 * are not a leftover to tidy away, they are the whole of the reordering for anybody who cannot use
 * a mouse. This file is what fails when somebody tidies them away.
 */

const editor = englishCommon.content.editor;

const block = (id: string, type: string): BlockEnvelope => ({
  id,
  type,
  version: 1,
  props: {},
  renderMode: null,
  frozen: null,
  column: 0,
});

const section = (id: string, extra: Partial<SectionEnvelope> = {}): SectionEnvelope => ({
  id,
  key: id,
  title: { en: id },
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: [],
  sections: [],
  ...extra,
});

const locked: SectionRule = { required: true, locked: true, allowedBlocks: null };

const noRules: ReadonlyMap<string, SectionRule> = new Map();

/** A type the registry really has, so the row is drawn the way the editor draws it. */
const someBlockType = registry.blocks[0]!.type;

function draw(body: Body, rules: ReadonlyMap<string, SectionRule> = noRules) {
  const moves = { section: vi.fn(), block: vi.fn() };

  renderWithProviders(
    <SectionTree
      body={body}
      rules={rules}
      selection={null}
      onSelect={vi.fn()}
      onAddSection={vi.fn()}
      onMoveSection={moves.section}
      onMoveBlock={moves.block}
      onReorderSections={vi.fn()}
      onReorderBlocks={vi.fn()}
      onDuplicateBlock={vi.fn()}
      onRemoveSection={vi.fn()}
      onRemoveBlock={vi.fn()}
    />,
  );

  return moves;
}

/**
 * Tabs forward until that control has the focus. Deliberately not `element.focus()`: what is being
 * asserted is that a person moving through the panel with the keyboard *arrives* here, which is a
 * different claim, and the one that fails when a row stops being a button.
 */
async function tabTo(user: ReturnType<typeof userEvent.setup>, target: HTMLElement): Promise<void> {
  for (let step = 0; step < 20 && document.activeElement !== target; step += 1) {
    await user.tab();
  }

  expect(document.activeElement, 'never reached by tabbing').toBe(target);
}

test('a section is still reordered from the keyboard, with no pointer anywhere', async () => {
  const user = userEvent.setup();
  const moves = draw({ schemaVersion: 1, sections: [section('one'), section('two')] });

  const down = screen.getAllByRole('button', { name: editor.moveDown })[0]!;

  await tabTo(user, down);
  await user.keyboard('{Enter}');

  expect(moves.section).toHaveBeenCalledWith('one', 1);
});

test('a block is still reordered from the keyboard', async () => {
  const user = userEvent.setup();
  const moves = draw({
    schemaVersion: 1,
    sections: [section('one', { blocks: [block('b1', someBlockType), block('b2', someBlockType)] })],
  });

  // The second of the two, because the first has no "up" to go to and a call with no effect would
  // still satisfy a looser assertion.
  const up = screen.getAllByRole('button', { name: editor.moveUp })[2]!;

  await tabTo(user, up);
  await user.keyboard('{Enter}');

  expect(moves.block).toHaveBeenCalledWith('b2', -1);
});

test('what can be dragged can also be moved with the arrows, and the other way round', () => {
  draw({
    schemaVersion: 1,
    sections: [section('one', { blocks: [block('b1', someBlockType)] }), section('two')],
  });

  // Two sections and one block: three rows, three handles, three pairs of arrows. A row that grew
  // a handle without arrows is a row only a mouse can move.
  expect(screen.getAllByRole('button', { name: editor.reorder })).toHaveLength(3);
  expect(screen.getAllByRole('button', { name: editor.moveUp })).toHaveLength(3);
  expect(screen.getAllByRole('button', { name: editor.moveDown })).toHaveLength(3);
});

test('a locked section offers neither the arrows nor the handle', () => {
  draw(
    { schemaVersion: 1, sections: [section('fixed', { blocks: [block('b1', someBlockType)] })] },
    new Map([['fixed', locked]]),
  );

  // What a locked section allows is the properties of its blocks and nothing else, so neither way
  // of restructuring it is on screen — not one of them disabled and the other left open.
  expect(screen.queryAllByRole('button', { name: editor.reorder })).toHaveLength(0);
  expect(screen.queryAllByRole('button', { name: editor.moveUp })).toHaveLength(0);
  expect(screen.getByLabelText(editor.locked)).toBeInTheDocument();
});
