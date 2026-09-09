import { createContext, useContext } from 'react';

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
}

export const PickingContext = createContext<Picking | null>(null);

/** `null` everywhere the editor has not said otherwise, which is everywhere the public reads. */
export function usePicking(): Picking | null {
  return useContext(PickingContext);
}
