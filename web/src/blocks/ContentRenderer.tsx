import { Badge } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../app/registry';
import { mediaFileUrl } from '../shared/api/mediaUrl';
import type { BlockRegistration } from '../shared/modules';

import { blockDataQuery } from './data';
import { columnsOf, type BlockEnvelope, type Body, type SectionEnvelope } from './envelope';
import { usePicking, type Picking } from './picking';

/**
 * Drawing a page. The same component renders the published version for a visitor and the draft in
 * the editor's preview, because "what will this look like" and "what does this look like" must not
 * be two pieces of code that can disagree (design M0 §5.4 and §7.7).
 *
 * A section decides its own frame — how wide, how much air, what sits behind it — and its layout
 * decides whether its blocks follow one another or stand in columns. A block decides nothing about
 * the page: it is handed its properties and draws them.
 *
 * A section's `title` is not drawn. It is the name the editor puts in the tree, which is why the
 * seeded templates spell it "Hero" and "Body": what a visitor reads is a `heading` block, which is
 * a block an editor can move, translate and delete.
 *
 * Since 9 September 2026 the same component is also **what the editor composes in** — see
 * `picking.ts`. That is one context read in two places; with no provider, which is the public path,
 * every line below behaves exactly as it did.
 */

/**
 * What sits behind a section. `image` brings no colour of its own: the picture is set as the
 * background of the frame, and the text over it keeps reading against the page's own foreground —
 * which is why the editor is told, in `docs/UI-GUIDELINES.md`, that a picture behind a section is
 * for a quiet one and not for a wall of prose.
 */
const BACKGROUND = {
  none: '',
  muted: 'bg-muted',
  accent: 'bg-accent',
  // ⚠️ The three dark grounds (Carmine, 11 September 2026: the palette of va.ivao.aero's page
  // builder, less its free colour picker). Each carries the class `dark` as well as its colour, and
  // that is what makes them safe: Atmosphere defines the dark theme's tokens on `.dark` and Tailwind's
  // `dark:` variant matches `.dark *`, so a dark section is a piece of the page in the dark theme —
  // every block inside it reads light on dark **by construction**, whatever colours it asks for.
  // A free colour could promise none of that, which is why there is none.
  //
  // Atmosphere's own tokens, not colours written here: `atmos-700` is exactly va.ivao.aero's #0D2C99.
  // `on-brand-ground` lightens the secondary grey on this one ground, where the dark theme's own
  // measured 3.50 : 1 (`styles/index.css`).
  brand: 'dark on-brand-ground bg-atmos-700 text-foreground',
  deep: 'dark bg-atmos-800 text-foreground',
  dark: 'dark bg-fuselage-900 text-foreground',
  image: 'bg-muted bg-cover bg-center',
} as const;

const PADDING = {
  none: '',
  sm: 'py-4',
  md: 'py-8',
  lg: 'py-14',
} as const;

const WIDTH = {
  narrow: 'mx-auto w-full max-w-3xl px-4',
  default: 'mx-auto w-full max-w-5xl px-4',
  wide: 'mx-auto w-full max-w-7xl px-4',
  full: 'w-full px-4',
} as const;

/** The share of the grid each column takes, per layout. Literal classes: Tailwind reads the source. */
const COLUMN_SPAN: Record<string, readonly string[]> = {
  '1/2+1/2': ['md:col-span-1', 'md:col-span-1'],
  '1/3+2/3': ['md:col-span-1', 'md:col-span-2'],
  '2/3+1/3': ['md:col-span-2', 'md:col-span-1'],
  '3x1/3': ['md:col-span-1', 'md:col-span-1', 'md:col-span-1'],
};

const GRID: Record<string, string> = {
  '1/2+1/2': 'md:grid-cols-2',
  '1/3+2/3': 'md:grid-cols-3',
  '2/3+1/3': 'md:grid-cols-3',
  '3x1/3': 'md:grid-cols-3',
};

export function ContentRenderer({
  body,
  /**
   * Draws what only the staff should be told: a block whose type nobody registered, and a badge on
   * a block that is showing a capture rather than the live answer. A visitor sees neither.
   */
  staff = false,
}: {
  body: Body;
  staff?: boolean;
}) {
  return (
    <div className="flex flex-col">
      {body.sections.map((section) => (
        <SectionView key={section.id} section={section} staff={staff} />
      ))}
    </div>
  );
}

function SectionView({ section, staff }: { section: SectionEnvelope; staff: boolean }) {
  const picking = usePicking();
  const frame = [
    BACKGROUND[section.background],
    PADDING[section.padding],
    // A section is picked by its own space — the air around its blocks — because clicking a block
    // picks the block. `outline` and not `border`: a border would move everything by two pixels and
    // the point of composing here is that what you see is what a reader gets.
    picking === null ? '' : ring(picking, section.id),
  ]
    .filter(Boolean)
    .join(' ');

  // The one place a style is written rather than a class: which picture it is only exists at
  // runtime, and Tailwind reads the source rather than the page.
  const picture =
    section.background === 'image' && typeof section.mediaId === 'number'
      ? { backgroundImage: `url(${mediaFileUrl(section.mediaId)})` }
      : undefined;

  return (
    <section
      className={frame}
      {...(picture === undefined ? {} : { style: picture })}
      {...(picking === null
        ? {}
        : {
            'data-pickable': 'section',
            // The bubble phase, while a block takes the capture phase and stops there: outer
            // handlers capture first, so a section that captured would always win and a block could
            // never be picked.
            onClick: () => picking.onPick('section', section.id),
          })}
    >
      <div className={`${WIDTH[section.width]} flex flex-col gap-6`}>
        <SectionBlocks section={section} staff={staff} />

        {section.sections.map((nested) => (
          <SectionView key={nested.id} section={nested} staff={staff} />
        ))}
      </div>
    </section>
  );
}

function SectionBlocks({ section, staff }: { section: SectionEnvelope; staff: boolean }) {
  if (section.layout === 'stacked') {
    return (
      <Column section={section} column={0}>
        {section.blocks.map((block) => (
          <BlockView key={block.id} block={block} staff={staff} />
        ))}
      </Column>
    );
  }

  const columns = columnsOf(section.layout);
  const spans = COLUMN_SPAN[section.layout] ?? [];

  return (
    <div className={`grid grid-cols-1 gap-6 ${GRID[section.layout] ?? ''}`}>
      {Array.from({ length: columns }, (_, column) => (
        <Column key={column} section={section} column={column} className={spans[column] ?? ''}>
          {section.blocks
            // A block with no column belongs to the first one: a section whose layout changed
            // must not lose the blocks that were written before it did.
            .filter((block) => (block.column ?? 0) === column)
            .map((block) => (
              <BlockView key={block.id} block={block} staff={staff} />
            ))}
        </Column>
      ))}
    </div>
  );
}

/**
 * One column of a section. For a visitor it is a plain column and nothing else.
 *
 * ⚠️ While a page is being composed it is **drawn**, and that is the whole of the request of
 * 11 September 2026 (Carmine, with va.ivao.aero's page builder in front of him: "when a section is
 * added you see clearly how it is divided — the drop here in the empty areas"). Every column gets a
 * dashed outline, so a section in two columns reads as two columns before anything is in them; an
 * empty one says where a component would go and takes the click that chooses it. Before this an
 * empty section drew nothing at all, and a component always landed in the first column.
 *
 * `outline` and not `border`, for the reason the selection ring gives: a border would move
 * everything by a pixel, and what is composed here has to be what a reader gets.
 */
function Column({
  section,
  column,
  className = '',
  children,
}: {
  section: SectionEnvelope;
  column: number;
  className?: string;
  children: ReactNode[];
}) {
  const picking = usePicking();
  const { t } = useTranslation();

  if (picking === null) {
    return <div className={`flex flex-col gap-6 ${className}`}>{children}</div>;
  }

  const chosen = picking.target?.section === section.id && picking.target.column === column;
  const open = picking.accepts(section.id);

  return (
    <div
      data-pickable="column"
      // The bubble phase, like the section's: a block takes the capture phase and stops there, so a
      // click on a block picks the block, and a click on the air of a column picks the column.
      // Stopped here, or the section would take it next and forget which column it was.
      onClick={(event) => {
        if (!open) {
          return;
        }
        event.stopPropagation();
        picking.onPickColumn(section.id, column);
      }}
      className={`flex min-h-16 flex-col gap-6 rounded-md outline-1 outline-offset-4 ${
        chosen ? 'outline-primary outline-solid' : 'outline-border outline-dashed'
      } ${className}`}
    >
      {children}

      {children.length === 0 && open ? (
        <button
          type="button"
          onClick={(event) => {
            event.stopPropagation();
            picking.onPickColumn(section.id, column);
          }}
          className={`text-muted-foreground flex min-h-16 flex-1 items-center justify-center rounded-md border border-dashed text-sm transition-colors ${
            chosen
              ? 'border-primary text-foreground'
              : 'border-border hover:border-primary hover:text-foreground'
          }`}
        >
          {t('content.editor.addHere')}
        </button>
      ) : null}
    </div>
  );
}

export function BlockView({ block, staff }: { block: BlockEnvelope; staff: boolean }) {
  const picking = usePicking();
  const registration = registry.blocks.find((candidate) => candidate.type === block.type);

  const drawn =
    registration === undefined ? (
      staff ? (
        <UnknownBlock type={block.type} />
      ) : null
    ) : registration.kind === 'Data' ? (
      <DataBlockView block={block} registration={registration} staff={staff} />
    ) : (
      <registration.component props={block.props} />
    );

  if (picking === null || drawn === null) {
    return drawn;
  }

  return (
    <div
      data-pickable="block"
      className={`rounded-sm ${ring(picking, block.id)}`}
      // ⚠️ The **capture** phase, and both `preventDefault` and `stopPropagation`. A block is not
      // an inert rectangle: it holds links, buttons, a contact form. Capturing means a click lands
      // on the block rather than on what is inside it — so a call to action selects itself instead
      // of carrying whoever is composing out of the editor with unsaved changes — and stopping it
      // there is what leaves the section pickable by its own space.
      onClickCapture={(event) => {
        event.preventDefault();
        event.stopPropagation();
        picking.onPick('block', block.id);
      }}
    >
      {drawn}
    </div>
  );
}

/** What says "this one". Two pixels away from the thing, so nothing on the page moves. */
function ring(picking: Picking, id: string): string {
  return picking.selected === id
    ? 'outline-primary outline-2 outline-offset-2'
    : 'hover:outline-border hover:outline-2 hover:outline-offset-2';
}

/**
 * A data block shows the capture the version carries, and asks the provider only when there is
 * none. That is the whole of live and frozen on this side: publication decided which it is, and
 * the renderer does not get a second opinion.
 *
 * A draft never carries a capture — publication writes it into the version, not back into the
 * draft — so the editor's preview shows live data for a block that will be frozen. That is right,
 * and it would be misleading unsaid: the badge says which of the two a member is looking at
 * (design M0 §7.7).
 */
function DataBlockView({
  block,
  registration,
  staff,
}: {
  block: BlockEnvelope;
  registration: BlockRegistration;
  staff: boolean;
}) {
  const { t } = useTranslation();
  const captured = block.frozen ?? null;
  const live = captured === null;

  const query = useQuery({ ...blockDataQuery(block.type, block.props), enabled: live });
  const Component = registration.component;

  const data = live ? (query.isPending ? undefined : (query.data ?? null)) : captured;

  // Three states, two of which only the staff is told about: this is a capture; this is live but
  // will be captured the next time somebody publishes; this is live and stays live.
  const badge = !live
    ? t('blocks.captured')
    : block.renderMode === 'frozen'
      ? t('blocks.willBeCaptured')
      : null;

  return (
    <div className="flex flex-col gap-2">
      {staff && badge !== null ? (
        <div>
          <Badge variant="flat" color="gray" text={badge} />
        </div>
      ) : null}
      <Component props={block.props} data={data} />
    </div>
  );
}

/**
 * A block the server knows and this browser cannot draw. It is only ever shown to the staff: a
 * visitor would be told nothing useful by it, and a gap is better than a stack trace — but a
 * coordinator staring at a page with a hole in it deserves to know why (design M0 §5.4).
 */
function UnknownBlock({ type }: { type: string }) {
  const { t } = useTranslation();

  return (
    <div className="border-border text-muted-foreground rounded-md border border-dashed p-4 text-sm">
      {t('blocks.unknown', { type })}
    </div>
  );
}
