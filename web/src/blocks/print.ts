import { createContext, useContext } from 'react';

/**
 * Whether the page is being drawn for paper (G14, implementation plan M1: "stampa").
 *
 * A block that hides part of itself behind a gesture — the panels of `tabs`, the folded answers of
 * `accordion` — has nothing to gesture with on paper, so while this is true it draws every panel
 * one under the other. `false` for every visitor and in the editor; the public document screen
 * turns it on for the length of a print (`usePrintMode`), and nothing else does.
 *
 * It is a context and not a property so that a block three sections deep learns about it without
 * the renderer threading a flag it does not otherwise care about — the same reason `PickingContext`
 * is one. And it lives in `blocks/` because the blocks read it: the renderer stays free of anything
 * about screens.
 */
export const PrintContext = createContext(false);

export function usePrinting(): boolean {
  return useContext(PrintContext);
}
