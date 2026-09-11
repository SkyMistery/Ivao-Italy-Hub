import type { z } from 'zod';

import { newId, type BlockEnvelope, type Body, type Layout, type SectionEnvelope } from '../../blocks';
import { blankValues } from '../../shared/forms';
import { emptyLocalized } from '../../shared/i18n/localized';

/**
 * Changing a body, as functions that return a new one. The editor holds the draft in state and
 * saves it whole, so every operation here is a pure rewrite of the tree: no operation reaches into
 * the one the screen is currently drawing, and undoing is a matter of not calling the setter.
 *
 * Sections nest, so every walk here is recursive. Three is as deep as they go, which the server
 * enforces (`BlockDocumentWalker.MaxDepth`) and the editor does not need to know.
 */

type SectionPatch = Partial<Omit<SectionEnvelope, 'id' | 'blocks' | 'sections'>>;

function mapSections(
  sections: SectionEnvelope[],
  change: (section: SectionEnvelope) => SectionEnvelope,
): SectionEnvelope[] {
  return sections.map((section) => change({ ...section, sections: mapSections(section.sections, change) }));
}

export function updateSection(body: Body, id: string, patch: SectionPatch): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) =>
      section.id === id ? { ...section, ...patch } : section,
    ),
  };
}

export function updateBlock(body: Body, id: string, patch: Partial<BlockEnvelope>): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) => ({
      ...section,
      blocks: section.blocks.map((block) => (block.id === id ? { ...block, ...patch } : block)),
    })),
  };
}

export function findSection(body: Body, id: string): SectionEnvelope | undefined {
  for (const section of body.sections) {
    const found = section.id === id ? section : findSection({ ...body, sections: section.sections }, id);
    if (found !== undefined) {
      return found;
    }
  }

  return undefined;
}

export function findBlock(
  body: Body,
  id: string,
): { block: BlockEnvelope; section: SectionEnvelope } | undefined {
  const walk = (
    sections: SectionEnvelope[],
  ): { block: BlockEnvelope; section: SectionEnvelope } | undefined => {
    for (const section of sections) {
      const block = section.blocks.find((candidate) => candidate.id === id);
      if (block !== undefined) {
        return { block, section };
      }

      const nested = walk(section.sections);
      if (nested !== undefined) {
        return nested;
      }
    }

    return undefined;
  };

  return walk(body.sections);
}

/** A new section, at the end, with the frame a section has when nobody has chosen one. */
/**
 * A section, at the top of the page or **inside another one** — which is what the editor calls a
 * *row*.
 *
 * ⚠️ Nesting is not new: `MaxDepth` in the envelope validator has been 3 since M1, the renderer
 * draws a nested section inside its parent's width container, and `removeSection` prunes
 * recursively. What was missing was any way to make one, so none of the ten seeded pages and
 * templates has ever nested — we built three levels, validated them, drew them, and offered two
 * (nota `2026-09-10-che-cosa-fa-il-pagebuilder-di-hq.md`).
 *
 * What it buys: a section carries the frame — the background, the air around it, how wide it is —
 * and each row inside it carries its own column layout. So one band of colour can hold two columns
 * and then three, which before took two sections and therefore two bands.
 */
export function addSection(
  body: Body,
  locales: readonly string[],
  parentId?: string,
): { body: Body; id: string } {
  const id = newId('s');
  const section: SectionEnvelope = {
    id,
    title: emptyLocalized(locales),
    layout: 'stacked',
    background: 'none',
    padding: 'md',
    width: 'default',
    blocks: [],
    sections: [],
  };

  if (parentId === undefined) {
    return { body: { ...body, sections: [...body.sections, section] }, id };
  }

  // A row is born without a frame of its own: the section around it already draws one, and a second
  // background inside the first is the thing that makes a page look assembled rather than composed.
  const row: SectionEnvelope = { ...section, background: 'none', padding: 'none' };

  return {
    body: {
      ...body,
      sections: mapSections(body.sections, (candidate) =>
        candidate.id === parentId ? { ...candidate, sections: [...candidate.sections, row] } : candidate,
      ),
    },
    id,
  };
}

export function removeSection(body: Body, id: string): Body {
  const prune = (sections: SectionEnvelope[]): SectionEnvelope[] =>
    sections
      .filter((section) => section.id !== id)
      .map((section) => ({ ...section, sections: prune(section.sections) }));

  return { ...body, sections: prune(body.sections) };
}

/** Moves a top level section one place up or down. Nested ones move with the section they are in. */
export function moveSection(body: Body, id: string, delta: -1 | 1): Body {
  return { ...body, sections: move(body.sections, (section) => section.id === id, delta) };
}

/**
 * A section dropped onto another one (design M1 §9.3). Only within the same list: a drop that
 * crosses from one parent into another moves nothing, which is what makes the arrows and the drag
 * two ways of doing the *same* thing rather than two different powers.
 */
export function reorderSections(body: Body, activeId: string, overId: string): Body {
  const walk = (sections: SectionEnvelope[]): SectionEnvelope[] => {
    const moved = reorder(sections, activeId, overId);
    return moved ?? sections.map((section) => ({ ...section, sections: walk(section.sections) }));
  };

  return { ...body, sections: walk(body.sections) };
}

/** The same, for the blocks of one section. */
export function reorderBlocks(body: Body, activeId: string, overId: string): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) => {
      const moved = reorder(section.blocks, activeId, overId);
      return moved === null ? section : { ...section, blocks: moved };
    }),
  };
}

/**
 * Adds a block at the end of one column of a section.
 *
 * `column` since 11 September 2026: it used to be 0 for every block, so in a section of two columns a
 * component always landed in the first and had to be moved into the second afterwards — which is the
 * friction the "add here" of an empty column exists to remove. It defaults to the first, which is also
 * where a block of a stacked section is.
 */
export function addBlock(
  body: Body,
  sectionId: string,
  type: string,
  props: Record<string, unknown>,
  renderMode: 'live' | 'frozen' | null,
  column = 0,
): { body: Body; id: string } {
  const id = newId('b');
  const block: BlockEnvelope = { id, type, version: 1, props, renderMode, frozen: null, column };

  return {
    body: {
      ...body,
      sections: mapSections(body.sections, (section) =>
        section.id === sectionId ? { ...section, blocks: [...section.blocks, block] } : section,
      ),
    },
    id,
  };
}

export function removeBlock(body: Body, id: string): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) => ({
      ...section,
      blocks: section.blocks.filter((block) => block.id !== id),
    })),
  };
}

/** A copy of a block, right after it, with an identifier of its own and no capture carried over. */
export function duplicateBlock(body: Body, id: string): { body: Body; id: string } {
  const copyId = newId('b');

  return {
    body: {
      ...body,
      sections: mapSections(body.sections, (section) => {
        const index = section.blocks.findIndex((block) => block.id === id);
        if (index < 0) {
          return section;
        }

        const original = section.blocks[index]!;
        const copy: BlockEnvelope = {
          ...original,
          id: copyId,
          props: structuredClone(original.props),
          frozen: null,
        };

        return { ...section, blocks: section.blocks.toSpliced(index + 1, 0, copy) };
      }),
    },
    id: copyId,
  };
}

export function moveBlock(body: Body, id: string, delta: -1 | 1): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) => ({
      ...section,
      blocks: move(section.blocks, (block) => block.id === id, delta),
    })),
  };
}

/**
 * A block whose column no longer exists would be drawn nowhere, and the server refuses the save.
 * Narrowing a section's layout therefore pulls its blocks back into a column that is still there.
 */
export function clampColumns(body: Body, sectionId: string, layout: Layout, columns: number): Body {
  return {
    ...body,
    sections: mapSections(body.sections, (section) =>
      section.id === sectionId
        ? {
            ...section,
            layout,
            blocks: section.blocks.map((block) => ({
              ...block,
              column: Math.min(block.column ?? 0, columns - 1),
            })),
          }
        : section,
    ),
  };
}

/**
 * One list, with `activeId` taken out and put back where `overId` was. Null when the two are not in
 * the same list, so that a caller can keep looking further down the tree.
 */
function reorder<T extends { id: string }>(items: T[], activeId: string, overId: string): T[] | null {
  const from = items.findIndex((item) => item.id === activeId);
  const to = items.findIndex((item) => item.id === overId);

  if (from < 0 || to < 0) {
    return null;
  }

  const moved = [...items];
  moved.splice(to, 0, ...moved.splice(from, 1));
  return moved;
}

function move<T>(items: T[], matches: (item: T) => boolean, delta: -1 | 1): T[] {
  const index = items.findIndex(matches);
  const target = index + delta;

  if (index < 0 || target < 0 || target >= items.length) {
    return items;
  }

  const moved = [...items];
  [moved[index], moved[target]] = [moved[target]!, moved[index]!];
  return moved;
}

/**
 * The properties a block starts with, read off its own schema. Writing them by hand next to each
 * block would be the same description twice, and the one that would go stale is this one.
 *
 * What "empty" means for each kind of field is `blankValues`, in the form generator: a new block
 * and a new entry of a repeatable list are the same question, and G3 is where the second one
 * started being asked.
 */
export function defaultProps(
  schema: z.ZodType<Record<string, unknown>>,
  locales: readonly string[],
): Record<string, unknown> {
  return blankValues(schema, locales);
}
