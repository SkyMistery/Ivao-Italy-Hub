import { createContext, useContext, type CSSProperties, type ComponentType, type ReactNode } from 'react';

/**
 * Composing a page **on the page**, instead of in an outline beside a preview.
 *
 * Decided by Carmine on 9 September 2026, road (A) of
 * `decisions/2026-09-09-comporre-una-pagina-guardandola.md`: what he asked for — "an idea of how the
 * document is coming out and of the space things take, without going back and forth to the preview"
 * — does not need coordinates. It needs the preview to be the thing you click.
 *
 * ⚠️ **Off by construction, not by a flag.** There is one renderer for the public site and for the
 * editor, which is the whole reason "what will this look like" cannot disagree with "what this looks
 * like" (design M0 §5.4). So the interactivity is a context that defaults to `null`, and the public
 * path never mounts a provider: a visitor's page has no handler to remove, no attribute to strip and
 * no class to override. `blocks.test.tsx` asserts that the page a visitor gets is inert.
 *
 * The keyboard road is the outline, which stays exactly as it was. This is a pointer affordance
 * **beside** it, the way dnd-kit was put above the arrows in G11 and not in their place.
 */
export interface Picking {
  /** The identifier of whatever is selected, section or block; `null` when nothing is. */
  readonly selected: string | null;
  readonly onPick: (kind: 'section' | 'block', id: string) => void;
  /**
   * The column a component from the palette would land in, when one has been chosen. Drawn as
   * chosen, so the next click on the palette has a visible destination.
   */
  readonly target: { readonly section: string; readonly column: number } | null;
  /**
   * Chooses a column of a section as where the next component goes (Carmine, 11 September 2026:
   * "you see clearly how it is divided — the drop here in the empty areas"). Before this, a
   * component always landed in the first column of a section and had to be moved to the second.
   */
  readonly onPickColumn: (section: string, column: number) => void;
  /**
   * Whether anything may be added to a section. A section a template locks takes no new component,
   * and an empty column in it must not invite one: an invitation the palette then refuses is a
   * button that lies.
   */
  readonly accepts: (section: string) => boolean;
  /**
   * Where a component dragged from the palette may be dropped: one before every block of a column
   * and one after the last (G15, session 3). A **component** handed over rather than a hook, because
   * the renderer must not import the drag and drop library — a visitor's page has no drag in it —
   * and a hook cannot travel through a context the way a component can. The editor provides one that
   * knows dnd-kit; here it is only drawn where a block could land, and never where `accepts` says no.
   */
  readonly DropZone?: ComponentType<{ section: string; column: number; index: number }>;
  /**
   * What may be done to the thing that is picked, drawn on it (Carmine, 11 September 2026: add and
   * remove sections, and remove a block, from the page and not only from the outline). The editor
   * answers with what the template allows — nothing for a locked section, no removal of a required
   * one — and the page draws exactly that list, so a rule lives in one place.
   */
  readonly actions?: (target: { kind: 'section' | 'block'; id: string }) => readonly PickAction[];
  /** A section at the end of the page, offered after the last one, the way an empty column offers a block. */
  readonly onAddSection?: () => void;
  /**
   * What makes a section draggable among its siblings on the page (Carmine, 11 September 2026:
   * "by hand, meaning draggable, on the page"). Two components handed over for the reason the drop
   * slot is one: the renderer must not import the drag and drop library. `SortableGroup` wraps the
   * sections of one parent — the page's, or the rows of a section — and `Sortable` wraps one of
   * them and hands back where to attach the node, the style that moves it, and the handle to grab.
   * The handle sits on the picked section's bar: a section is dragged after it is picked, so
   * clicking the air of a section still picks it and nothing else.
   */
  readonly SortableGroup?: ComponentType<{ ids: readonly string[]; children: ReactNode }>;
  readonly Sortable?: ComponentType<{ id: string; children: (sortable: SortableBinding) => ReactNode }>;
  /**
   * What makes a block draggable onto any slot of the page — its own column, another, another
   * section's (Carmine, 11 September 2026). The same binding a section gets; the grip is on the
   * picked block's bar, and a block the editor answers no commands for gets no grip.
   */
  readonly BlockDraggable?: ComponentType<{
    id: string;
    type: string;
    children: (draggable: SortableBinding) => ReactNode;
  }>;
}

export interface SortableBinding {
  readonly setNodeRef: (element: HTMLElement | null) => void;
  readonly style: CSSProperties;
  /** The grip: where to attach it, and what it listens to. Not spelled `ref`, which the lint reads as one. */
  readonly handle: {
    readonly attach: (element: HTMLElement | null) => void;
    readonly listeners: Record<string, unknown> | undefined;
  };
}

export interface PickAction {
  readonly key: 'moveUp' | 'moveDown' | 'remove' | 'duplicate' | 'addRow';
  readonly run: () => void;
}

export const PickingContext = createContext<Picking | null>(null);

/** `null` everywhere the editor has not said otherwise, which is everywhere the public reads. */
export function usePicking(): Picking | null {
  return useContext(PickingContext);
}
