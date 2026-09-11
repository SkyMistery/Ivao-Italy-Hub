import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { ReactNode } from 'react';

import type { SortableBinding } from '../../blocks';

/**
 * A section dragged among its siblings on the page (Carmine, 11 September 2026). The renderer
 * draws these through `Picking.SortableGroup` and `Picking.Sortable` and never sees dnd-kit; the
 * editor's one `DndContext` — the same the palette drags in — hears the drop and reorders.
 *
 * What a dragged section carries, and what the drop handler reads: `kind: 'section'`. The outline
 * uses the same word for its rows, in a context of its own that never holds the page.
 */
export interface SectionDrag {
  readonly kind: 'section';
}

export function SectionSortableGroup({ ids, children }: { ids: readonly string[]; children: ReactNode }) {
  return (
    <SortableContext items={[...ids]} strategy={verticalListSortingStrategy}>
      {children}
    </SortableContext>
  );
}

export function SectionSortable({
  id,
  children,
}: {
  id: string;
  children: (sortable: SortableBinding) => ReactNode;
}) {
  const data: SectionDrag = { kind: 'section' };
  const { setNodeRef, setActivatorNodeRef, listeners, transform, transition, isDragging } = useSortable({
    id,
    data,
  });

  return children({
    setNodeRef,
    style: { transform: CSS.Translate.toString(transform), transition, opacity: isDragging ? 0.6 : 1 },
    handle: { attach: setActivatorNodeRef, listeners },
  });
}
