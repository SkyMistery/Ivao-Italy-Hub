import { useDraggable } from '@dnd-kit/core';
import type { ReactNode } from 'react';

import type { SortableBinding } from '../../blocks';

import type { BlockDrag } from './DropZone';

/**
 * A block dragged from where it stands to any slot of the page (Carmine, 11 September 2026: "the
 * elements in a section too, and between sections"). Drawn through `Picking.BlockDraggable`, so
 * the renderer never sees dnd-kit; it lands on the same slots a palette entry lands on, and the
 * editor's one `DndContext` moves it (`moveBlockTo`).
 *
 * Not sortable: the places it may go are the slots, which are droppables of their own, and a
 * sortable list of blocks would only ever know one column.
 */
export function BlockDraggable({
  id,
  type,
  children,
}: {
  id: string;
  type: string;
  children: (draggable: SortableBinding) => ReactNode;
}) {
  const data: BlockDrag = { kind: 'block', id, type };
  const { setNodeRef, setActivatorNodeRef, listeners, isDragging } = useDraggable({
    id: `block:${id}`,
    data,
  });

  // The block itself does not travel — the chip under the pointer does (`DragOverlay`) — it only
  // fades, so the page keeps its shape while the slots are chosen.
  return children({
    setNodeRef,
    style: { opacity: isDragging ? 0.5 : 1 },
    handle: { attach: setActivatorNodeRef, listeners },
  });
}
