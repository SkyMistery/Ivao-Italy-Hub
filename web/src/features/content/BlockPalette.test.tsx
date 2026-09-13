import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { registry } from '../../app/registry';
import { BLOCK_GROUPS } from '../../shared/modules';
import { renderWithProviders } from '../../test/harness';

import { BlockPalette } from './BlockPalette';
import { groupsOf } from './palette';
import type { SectionRule } from './templateRules';

/**
 * The bar of components on the left of the editor.
 *
 * What it has to keep true is not how it looks: it is that the palette **is** the registry — every
 * registered block reachable from it, in a drawer that opens and shuts — and that it never adds a
 * block where the template said it must not.
 */

const editor = englishCommon.content.editor;

const FREE: SectionRule = { required: false, locked: false, allowedBlocks: null };

const target = { id: 's1', name: 'Intro' };

function render(props: Partial<Parameters<typeof BlockPalette>[0]> = {}) {
  const onAdd = vi.fn();

  const { unmount } = renderWithProviders(
    <BlockPalette target={target} rule={FREE} onAdd={onAdd} {...props} />,
  );

  return { onAdd, unmount };
}

test('every registered block is reachable from the palette', () => {
  const arranged = groupsOf(registry.blocks);

  const reachable = arranged.flatMap((group) => [
    ...group.blocks,
    ...group.subgroups.flatMap((subgroup) => subgroup.blocks),
  ]);

  // Not "as many as": the same ones. A block that fell out of every drawer is a block a
  // coordinator cannot add and nobody would notice until they went looking for it.
  expect(new Set(reachable.map((block) => block.type))).toEqual(
    new Set(registry.blocks.map((block) => block.type)),
  );
});

test('the drawers are in the order the groups are declared, not the order blocks were written', () => {
  const arranged = groupsOf(registry.blocks);

  const declared = BLOCK_GROUPS.filter((group) => registry.blocks.some((block) => block.group === group));

  expect(arranged.map((group) => group.group)).toEqual([...declared]);
});

test('a word typed finds a component by its name, and empties the drawers that have none', async () => {
  const user = userEvent.setup();
  render();

  await user.type(screen.getByRole('searchbox', { name: editor.searchComponents }), 'pict');

  // The picture stays, the heading goes, and the drawer the picture lives in is open whatever it
  // was before — a match inside a shut drawer would be a match nobody sees.
  expect(screen.getByRole('button', { name: englishCommon.blocks.image.label })).toBeVisible();
  expect(screen.queryByRole('button', { name: englishCommon.blocks.heading.label })).not.toBeInTheDocument();

  await user.clear(screen.getByRole('searchbox', { name: editor.searchComponents }));
  await user.type(screen.getByRole('searchbox', { name: editor.searchComponents }), 'zzz');
  expect(screen.getByText(editor.noComponentMatches.replace('{{query}}', 'zzz'))).toBeVisible();
});

test('a group can be collapsed and opened again', async () => {
  const user = userEvent.setup();
  render();

  const content = englishCommon.blocks.groups.content;

  // Open to begin with: a palette that starts shut hides the thing it exists to show.
  expect(screen.getByRole('button', { name: englishCommon.blocks.heading.label })).toBeVisible();

  await user.click(screen.getByRole('button', { name: content }));
  // ⚠️ Gone from the document, not merely hidden: a shut drawer of this accordion unmounts what
  // is inside it. Asserting invisibility would pass for an element that was never drawn at all.
  expect(screen.queryByRole('button', { name: englishCommon.blocks.heading.label })).toBeNull();

  await user.click(screen.getByRole('button', { name: content }));
  expect(screen.getByRole('button', { name: englishCommon.blocks.heading.label })).toBeVisible();
});

test('one button shuts every drawer and opens them all again', async () => {
  const user = userEvent.setup();
  render();

  await user.click(screen.getByRole('button', { name: editor.collapseAll }));
  // ⚠️ Gone from the document, not merely hidden: a shut drawer of this accordion unmounts what
  // is inside it. Asserting invisibility would pass for an element that was never drawn at all.
  expect(screen.queryByRole('button', { name: englishCommon.blocks.heading.label })).toBeNull();

  await user.click(screen.getByRole('button', { name: editor.expandAll }));
  expect(screen.getByRole('button', { name: englishCommon.blocks.heading.label })).toBeVisible();
});

test('clicking a component adds it to the section the palette says it will', async () => {
  const user = userEvent.setup();
  const { onAdd } = render();

  expect(screen.getByText(`Adds to: ${target.name}`)).toBeVisible();

  await user.click(screen.getByRole('button', { name: englishCommon.blocks.heading.label }));

  expect(onAdd).toHaveBeenCalledWith('heading');
});

test('with no section chosen it says so, and adds nothing', async () => {
  const user = userEvent.setup();
  const { onAdd } = render({ target: null });

  expect(screen.getByText(editor.componentsHint)).toBeVisible();

  const heading = screen.getByRole('button', { name: englishCommon.blocks.heading.label });
  expect(heading).toBeDisabled();

  await user.click(heading);
  expect(onAdd).not.toHaveBeenCalled();
});

test('a block the template forbids is disabled and says why, and the others still work', async () => {
  const user = userEvent.setup();
  const { onAdd } = render({ rule: { ...FREE, allowedBlocks: ['text'] } });

  const heading = screen.getByRole('button', { name: englishCommon.blocks.heading.label });
  expect(heading).toBeDisabled();
  expect(heading).toHaveAttribute('title', editor.notAllowedHere);

  await user.click(heading);
  expect(onAdd).not.toHaveBeenCalled();

  // ⚠️ Disabled and still **there**. The palette beside the page must not change shape every time
  // the selection moves, or it is a list nobody can learn; the outline's own palette filters,
  // because it is drawn inside the one section it adds to.
  await user.click(screen.getByRole('button', { name: englishCommon.blocks.text.label }));
  expect(onAdd).toHaveBeenCalledWith('text');
});

test('a subgroup is a drawer inside a drawer, and shuts on its own', async () => {
  const user = userEvent.setup();
  render();

  const media = englishCommon.blocks.subgroups.media;
  const image = englishCommon.blocks.image.label;

  expect(screen.getByRole('button', { name: image })).toBeVisible();

  await user.click(screen.getByRole('button', { name: media }));
  expect(screen.queryByRole('button', { name: image })).toBeNull();

  // And the group around it stayed open: shutting Media must not shut Content.
  expect(screen.getByRole('button', { name: englishCommon.blocks.heading.label })).toBeVisible();
});

test('a group with nothing in it is not drawn at all', () => {
  // What a fork gets when it registers no data block: no empty Data drawer to wonder about.
  const withoutData = registry.blocks.filter((block) => block.group !== 'data');

  expect(groupsOf(withoutData).map((group) => group.group)).not.toContain('data');
});

test('the drawers hold what the blocks say they hold', () => {
  const arranged = groupsOf(registry.blocks);
  const content = arranged.find((group) => group.group === 'content');

  const media = content?.subgroups.find((subgroup) => subgroup.subgroup === 'media');

  expect(media?.blocks.map((block) => block.type)).toEqual([
    'hero',
    'image',
    'video',
    'embed',
    'interactive',
  ]);

  const data = arranged.find((group) => group.group === 'data');
  expect(data?.blocks.map((block) => block.type)).toContain('networkStats');
});

test('a block behind a permission is in the list only for somebody who holds it', () => {
  const interactive = englishCommon.blocks.interactive.label;

  // ⚠️ Not in the list — where a block a *template* forbids is shown disabled with the reason on it.
  // The two are different facts: the template's refusal is about this section and worth saying, and
  // a permission is about the reader, so an entry they can never use is noise in a list they scan
  // all day (`BlockPalette.tsx`).
  const { unmount } = render({ holds: () => false });
  expect(screen.queryByRole('button', { name: interactive })).toBeNull();
  unmount();

  render({ holds: () => true });
  expect(screen.getByRole('button', { name: interactive })).toBeVisible();
});
