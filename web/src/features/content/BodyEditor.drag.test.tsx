import type * as DndKit from '@dnd-kit/core';
import type { DndContextProps, DragEndEvent } from '@dnd-kit/core';
import { act, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { Body } from '../../blocks';
import type { MediaLibraryQuery } from '../../shared/ui';
import { renderWithProviders } from '../../test/harness';

import { BodyEditor } from './BodyEditor';

/**
 * Dragging a section on the page, where a template locks it (review of #513, finding 1). A pointer drag means layout,
 * which jsdom has none of, so the drop is handed to the editor the way dnd-kit hands it: through the `onDragEnd` of
 * the page's one `DndContext`, caught here as it is drawn.
 */

const dropped: { onDragEnd?: (event: DragEndEvent) => void } = {};

vi.mock('@dnd-kit/core', async (original) => {
  const actual = await original<typeof DndKit>();
  const Real = actual.DndContext;
  return {
    ...actual,
    DndContext: (props: DndContextProps) => {
      dropped.onDragEnd = (event) => props.onDragEnd?.(event);
      return <Real {...props} />;
    },
  };
});

const editor = englishCommon.content.editor;

const framed = (id: string, sections: unknown[] = [], extra: object = {}) => ({
  id,
  key: null,
  title: { en: id },
  layout: 'stacked',
  background: 'none',
  padding: 'md',
  width: 'default',
  blocks: [],
  sections,
  ...extra,
});

const mediaLibrary = {
  queryKey: ['media', 'test'],
  queryFn: () => Promise.resolve({ items: [], page: 1, pageSize: 50, total: 0 }),
} as unknown as MediaLibraryQuery;

function drawLocked() {
  const onChange = vi.fn<(changed: Body) => void>();

  renderWithProviders(
    <BodyEditor
      initial={
        {
          schemaVersion: 1,
          sections: [framed('fixed', [framed('r1'), framed('r2')], { key: 'fixed' }), framed('free')],
        } as unknown as Body
      }
      onChange={onChange}
      toolbar={(tools) => <div role="toolbar">{tools}</div>}
      locales={['en']}
      division={{ defaultLocale: 'en', timezone: 'UTC' }}
      mediaLibrary={mediaLibrary}
      holds={() => false}
      template={{
        body: {
          schemaVersion: 1,
          sections: [framed('t1', [], { key: 'fixed', locked: true })],
        } as unknown as Body,
        title: { en: 'Template' },
        department: 'WD',
        canManage: false,
      }}
    />,
  );

  return onChange;
}

const sectionDrop = (from: string, onto: string) =>
  ({
    active: { id: from, data: { current: { kind: 'section' } } },
    over: { id: onto, data: { current: { kind: 'section' } } },
  }) as unknown as DragEndEvent;

test('a section or a row a template locks has no grip on the page, as in the outline', async () => {
  const user = userEvent.setup();
  drawLocked();

  for (const picked of ['1 · fixed', 'Row 1.1 · r1']) {
    await user.click(screen.getByText(picked));
    expect(screen.queryByRole('button', { name: editor.reorder })).not.toBeInTheDocument();
  }

  // A free section keeps its own.
  await user.click(screen.getByText('2 · free'));
  expect(screen.getByRole('button', { name: editor.reorder })).toBeInTheDocument();
});

test('a drop does not reorder what a template locks, and does reorder what it leaves free', () => {
  const onChange = drawLocked();

  // The rows of a locked section, and the locked section itself.
  act(() => dropped.onDragEnd?.(sectionDrop('r1', 'r2')));
  act(() => dropped.onDragEnd?.(sectionDrop('fixed', 'free')));
  expect(onChange).not.toHaveBeenCalled();

  // A free section dropped onto its sibling moves, as the arrows would move it.
  act(() => dropped.onDragEnd?.(sectionDrop('free', 'fixed')));
  expect(onChange.mock.lastCall?.[0].sections.map((section) => section.id)).toEqual(['free', 'fixed']);
});
