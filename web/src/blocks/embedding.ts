import { createContext, useContext } from 'react';

/**
 * How a block of the page finds the address of its own frame (12 September 2026,
 * `decisions/2026-09-12-il-blocco-interattivo.md`).
 *
 * ⚠️ Why a context and not a prop. The frame of an interactive block is served by an endpoint —
 * `/embed/{content}/{version}/{block}` — so the address contains the row and the version, and a
 * block knows neither: a block is handed its properties and draws them, and the renderer is shared
 * with the public site and knows nothing about routes or the API. The screen that mounts the
 * renderer knows both — the public page knows which version it is showing, the editor knows it is
 * showing a draft — so it is the screen that supplies this, exactly as it supplies the picking
 * context that gives the editor its chrome (`blocks/picking.ts`).
 *
 * With no provider the value is `null` and an interactive block draws no frame at all. That is not
 * a failure mode to be sorry about: it is the same rule as everywhere else here — the public
 * renderer does only what the page around it has granted.
 */
export interface Embedding {
  /**
   * The address of the frame for one block of this page, or `null` when this screen cannot serve
   * one — a preview of a body that has never been saved, for instance, where there is no row to
   * ask the server about.
   */
  readonly frameUrl: (blockId: string) => string | null;
}

export const EmbeddingContext = createContext<Embedding | null>(null);

export function useEmbedding(): Embedding | null {
  return useContext(EmbeddingContext);
}

/**
 * The address of a frame, in the one place that writes it. Both callers — the public page and the
 * editor — go through here, so the shape of that address exists once on this side as it exists once
 * on the other (`EmbedEndpoints.Pattern`).
 *
 * `locale` rides as a query so that the document inside the frame is written in the language the
 * reader is reading, which is the only thing the server cannot work out for itself.
 */
export function frameAddress(
  contentId: number,
  version: number | 'draft',
  blockId: string,
  locale: string,
): string {
  return `/embed/${contentId}/${version}/${encodeURIComponent(blockId)}?lang=${encodeURIComponent(locale)}`;
}
