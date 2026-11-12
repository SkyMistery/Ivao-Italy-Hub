import {
  createContext,
  useContext,
  useEffect,
  type CSSProperties,
  type ComponentType,
  type ReactNode,
} from 'react';

import type { BlockEnvelope, Span } from './envelope';

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
   * A double click: pick it **and** put the cursor in its first field (Carmine, 11 September 2026:
   * one gesture less on every change). The page only says what was opened; where the cursor goes
   * is the editor's, which owns the panel.
   */
  readonly onOpen?: (kind: 'section' | 'block', id: string) => void;
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
  /**
   * A section where it is wanted: with nothing said, at the end of the page, offered after the last
   * one the way an empty column offers a block; with a place, between two others — `parentId` the
   * section whose row it is, none for one of the page, and `at` its place among its siblings. Absent
   * on a body nothing may be added to. Rows are offered only inside a section whose `actions` answer
   * `addRow`, so the template's rules and the depth the server accepts stay in the editor. `byKeyboard`
   * says the press came from the keyboard: the button it was on is about to mean something else, and
   * where the cursor goes next is the editor's, which owns the panel.
   */
  readonly onAddSection?: (parentId?: string, at?: number, byKeyboard?: boolean) => void;
  /**
   * The name to draw a block by while nothing has been written into it, or `null` once something
   * has (Carmine, 11 September 2026). A heading without words draws nothing, and nothing is what
   * looked lost; the editor decides what "nothing written" means for each block, the page draws a
   * placeholder in its place, and a visitor — who never reads a draft — is never shown one.
   */
  readonly blank?: (block: BlockEnvelope) => string | null;
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
  /**
   * The width of a tile of a dashboard, changed from the handle on its edge or from the keyboard (D2,
   * note 2026-09-13-le-dashboard-a-tutto-schermo §3.5). Absent for a page, whose blocks have no width.
   */
  readonly onSpan?: (id: string, span: Span) => void;
  /**
   * The width a block takes in its column, from the handle on its edge (design of the editor's phase 3, §5.3): the new
   * value of the property its registration declares `resizable`, already on a step and within the limits. `gesture`
   * tells one drag from the next (`newGesture`), so that every value of one drag is one step of the history and two
   * drags are two. Absent on a body nothing may be written to: then there is no handle.
   */
  readonly onResize?: (id: string, width: number, gesture: number) => void;
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
  readonly key:
    | 'moveUp'
    | 'moveDown'
    | 'remove'
    | 'duplicate'
    | 'addRow'
    | 'sectionAbove'
    | 'sectionBelow'
    | 'rowAbove'
    | 'rowBelow';
  /** `byKeyboard` as in `Picking.onAddSection`: told by the bar, read by what inserts. */
  readonly run: (byKeyboard?: boolean) => void;
}

export const PickingContext = createContext<Picking | null>(null);

let gestures = 0;

/**
 * A number no other gesture of this page has had: the drag of a handle, or one move of the panel's slider. Counted for
 * the whole page and not by the handle, which is drawn again whenever its block is picked again: counted there, two drags
 * apart would both be the first, and would be undone as one.
 */
export function newGesture(): number {
  gestures += 1;
  return gestures;
}

/** `null` everywhere the editor has not said otherwise, which is everywhere the public reads. */
export function usePicking(): Picking | null {
  return useContext(PickingContext);
}

/** What taking a click means while a page is composed or looked at: its default does not happen, and it goes no further. */
export function stopClick(event: Pick<Event, 'preventDefault' | 'stopPropagation'>): void {
  event.preventDefault();
  event.stopPropagation();
}

/**
 * The read-only way of the same thing (design of phase 3, §4.7): the draft looked at in a tab of its own, where a link
 * must not carry anybody away from what they came to check, and there is nothing to pick. The whole document and not a
 * block at a time, so that the frame's links keep the promise the strip makes too, with nothing around a block that a
 * visitor's page does not have. Only the links: an accordion opens and a tab changes, because reading them is what the
 * preview is for, and no block writes anything from a click.
 *
 * `auxclick` is the middle button, which opens a link without a `click`. The frame of an interactive block is another
 * document, whose clicks do not come through here.
 */
export function useLinksDisabled(): void {
  useEffect(() => {
    const stopLink = (event: MouseEvent) => {
      if (event.target instanceof Element && event.target.closest('a') !== null) {
        stopClick(event);
      }
    };

    document.addEventListener('click', stopLink, { capture: true });
    document.addEventListener('auxclick', stopLink, { capture: true });

    return () => {
      document.removeEventListener('click', stopLink, { capture: true });
      document.removeEventListener('auxclick', stopLink, { capture: true });
    };
  }, []);
}
