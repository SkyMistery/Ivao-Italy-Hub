import type { z } from 'zod';

import { newId, type BlockEnvelope, type Body, type Layout, type SectionEnvelope } from '../../blocks';
import { readFields, type FieldNode } from '../../shared/forms';
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
export function addSection(body: Body, locales: readonly string[]): { body: Body; id: string } {
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

  return { body: { ...body, sections: [...body.sections, section] }, id };
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

export function addBlock(
  body: Body,
  sectionId: string,
  type: string,
  props: Record<string, unknown>,
  renderMode: 'live' | 'frozen' | null,
): { body: Body; id: string } {
  const id = newId('b');
  const block: BlockEnvelope = { id, type, version: 1, props, renderMode, frozen: null, column: 0 };

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
 */
export function defaultProps(
  schema: z.ZodType<Record<string, unknown>>,
  locales: readonly string[],
): Record<string, unknown> {
  const value: Record<string, unknown> = {};

  for (const field of readFields(schema)) {
    value[field.path] = defaultOf(field, locales);
  }

  return value;
}

function defaultOf(field: FieldNode, locales: readonly string[]): unknown {
  switch (field.kind) {
    case 'localized':
      return emptyLocalized(locales);
    case 'text':
      return '';
    case 'number':
      return 0;
    case 'boolean':
      return false;
    case 'enum':
      return field.options[0] ?? '';
    case 'list':
      return [];
    case 'object':
      // A child's `path` is the whole way down from the top of the schema, which is what a label is
      // looked up by; the key inside the object is only its last segment.
      return Object.fromEntries(
        field.children.map((child) => [child.path.slice(field.path.length + 1), defaultOf(child, locales)]),
      );
  }
}
