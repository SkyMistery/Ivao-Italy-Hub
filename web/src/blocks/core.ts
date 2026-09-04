import { Heading, Info, Link2, MousePointerClick, Pilcrow } from 'lucide-react';

import type { BlockRegistration } from '../shared/modules';

import { CalloutBlock, CtaBlock, HeadingBlock, LinkListBlock, TextBlock } from './blocks';
import { calloutSchema, ctaSchema, headingSchema, linkListSchema, textSchema } from './schemas';

/**
 * The five blocks of the core, tied together: a type, a schema, a component, an icon, the i18n key
 * of its name, and example properties for the gallery (design M0 §5.4).
 *
 * The server declares the same five as `IBlockDescriptor`s and publishes them in `/api/me`. The
 * halves are deliberately separate — what a block *is* lives here, what it *means* lives only in
 * TypeScript (CLAUDE.md §2) — and the ui-kit is where a mismatch shows up.
 */

export const CORE_BLOCK_TYPES = {
  heading: 'heading',
  text: 'text',
  callout: 'callout',
  cta: 'cta',
  linkList: 'linkList',
} as const;

export const coreBlockRegistrations: readonly BlockRegistration[] = [
  {
    type: CORE_BLOCK_TYPES.heading,
    version: 1,
    kind: 'Content',
    schema: headingSchema,
    component: HeadingBlock,
    example: { level: 2, text: { en: 'A heading', it: 'Un titolo' } },
    editorLabelKey: 'blocks.heading.label',
    icon: Heading,
  },
  {
    type: CORE_BLOCK_TYPES.text,
    version: 1,
    kind: 'Content',
    schema: textSchema,
    component: TextBlock,
    example: {
      markdown: {
        en: 'A paragraph with **bold** and a [link](https://www.ivao.aero).',
        it: 'Un paragrafo con **grassetto** e un [link](https://www.ivao.aero).',
      },
    },
    editorLabelKey: 'blocks.text.label',
    icon: Pilcrow,
  },
  {
    type: CORE_BLOCK_TYPES.callout,
    version: 1,
    kind: 'Content',
    schema: calloutSchema,
    component: CalloutBlock,
    example: {
      tone: 'info',
      title: { en: 'Worth knowing', it: 'Da sapere' },
      text: {
        en: 'Something the reader should not miss.',
        it: 'Qualcosa che il lettore non deve perdere.',
      },
    },
    editorLabelKey: 'blocks.callout.label',
    icon: Info,
  },
  {
    type: CORE_BLOCK_TYPES.cta,
    version: 1,
    kind: 'Content',
    schema: ctaSchema,
    component: CtaBlock,
    example: {
      label: { en: 'Start here', it: 'Comincia da qui' },
      href: 'https://www.ivao.aero',
    },
    editorLabelKey: 'blocks.cta.label',
    icon: MousePointerClick,
  },
  {
    type: CORE_BLOCK_TYPES.linkList,
    version: 1,
    kind: 'Data',
    schema: linkListSchema,
    component: LinkListBlock,
    example: { category: '', department: '', limit: 10 },
    // The gallery is a page about the components: a data block shows this rather than whatever
    // this installation happens to hold today, or nothing at all on a fresh one.
    exampleData: {
      items: [
        {
          title: { en: 'IVAO', it: 'IVAO' },
          url: 'https://www.ivao.aero',
          description: { en: 'The network itself.', it: 'La rete stessa.' },
        },
      ],
    },
    editorLabelKey: 'blocks.linkList.label',
    icon: Link2,
  },
];
