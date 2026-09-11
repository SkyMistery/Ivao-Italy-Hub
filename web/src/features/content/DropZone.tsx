import { useDndContext, useDroppable } from '@dnd-kit/core';
import { useTranslation } from 'react-i18next';

/**
 * Where a component dragged from the palette lands on the page (G15, session 3; the third thing
 * va.ivao.aero's builder has, `decisions/2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`).
 *
 * The renderer draws one of these before every block of a column and one after the last, through
 * `Picking.DropZone` — the page does not import dnd-kit, this file does. **Hidden until a drag is
 * under way**: a slot that took space at rest would put air between the blocks that a visitor does
 * not get, and the page being composed has to be the page a reader gets. Always mounted, though, so
 * that the slot is registered before the drag that needs it begins.
 */

/** What a dragged palette entry carries, and what a slot reads on the drop. */
export interface PaletteDrag {
  readonly kind: 'palette';
  readonly type: string;
}

/** What a block dragged on the page carries: which one, and its type for the chip under the pointer. */
export interface BlockDrag {
  readonly kind: 'block';
  readonly id: string;
  readonly type: string;
}

export interface SlotDrop {
  readonly kind: 'slot';
  readonly section: string;
  readonly column: number;
  readonly index: number;
}

export function DropZone({ section, column, index }: { section: string; column: number; index: number }) {
  const { t } = useTranslation();
  const { active } = useDndContext();
  const data: SlotDrop = { kind: 'slot', section, column, index };
  const { setNodeRef, isOver } = useDroppable({ id: `slot:${section}:${column}:${index}`, data });

  // A slot is for a component from the palette and for a block already on the page; a section
  // being dragged lands on sections, and the slots stay out of its way.
  const kind = (active?.data.current as PaletteDrag | BlockDrag | undefined)?.kind;
  const dragging = kind === 'palette' || kind === 'block';

  return (
    <div
      ref={setNodeRef}
      data-drop-index={index}
      aria-label={t('content.editor.dropHere')}
      hidden={!dragging}
      className={`flex h-8 items-center justify-center rounded-md border border-dashed text-xs transition-colors ${
        isOver ? 'border-primary bg-accent text-foreground' : 'border-border text-muted-foreground'
      }`}
    >
      {t('content.editor.dropHere')}
    </div>
  );
}
