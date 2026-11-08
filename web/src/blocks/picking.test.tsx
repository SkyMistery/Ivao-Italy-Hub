import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import type { Body } from './envelope';
import { PickingContext, useLinksDisabled, type Picking, type SortableBinding } from './picking';

/**
 * Composing on the page, and the promise that keeps it safe.
 *
 * There is **one** renderer for the public site and for the editor, so the interactivity that lets
 * somebody compose by clicking has exactly one way of being acceptable: it must not exist at all
 * where a visitor reads. That is what the first test is for, and it is the one that must never be
 * relaxed — the second is only the feature.
 */

/** An editing context, with only what the test is about filled in. */
const editing = (overrides: Partial<Picking> = {}): Picking => ({
  selected: null,
  onPick: vi.fn(),
  target: null,
  onPickColumn: vi.fn(),
  accepts: () => true,
  ...overrides,
});

const body: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's1',
      key: 'hero',
      title: { en: 'Hero', it: 'Hero' },
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      mediaId: null,
      required: null,
      locked: null,
      allowedBlocks: null,
      blocks: [
        {
          id: 'b1',
          type: 'cta',
          version: 1,
          renderMode: null,
          frozen: null,
          column: 0,
          source: null,
          span: null,
          props: {
            title: { en: 'Join us', it: 'Unisciti' },
            text: { en: 'Come and fly', it: 'Vieni a volare' },
            href: 'https://example.org/join',
            label: { en: 'Join', it: 'Iscriviti' },
          },
        },
      ],
      sections: [],
    },
  ],
};

test('the page a visitor reads has nothing to click and nothing to strip', () => {
  renderWithProviders(<ContentRenderer body={body} />);

  // No provider, so no wrapper, no handler, no attribute. Asserted on the attribute because it is
  // the only trace the editing shell leaves in the document: if it is absent, so is the rest.
  expect(document.querySelectorAll('[data-pickable]')).toHaveLength(0);

  // And the link inside the block is still a link that goes somewhere, which is the half a broken
  // capture handler would take away without anybody noticing until a visitor clicked it.
  const link = screen.getByRole('link', { name: 'Join' });
  expect(link).toHaveAttribute('href', 'https://example.org/join');

  const clicked = new MouseEvent('click', { bubbles: true, cancelable: true });
  link.dispatchEvent(clicked);
  expect(clicked.defaultPrevented).toBe(false);
});

test('a page looked at in a tab of its own follows no link, and picks nothing', async () => {
  const user = userEvent.setup();

  function LookedAt() {
    useLinksDisabled();
    return <ContentRenderer body={body} />;
  }

  renderWithProviders(<LookedAt />);

  // Read-only and not composing: nothing of the editor's shell is drawn.
  expect(document.querySelectorAll('[data-pickable]')).toHaveLength(0);

  const link = screen.getByRole('link', { name: 'Join' });
  const clicked = new MouseEvent('click', { bubbles: true, cancelable: true });
  link.dispatchEvent(clicked);
  expect(clicked.defaultPrevented).toBe(true);

  // Taken in the capture phase of the document, so that nothing under it hears the click either.
  const heard = vi.fn();
  link.addEventListener('click', heard);
  await user.click(link);
  expect(heard).not.toHaveBeenCalled();
});

test('with the editor behind it, a click picks the block instead of following it', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPick: picked })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  await user.click(screen.getByRole('link', { name: 'Join' }));

  // ⚠️ The block, not the section: the click was captured at the block and stopped there. And the
  // link did not fire — a call to action that carried whoever is composing out of the editor, with
  // unsaved changes, would be worse than no picking at all.
  expect(picked).toHaveBeenCalledTimes(1);
  expect(picked).toHaveBeenCalledWith('block', 'b1');
});

test('the space around the blocks picks the section', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPick: picked })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  const section = document.querySelector('[data-pickable="section"]');
  expect(section).not.toBeNull();

  await user.click(section!);

  expect(picked).toHaveBeenCalledWith('section', 's1');
});

/**
 * The structure of a section, drawn while composing (Carmine, 11 September 2026, with the page
 * builder of va.ivao.aero in front of him: "you see clearly how it is divided — the drop here in the
 * empty areas").
 */
const halves: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's2',
      key: 'split',
      title: { en: 'Split', it: 'Diviso' },
      layout: '1/2+1/2',
      background: 'none',
      padding: 'md',
      width: 'default',
      mediaId: null,
      required: null,
      locked: null,
      allowedBlocks: null,
      blocks: [],
      sections: [],
    },
  ],
};

test('while composing, every empty column says where a component would go, and choosing one says which', async () => {
  const user = userEvent.setup();
  const chosen = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPickColumn: chosen })}>
      <ContentRenderer body={halves} />
    </PickingContext.Provider>,
  );

  // Two columns, two invitations: a section in halves reads as halves before anything is in it.
  const invitations = screen.getAllByRole('button', { name: '+ Add here' });
  expect(invitations).toHaveLength(2);

  await user.click(invitations[1]!);

  // The second column — which is the whole point: before this, a component always landed in the
  // first one and had to be moved.
  expect(chosen).toHaveBeenCalledWith('s2', 1);
});

test('a section a template locks does not invite a component it would refuse', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ accepts: () => false })}>
      <ContentRenderer body={halves} />
    </PickingContext.Provider>,
  );

  expect(screen.queryByRole('button', { name: '+ Add here' })).not.toBeInTheDocument();
});

test('a visitor sees none of it: an empty column is simply empty', () => {
  renderWithProviders(<ContentRenderer body={halves} />);

  expect(screen.queryByRole('button', { name: '+ Add here' })).not.toBeInTheDocument();
  expect(document.querySelectorAll('[data-pickable]')).toHaveLength(0);
});

/** What the editor hands over as a place to drop onto: here, a marker that says where it stands. */
function Slot({ section, column, index }: { section: string; column: number; index: number }) {
  return <div data-slot={`${section}:${column}:${index}`} />;
}

test('a place to drop onto stands before every block and after the last, only while composing', () => {
  // One block in `body`: a place before it and one after it. The renderer draws the component the
  // editor hands over and knows nothing else about dropping — dnd-kit is never imported here.
  renderWithProviders(
    <PickingContext.Provider value={editing({ DropZone: Slot })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect([...document.querySelectorAll('[data-slot]')].map((slot) => slot.getAttribute('data-slot'))).toEqual(
    ['s1:0:0', 's1:0:1'],
  );
});

test('a section a template locks offers no place to drop onto either', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ DropZone: Slot, accepts: () => false })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(document.querySelectorAll('[data-slot]')).toHaveLength(0);
});

test('a visitor gets no place to drop onto, because there is nothing to drop', () => {
  renderWithProviders(<ContentRenderer body={body} />);

  expect(document.querySelectorAll('[data-slot]')).toHaveLength(0);
});

test('the picked block carries what may be done to it, and a press there does not re-pick', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();
  const removed = vi.fn();

  renderWithProviders(
    <PickingContext.Provider
      value={editing({
        selected: 'b1',
        onPick: picked,
        actions: ({ kind, id }) => (kind === 'block' && id === 'b1' ? [{ key: 'remove', run: removed }] : []),
      })}
    >
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  // The bar names the block — by the label the palette uses — and offers exactly what the editor
  // answered: one command, not a menu.
  expect(document.querySelector('[data-chrome]')).toHaveTextContent('Button');
  await user.click(screen.getByRole('button', { name: 'Remove' }));

  expect(removed).toHaveBeenCalledTimes(1);
  // The block's own capture handler lets the bar through: pressing "remove" is not a click on the
  // block, and must not pick the section the block was in either.
  expect(picked).not.toHaveBeenCalled();
});

test('a section is offered at the end of the page while composing, and to nobody else', async () => {
  const user = userEvent.setup();
  const added = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onAddSection: added })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  await user.click(screen.getByRole('button', { name: 'Add a section' }));
  expect(added).toHaveBeenCalledTimes(1);
});

test('the invitation to add a section stands after the container the sections measure, not inside it', () => {
  // Inside it, a browser could leave the whole preview without a box (`ContentRenderer`): what jsdom
  // can keep is where the invitation stands.
  renderWithProviders(
    <PickingContext.Provider value={editing({ onAddSection: vi.fn() })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  const invitation = screen.getByRole('button', { name: 'Add a section' });
  const container = document.querySelector('[data-pickable="section"]')?.parentElement;

  expect(container).toHaveClass('@container');
  expect(container).not.toContainElement(invitation);
});

test('a picked section is drawn through what makes it draggable, with a grip on its bar', () => {
  // What the editor hands over, faked: a group that marks its list, and an item that hands back a
  // node ref, a style and a handle — the renderer attaches all three and asks nothing about drag.
  const Group = ({ ids, children }: { ids: readonly string[]; children: React.ReactNode }) => (
    <div data-group={ids.join(',')}>{children}</div>
  );
  const Item = ({ id, children }: { id: string; children: (s: SortableBinding) => React.ReactNode }) => (
    <>
      {children({
        setNodeRef: () => {},
        style: { opacity: 0.5 },
        handle: { attach: () => {}, listeners: { 'data-handle': id } },
      })}
    </>
  );

  renderWithProviders(
    <PickingContext.Provider value={editing({ selected: 's1', SortableGroup: Group, Sortable: Item })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(document.querySelector('[data-group]')).toHaveAttribute('data-group', 's1');
  expect(document.querySelector('[data-pickable="section"]')).toHaveStyle({ opacity: '0.5' });
  expect(screen.getByRole('button', { name: 'Drag to reorder' })).toHaveAttribute('data-handle', 's1');
});

test('a picked block is drawn through what makes it draggable, and a block nothing may be done to gets no grip', () => {
  const Item = ({ id, children }: { id: string; children: (s: SortableBinding) => React.ReactNode }) => (
    <>
      {children({
        setNodeRef: () => {},
        style: { opacity: 0.5 },
        handle: { attach: () => {}, listeners: { 'data-handle': id } },
      })}
    </>
  );

  const { unmount } = renderWithProviders(
    <PickingContext.Provider
      value={editing({
        selected: 'b1',
        BlockDraggable: Item,
        actions: () => [{ key: 'remove', run: () => {} }],
      })}
    >
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(screen.getByRole('button', { name: 'Drag to reorder' })).toHaveAttribute('data-handle', 'b1');
  unmount();

  // The editor answers no commands — a locked section — so the block is not dragged either.
  renderWithProviders(
    <PickingContext.Provider value={editing({ selected: 'b1', BlockDraggable: Item, actions: () => [] })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(screen.queryByRole('button', { name: 'Drag to reorder' })).not.toBeInTheDocument();
});

test('a block nothing is written in is drawn as a placeholder, and a visitor never sees one', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ blank: (block) => (block.id === 'b1' ? 'Button' : null) })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  // In its place, not beside it: what the block would have drawn is not there.
  expect(screen.getByText('Button — nothing written yet. Fill it in on the right.')).toBeVisible();
  expect(screen.queryByRole('link', { name: 'Join' })).not.toBeInTheDocument();
});

test('a double click opens a block, and the picked one is marked for the editor to scroll to', async () => {
  const user = userEvent.setup();
  const opened = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ selected: 'b1', onOpen: opened })}>
      <ContentRenderer body={body} />
    </PickingContext.Provider>,
  );

  expect(document.querySelector('[data-picked]')).toHaveAttribute('data-pickable', 'block');

  await user.dblClick(screen.getByRole('link', { name: 'Join' }));
  expect(opened).toHaveBeenCalledWith('block', 'b1');
});

test('a visitor is offered no section and sees no bar', () => {
  renderWithProviders(<ContentRenderer body={body} />);

  expect(screen.queryByRole('button', { name: 'Add a section' })).not.toBeInTheDocument();
  expect(document.querySelectorAll('[data-chrome]')).toHaveLength(0);
  expect(screen.queryByText(/nothing written yet/u)).not.toBeInTheDocument();
});

/** A section around nothing, for the tree below. */
const framed = (id: string, title: string, sections: Body['sections'] = []): Body['sections'][number] => ({
  id,
  key: null,
  title: { en: title, it: title },
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  mediaId: null,
  required: null,
  locked: null,
  allowedBlocks: null,
  blocks: [],
  sections,
});

/** Two sections; the second, on a pale blue ground, has two rows, and the first of them no name. */
const tree: Body = {
  schemaVersion: 1,
  sections: [
    framed('s1', 'Opening'),
    {
      ...framed('s2', 'Rules', [framed('r1', ''), framed('r2', 'Annex')]),
      background: 'accent',
    },
  ],
};

const labels = () => [...document.querySelectorAll('[data-section-label]')].map((label) => label.textContent);

test('while composing every section says its number, its name and its ground, and a row says it is one', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing()}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  expect(labels()).toEqual(['1 · Opening', '2 · Rules · pale blue background', 'Row 2.1', 'Row 2.2 · Annex']);
});

test('a section with no name is said to have none', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing()}>
      <ContentRenderer body={{ schemaVersion: 1, sections: [framed('s1', '')] }} />
    </PickingContext.Provider>,
  );

  expect(labels()).toEqual(['1 · Section without a name']);
});

test('a visitor reads no label and no outline, and nothing to insert', () => {
  renderWithProviders(<ContentRenderer body={tree} />);

  expect(document.querySelectorAll('[data-section-label]')).toHaveLength(0);
  expect(document.querySelectorAll('[data-insert-gap]')).toHaveLength(0);
  expect(document.querySelector('section')?.className).not.toContain('outline');
  expect(screen.queryByText(/Opening|Rules|Annex/u)).not.toBeInTheDocument();
});

test('every section has an outline, dashed until it is the one picked', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing({ selected: 's2' })}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  const [first, second] = document.querySelectorAll('[data-pickable="section"]');
  expect(first).toHaveClass('outline-dashed');
  expect(second).not.toHaveClass('outline-dashed');
  expect(second).toHaveClass('outline-primary');
});

test('"+ section" inserts at the place it stands, among the sections of the page', async () => {
  const user = userEvent.setup();
  const added = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onAddSection: added })}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  // One before each section of the page: no actions answered here, so no row is offered.
  const inserts = screen.getAllByRole('button', { name: '+ section' });
  expect(inserts).toHaveLength(2);
  expect(screen.queryByRole('button', { name: '+ row' })).not.toBeInTheDocument();
  expect(inserts[1]).toHaveAccessibleDescription('Insert a section here: it becomes number 2');

  await user.click(inserts[1]!);

  expect(added).toHaveBeenCalledTimes(1);
  expect(added).toHaveBeenCalledWith(undefined, 1, false);

  // The same place from the keyboard, and the editor is told: the cursor is the editor's to move.
  inserts[1]!.focus();
  await user.keyboard('{Enter}');
  expect(added).toHaveBeenLastCalledWith(undefined, 1, true);
});

test('the places to insert wait for the typefaces, and the labels do not', async () => {
  // Drawn with the page, they leave a slow machine's preview without a box (`useInsert`): what jsdom can
  // keep is when they arrive. `preview.spec.ts` measures the fault itself.
  let finish = () => undefined as void;
  const ready = new Promise<void>((resolve) => {
    finish = resolve;
  });
  Object.defineProperty(document, 'fonts', { configurable: true, value: { ready } });

  try {
    renderWithProviders(
      <PickingContext.Provider value={editing({ onAddSection: vi.fn() })}>
        <ContentRenderer body={tree} />
      </PickingContext.Provider>,
    );

    expect(labels()).toHaveLength(4);
    expect(document.querySelectorAll('[data-insert-gap]')).toHaveLength(0);
    // The invitation stands outside what the sections measure, so it has nothing to wait for.
    expect(screen.getByRole('button', { name: 'Add a section' })).toBeInTheDocument();

    finish();

    expect(await screen.findAllByRole('button', { name: '+ section' })).toHaveLength(2);
  } finally {
    Reflect.deleteProperty(document, 'fonts');
  }
});

test('"+ row" stands in the order of the page, and at the end of a section of the page, never of a row', () => {
  renderWithProviders(
    <PickingContext.Provider
      value={editing({
        onAddSection: vi.fn(),
        // Every section and every row takes a row here: the end of a row is still not offered.
        actions: () => [{ key: 'addRow', run: () => {} }],
      })}
    >
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  // In document order, which is the order of the keyboard: the end of the second section after its rows.
  expect(screen.getAllByRole('button', { name: '+ row' }).map((insert) => insert.title)).toEqual([
    'Insert a row here: it becomes number 1.1',
    'Insert a row here: it becomes number 2.1',
    'Insert a row here: it becomes number 2.2',
    'Insert a row here: it becomes number 2.3',
  ]);
});

test('what a section holds stands above the places to insert while composing, and a visitor has neither', () => {
  const { unmount } = renderWithProviders(
    <PickingContext.Provider value={editing({ onAddSection: vi.fn() })}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  // The geometry is the browser's (`editor.spec.ts` clicks it): what jsdom can keep is the layers.
  expect(document.querySelector('[data-insert-gap]')).toHaveClass('z-0');
  const content = document.querySelector('[data-pickable="section"] > div.flex:not([data-chrome])');
  expect(content).toHaveClass('relative', 'z-[1]');
  unmount();

  renderWithProviders(<ContentRenderer body={tree} />);
  expect(document.querySelector('section > div.flex')).not.toHaveClass('z-[1]');
});

test('"+ row" inserts among the rows of a section, and at their end, where the editor offers a row', async () => {
  const user = userEvent.setup();
  const added = vi.fn();
  const picked = vi.fn();

  renderWithProviders(
    <PickingContext.Provider
      value={editing({
        onAddSection: added,
        onPick: picked,
        // A row in the second section only; the rows themselves are as deep as they go.
        actions: ({ id }) => (id === 's2' ? [{ key: 'addRow', run: () => {} }] : []),
      })}
    >
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  const inserts = screen.getAllByRole('button', { name: '+ row' });
  expect(inserts.map((insert) => insert.getAttribute('title'))).toEqual([
    'Insert a row here: it becomes number 2.1',
    'Insert a row here: it becomes number 2.2',
    'Insert a row here: it becomes number 2.3',
  ]);

  await user.click(inserts[1]!);
  expect(added).toHaveBeenLastCalledWith('s2', 1, false);

  await user.click(inserts[2]!);
  expect(added).toHaveBeenLastCalledWith('s2', undefined, false);

  // A press on the button is not a click on the section it stands in.
  expect(picked).not.toHaveBeenCalled();
});

test('nothing is offered where nothing may be added', () => {
  renderWithProviders(
    <PickingContext.Provider value={editing()}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  expect(document.querySelectorAll('[data-insert-gap]')).toHaveLength(0);
});

test('a row is picked by its own air and by its label, and not the section around it', async () => {
  const user = userEvent.setup();
  const picked = vi.fn();
  const opened = vi.fn();

  renderWithProviders(
    <PickingContext.Provider value={editing({ onPick: picked, onOpen: opened })}>
      <ContentRenderer body={tree} />
    </PickingContext.Provider>,
  );

  const label = screen.getByText('Row 2.2 · Annex');
  // The row's element itself, which is what a click between its children lands on.
  const air = label.closest<HTMLElement>('[data-pickable="section"]')!;

  await user.click(air);
  expect(picked).toHaveBeenCalledTimes(1);
  expect(picked).toHaveBeenLastCalledWith('section', 'r2');

  await user.click(label);
  expect(picked).toHaveBeenCalledTimes(2);
  expect(picked).toHaveBeenLastCalledWith('section', 'r2');

  await user.dblClick(air);
  expect(opened).toHaveBeenCalledTimes(1);
  expect(opened).toHaveBeenCalledWith('section', 'r2');
});
