import {
  Alert,
  BlockQuote,
  Button,
  CardContent,
  CardRoot,
  H1,
  H2,
  H3,
  H4,
  Separator,
  Table,
  Tabs,
  AccordionRoot,
  AccordionItem,
  AccordionTrigger,
  AccordionContent,
} from '@ivao/atmosphere-react';
import { CircleCheck, Info, OctagonAlert, TriangleAlert } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../shared/api/bootstrap';
import { mediaFileUrl } from '../shared/api/mediaUrl';
import { ICONS } from '../shared/icons';
import { useLocalized } from '../shared/i18n/useLocalized';
import type { BlockComponentProps } from '../shared/modules';
import { MarkdownContent } from '../shared/ui';

import { embedSource } from './allowlist';
import { CALLOUT_TONES } from './schemas';

/**
 * How the blocks of the core are drawn (design M0 §5.4, design M1 §1). All but one draw what an
 * editor typed; the data ones draw what the hub knows, which is what makes live and frozen visible
 * at all.
 *
 * A component is handed its properties and draws them. It decides nothing about the page around
 * it, and it never reads the language off a prop: `useLocalized` knows which one is on screen.
 *
 * ⚠️ No block carries a margin of its own (docs/UI-GUIDELINES.md). The space around a block is put
 * there by its section, so that the distance between two blocks does not depend on which two they
 * are. `spacer` is the declared exception, and it is a block whose whole content is the air.
 *
 * What ties a component to its schema is `core.ts`.
 */

/** Reading a property that the schema says is translated. */
function text(props: Record<string, unknown>, name: string): LocalizedString | null {
  const value = props[name];
  return value !== null && typeof value === 'object' ? (value as LocalizedString) : null;
}

function plain(props: Record<string, unknown>, name: string): string {
  return typeof props[name] === 'string' ? props[name] : '';
}

/** A property the schema declares as a closed set, back to one of its members. */
function choice<T extends string>(
  props: Record<string, unknown>,
  name: string,
  allowed: readonly T[],
  fallback: T,
): T {
  return allowed.find((candidate) => candidate === props[name]) ?? fallback;
}

function count(props: Record<string, unknown>, name: string, fallback: number): number {
  return typeof props[name] === 'number' ? props[name] : fallback;
}

function flag(props: Record<string, unknown>, name: string, fallback: boolean): boolean {
  return typeof props[name] === 'boolean' ? props[name] : fallback;
}

/** The identifier of a file, when the property holds one. */
function media(props: Record<string, unknown>, name: string): number | null {
  return typeof props[name] === 'number' ? props[name] : null;
}

/** The entries of a repeatable property, each of them read like a small set of properties. */
function entries(props: Record<string, unknown>, name: string): Record<string, unknown>[] {
  const value = props[name];
  return Array.isArray(value)
    ? value.filter((entry): entry is Record<string, unknown> => entry !== null && typeof entry === 'object')
    : [];
}

/**
 * An address an editor typed, which is an address this hub does not own: it never carries our
 * referrer and never gets a handle on the window it came from. Written once because seven blocks
 * link somewhere.
 */
function OutsideLink({
  href,
  className,
  children,
}: {
  href: string;
  className?: string;
  children: ReactNode;
}) {
  const outside = href.startsWith('http://') || href.startsWith('https://');

  return (
    <a
      href={href}
      {...(className === undefined ? {} : { className })}
      {...(outside ? { target: '_blank', rel: 'noreferrer noopener' } : {})}
    >
      {children}
    </a>
  );
}

/** An icon an editor chose out of the allow list, drawn, or nothing when the name is not on it. */
function ChosenIcon({ name, className }: { name: string; className: string }) {
  const Icon = ICONS[name];
  return Icon === undefined ? null : <Icon className={className} aria-hidden />;
}

/** How many columns a grid stands in. Literal classes, because Tailwind reads the source. */
const GRID_OF: Record<number, string> = {
  2: 'sm:grid-cols-2',
  3: 'sm:grid-cols-2 md:grid-cols-3',
  4: 'sm:grid-cols-2 md:grid-cols-4',
};

function gridOf(columns: number): string {
  return GRID_OF[columns] ?? GRID_OF[3]!;
}

// ---- heading ---------------------------------------------------------------------------------

export function HeadingBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const written = read(text(props, 'text'));
  const level = typeof props.level === 'number' ? props.level : 2;

  switch (level) {
    case 1:
      return <H1>{written}</H1>;
    case 2:
      return <H2>{written}</H2>;
    case 3:
      return <H3>{written}</H3>;
    default:
      return <H4>{written}</H4>;
  }
}

// ---- text ------------------------------------------------------------------------------------

export function TextBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  return <MarkdownContent source={read(text(props, 'markdown'))} />;
}

// ---- callout ---------------------------------------------------------------------------------

/**
 * The tone a coordinator chooses, in the vocabulary the design system speaks. Atmosphere has three
 * variants and we want four tones, so the icon carries the difference between a warning and a
 * refusal: colour alone would say nothing to a reader who cannot tell the two reds apart
 * (docs/UI-GUIDELINES.md).
 */
const CALLOUT_STYLE = {
  info: { variant: 'default', Icon: Info },
  success: { variant: 'success', Icon: CircleCheck },
  warning: { variant: 'destructive', Icon: TriangleAlert },
  danger: { variant: 'destructive', Icon: OctagonAlert },
} as const;

export function CalloutBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const tone = CALLOUT_TONES.find((candidate) => candidate === props.tone) ?? 'info';
  const style = CALLOUT_STYLE[tone];

  return (
    <Alert
      variant={style.variant}
      Icon={style.Icon}
      title={read(text(props, 'title'))}
      description={read(text(props, 'text'))}
    />
  );
}

// ---- cta -------------------------------------------------------------------------------------

export function CtaBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const href = plain(props, 'href');
  const external = href.startsWith('http://') || href.startsWith('https://');

  return (
    <div>
      <Button asChild>
        {/* An address an editor typed is an address the hub does not own: it never carries our
            referrer and never gets a handle on the window it came from. */}
        <a href={href} {...(external ? { target: '_blank', rel: 'noreferrer noopener' } : {})}>
          {read(text(props, 'label'))}
        </a>
      </Button>
    </div>
  );
}

// ---- linkList (data) -------------------------------------------------------------------------

/** What `LinkListProvider` answers with. Read defensively: it is JSON off the wire. */
interface LinkListData {
  items?: { title?: LocalizedString; url?: string; description?: LocalizedString | null }[];
}

export function LinkListBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const items = (data as LinkListData | null | undefined)?.items;

  if (items === undefined) {
    // Undefined is "the answer is on its way"; an empty array is "there are none", and the two
    // must not look the same.
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (items.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('blocks.linkList.empty')}</p>;
  }

  return (
    <ul className="flex flex-col gap-2">
      {items.map((item) => (
        <li key={item.url} className="flex flex-col">
          <a
            href={item.url}
            className="text-primary underline underline-offset-2"
            target="_blank"
            rel="noreferrer noopener"
          >
            {read(item.title)}
          </a>
          {item.description ? (
            <span className="text-muted-foreground text-sm">{read(item.description)}</span>
          ) : null}
        </li>
      ))}
    </ul>
  );
}

// ---- hero ------------------------------------------------------------------------------------

/** The ground a hero stands on. One of the three blocks allowed one of its own (§1.4). */
const HERO_TONE = {
  plain: 'bg-card text-card-foreground border-border border',
  muted: 'bg-muted text-foreground',
  accent: 'bg-accent text-accent-foreground',
} as const;

export function HeroBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const tone = choice(props, 'tone', ['plain', 'muted', 'accent'] as const, 'muted');
  const align = choice(props, 'align', ['left', 'center'] as const, 'left');
  const picture = media(props, 'mediaId');
  const eyebrow = read(text(props, 'eyebrow'));
  const body = read(text(props, 'text'));

  // The picture stands beside the words rather than behind them. Behind, it would need a veil to
  // keep the text readable, and a veil is a colour that is not a token of the theme.
  const words = (
    <div
      className={`flex flex-col gap-4 ${align === 'center' && picture === null ? 'items-center text-center' : ''}`}
    >
      {eyebrow === '' ? null : (
        <span className="text-muted-foreground text-sm font-semibold tracking-wide uppercase">{eyebrow}</span>
      )}
      <H1>{read(text(props, 'title'))}</H1>
      {body === '' ? null : <p className="max-w-prose text-lg leading-relaxed">{body}</p>}
      <HeroActions props={props} />
    </div>
  );

  return (
    <section className={`rounded-lg p-8 md:p-12 ${HERO_TONE[tone]}`}>
      {picture === null ? (
        words
      ) : (
        <div className="grid grid-cols-1 items-center gap-8 md:grid-cols-2">
          {words}
          <img src={mediaFileUrl(picture)} alt="" className="w-full rounded-lg object-cover" loading="lazy" />
        </div>
      )}
    </section>
  );
}

function HeroActions({ props }: { props: Record<string, unknown> }) {
  const read = useLocalized();
  const buttons = (['primary', 'secondary'] as const)
    .map((name) => ({ name, value: props[name] }))
    .filter(
      (entry): entry is { name: 'primary' | 'secondary'; value: Record<string, unknown> } =>
        entry.value !== null && typeof entry.value === 'object',
    )
    .filter((entry) => plain(entry.value, 'href') !== '');

  if (buttons.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-wrap gap-3">
      {buttons.map((entry) => (
        <Button key={entry.name} asChild variant={entry.name === 'primary' ? 'primary' : 'secondary'}>
          <OutsideLink href={plain(entry.value, 'href')}>{read(text(entry.value, 'label'))}</OutsideLink>
        </Button>
      ))}
    </div>
  );
}

// ---- image -----------------------------------------------------------------------------------

/** How much of its column a picture takes. */
const IMAGE_WIDTH = {
  third: 'max-w-[33%]',
  half: 'max-w-[50%]',
  full: 'w-full',
} as const;

export function ImageBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const id = media(props, 'mediaId');
  const caption = read(text(props, 'caption'));

  if (id === null) {
    return null;
  }

  return (
    <figure
      className={`flex flex-col gap-2 ${IMAGE_WIDTH[choice(props, 'width', ['third', 'half', 'full'] as const, 'full')]}`}
    >
      <img
        src={mediaFileUrl(id)}
        // Empty is a decorative picture, and a decorative picture is one a screen reader skips.
        // Anything else would be reading a file name aloud (design M1 §1.2, decision of 6 Sep 2026).
        alt={read(text(props, 'alt'))}
        className={`w-full ${flag(props, 'rounded', true) ? 'rounded-lg' : ''}`}
        loading="lazy"
      />
      {caption === '' ? null : <figcaption className="text-muted-foreground text-sm">{caption}</figcaption>}
    </figure>
  );
}

// ---- video -----------------------------------------------------------------------------------

const ASPECT = {
  '16x9': 'aspect-video',
  '4x3': 'aspect-[4/3]',
  '1x1': 'aspect-square',
} as const;

export function VideoBlock({ props }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const aspect = ASPECT[choice(props, 'aspect', ['16x9', '4x3', '1x1'] as const, '16x9')];
  const caption = read(text(props, 'caption'));
  const uploaded = media(props, 'mediaId');
  const address = plain(props, 'url');

  // A file of the library plays as itself; an address plays in a frame, and only on a host the
  // allow list knows how to build one for (design M1 §1.2).
  const framed = uploaded === null && address !== '' ? embedSource(address) : null;

  const player =
    uploaded !== null ? (
      <video src={mediaFileUrl(uploaded)} controls className={`w-full rounded-lg ${aspect}`} />
    ) : framed !== null ? (
      <iframe
        src={framed}
        title={caption === '' ? t('blocks.video.label') : caption}
        allowFullScreen
        className={`w-full rounded-lg border-0 ${aspect}`}
      />
    ) : (
      <p className="border-border text-muted-foreground rounded-md border border-dashed p-4 text-sm">
        {t('blocks.video.notAllowed')}
      </p>
    );

  return (
    <figure className="flex flex-col gap-2">
      {player}
      {caption === '' ? null : <figcaption className="text-muted-foreground text-sm">{caption}</figcaption>}
    </figure>
  );
}

// ---- embed -----------------------------------------------------------------------------------

export function EmbedBlock({ props }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const framed = embedSource(plain(props, 'url'));
  const title = read(text(props, 'title'));

  if (framed === null) {
    return (
      <p className="border-border text-muted-foreground rounded-md border border-dashed p-4 text-sm">
        {t('blocks.embed.notAllowed')}
      </p>
    );
  }

  return (
    <iframe
      src={framed}
      // The name of the frame, and the reason `title` is not optional: without it somebody reading
      // with a keyboard finds a box with nothing to say what is inside.
      title={title}
      allowFullScreen
      style={{ height: `${Math.max(120, Math.min(count(props, 'height', 480), 2000))}px` }}
      className="w-full rounded-lg border-0"
    />
  );
}

// ---- timeline --------------------------------------------------------------------------------

export function TimelineBlock({ props }: BlockComponentProps) {
  const { i18n } = useTranslation();
  const read = useLocalized();
  const steps = choice(props, 'variant', ['steps', 'timeline'] as const, 'steps') === 'steps';
  const items = entries(props, 'items');

  const day = (value: unknown): string => {
    if (typeof value !== 'string' || value === '') {
      return '';
    }

    const instant = new Date(value);
    return Number.isNaN(instant.getTime())
      ? ''
      : new Intl.DateTimeFormat(i18n.language, { dateStyle: 'long', timeZone: 'UTC' }).format(instant);
  };

  return (
    <ol
      className={steps ? `grid grid-cols-1 gap-6 ${gridOf(Math.min(items.length || 1, 3))}` : 'flex flex-col'}
    >
      {items.map((item, index) => {
        const when = day(item.date);
        const body = read(text(item, 'text'));

        return (
          <li
            key={index}
            className={
              steps ? 'flex flex-col gap-2' : 'border-border flex flex-col gap-2 border-l pb-6 pl-6 last:pb-0'
            }
          >
            <div className="text-muted-foreground flex items-center gap-2 text-sm">
              {typeof item.icon === 'string' ? (
                <ChosenIcon name={item.icon} className="size-4" />
              ) : (
                // The number is what says "step three of five" when no icon was chosen; on a
                // timeline it is the marker on the line.
                <span className="bg-muted text-muted-foreground flex size-6 items-center justify-center rounded-full text-xs tabular-nums">
                  {index + 1}
                </span>
              )}
              {when === '' ? null : <time className="tabular-nums">{when}</time>}
            </div>
            <H4>{read(text(item, 'title'))}</H4>
            {body === '' ? null : <p className="text-muted-foreground">{body}</p>}
          </li>
        );
      })}
    </ol>
  );
}

// ---- table -----------------------------------------------------------------------------------

const CELL_ALIGN = {
  left: 'text-left',
  center: 'text-center',
  right: 'text-right',
} as const;

export function TableBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const columns = entries(props, 'columns');
  const caption = read(text(props, 'caption'));

  const alignOf = (index: number) =>
    CELL_ALIGN[choice(columns[index] ?? {}, 'align', ['left', 'center', 'right'] as const, 'left')];

  return (
    // No wrapper of our own: Atmosphere's table already puts itself in a box that scrolls
    // (`relative w-full overflow-auto`), which is exactly what a table wider than its column needs.
    // Measured rather than assumed — a second wrapper here passed the e2e that measures the page
    // just as happily with it removed, which is how it was found to be a copy of something that
    // already existed (CLAUDE.md §2).
    <>
      <Table
        {...(caption === '' ? {} : { caption })}
        columns={columns.map((column, index) => ({
          label: read(text(column, 'label')),
          className: alignOf(index),
        }))}
        rows={entries(props, 'rows').map((row) => ({
          columns: entries(row, 'cells').map((cell, index) => ({
            value: read(text(cell, 'text')),
            className: alignOf(index),
          })),
        }))}
      />
    </>
  );
}

// ---- cardGrid --------------------------------------------------------------------------------

export function CardGridBlock({ props }: BlockComponentProps) {
  const read = useLocalized();

  return (
    <div className={`grid grid-cols-1 gap-6 ${gridOf(count(props, 'columns', 3))}`}>
      {entries(props, 'cards').map((card, index) => {
        const picture = media(card, 'mediaId');
        const body = read(text(card, 'text'));
        const href = plain(card, 'href');

        const inside = (
          <CardRoot className="flex h-full flex-col overflow-hidden">
            {picture === null ? null : (
              <img src={mediaFileUrl(picture)} alt="" className="h-40 w-full object-cover" loading="lazy" />
            )}
            <CardContent className="flex flex-col gap-2 p-5">
              {typeof card.icon === 'string' ? (
                <ChosenIcon name={card.icon} className="text-muted-foreground size-6" />
              ) : null}
              <H4>{read(text(card, 'title'))}</H4>
              {body === '' ? null : <p className="text-muted-foreground">{body}</p>}
            </CardContent>
          </CardRoot>
        );

        return (
          <div key={index} className="h-full">
            {href === '' ? (
              inside
            ) : (
              <OutsideLink
                href={href}
                className="focus-visible:ring-ring block h-full rounded-lg focus-visible:ring-2 focus-visible:outline-none"
              >
                {inside}
              </OutsideLink>
            )}
          </div>
        );
      })}
    </div>
  );
}

// ---- iconGrid --------------------------------------------------------------------------------

export function IconGridBlock({ props }: BlockComponentProps) {
  const read = useLocalized();

  return (
    <div className={`grid grid-cols-1 gap-6 ${gridOf(count(props, 'columns', 3))}`}>
      {entries(props, 'items').map((item, index) => {
        const body = read(text(item, 'text'));

        return (
          <div key={index} className="flex flex-col gap-2">
            <ChosenIcon name={plain(item, 'icon')} className="text-primary size-8" />
            <H4>{read(text(item, 'title'))}</H4>
            {body === '' ? null : <p className="text-muted-foreground">{body}</p>}
          </div>
        );
      })}
    </div>
  );
}

// ---- gallery ---------------------------------------------------------------------------------

export function GalleryBlock({ props }: BlockComponentProps) {
  const lightbox = flag(props, 'lightbox', true);

  return (
    <ul className={`grid grid-cols-2 gap-3 ${gridOf(count(props, 'columns', 3))}`}>
      {entries(props, 'images').map((image, index) => {
        const id = media(image, 'mediaId');
        if (id === null) {
          return null;
        }

        const picture = (
          <img
            src={mediaFileUrl(id)}
            alt=""
            className="aspect-square w-full rounded-md object-cover"
            loading="lazy"
          />
        );

        return (
          <li key={index}>
            {/* "Lightbox" here is the file itself, opened in a tab of its own. A dialog of our own
                making would be a custom component, and that list is closed (design M1 §12). */}
            {lightbox ? <OutsideLink href={mediaFileUrl(id)}>{picture}</OutsideLink> : picture}
          </li>
        );
      })}
    </ul>
  );
}

// ---- logoGrid --------------------------------------------------------------------------------

export function LogoGridBlock({ props }: BlockComponentProps) {
  return (
    <ul className={`grid grid-cols-2 items-center gap-6 ${gridOf(count(props, 'columns', 4))}`}>
      {entries(props, 'items').map((item, index) => {
        const id = media(item, 'mediaId');
        if (id === null) {
          return null;
        }

        // The name is the alternative text: a reader who cannot see the logo is told whose it is,
        // which is the only thing the picture was saying.
        const logo = (
          <img
            src={mediaFileUrl(id)}
            alt={plain(item, 'name')}
            className="h-12 w-full object-contain"
            loading="lazy"
          />
        );

        const href = plain(item, 'href');

        return <li key={index}>{href === '' ? logo : <OutsideLink href={href}>{logo}</OutsideLink>}</li>;
      })}
    </ul>
  );
}

// ---- tabs ------------------------------------------------------------------------------------

export function TabsBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const items = entries(props, 'tabs');

  if (items.length === 0) {
    return null;
  }

  const tabs = Object.fromEntries(
    items.map((tab, index) => [
      String(index),
      {
        trigger: read(text(tab, 'label')),
        // Markdown per entry, never blocks: the same sanitized renderer as `text` (design §1.5).
        content: (
          <div className="pt-4">
            <MarkdownContent source={read(text(tab, 'body'))} />
          </div>
        ),
      },
    ]),
  );

  // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]` (HANDOFF §13).
  return <Tabs className="w-full" tabs={tabs} defaultValue="0" />;
}

// ---- accordion -------------------------------------------------------------------------------

export function AccordionBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const items = entries(props, 'items').map((item, index) => (
    <AccordionItem key={index} value={String(index)}>
      <AccordionTrigger>{read(text(item, 'question'))}</AccordionTrigger>
      <AccordionContent>
        <MarkdownContent source={read(text(item, 'answer'))} />
      </AccordionContent>
    </AccordionItem>
  ));

  // Two calls rather than a computed `type`: the two shapes of the underlying component take
  // different properties, and one of them may only be collapsed when it is on its own.
  return flag(props, 'allowMultiple', false) ? (
    <AccordionRoot type="multiple" className="w-full">
      {items}
    </AccordionRoot>
  ) : (
    <AccordionRoot type="single" collapsible className="w-full">
      {items}
    </AccordionRoot>
  );
}

// ---- testimonial -----------------------------------------------------------------------------

export function TestimonialBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const portrait = media(props, 'mediaId');
  const role = read(text(props, 'role'));

  return (
    <figure className="bg-muted flex flex-col gap-4 rounded-lg p-6">
      <BlockQuote className="text-lg">{read(text(props, 'quote'))}</BlockQuote>
      <figcaption className="flex items-center gap-3">
        {portrait === null ? null : (
          <img
            src={mediaFileUrl(portrait)}
            alt=""
            className="size-10 rounded-full object-cover"
            loading="lazy"
          />
        )}
        <span className="flex flex-col">
          <span className="font-semibold">{plain(props, 'author')}</span>
          {role === '' ? null : <span className="text-muted-foreground text-sm">{role}</span>}
        </span>
      </figcaption>
    </figure>
  );
}

// ---- buttonGroup -----------------------------------------------------------------------------

const GROUP_ALIGN = {
  left: 'justify-start',
  center: 'justify-center',
  right: 'justify-end',
} as const;

const BUTTON_VARIANT = {
  primary: 'primary',
  secondary: 'secondary',
  ghost: 'ghost',
} as const;

export function ButtonGroupBlock({ props }: BlockComponentProps) {
  const read = useLocalized();
  const align = GROUP_ALIGN[choice(props, 'align', ['left', 'center', 'right'] as const, 'left')];

  return (
    <div className={`flex flex-wrap gap-3 ${align}`}>
      {entries(props, 'buttons').map((button, index) => (
        <Button
          key={index}
          asChild
          variant={
            BUTTON_VARIANT[choice(button, 'variant', ['primary', 'secondary', 'ghost'] as const, 'primary')]
          }
        >
          <OutsideLink href={plain(button, 'href')}>{read(text(button, 'label'))}</OutsideLink>
        </Button>
      ))}
    </div>
  );
}

// ---- spacer ----------------------------------------------------------------------------------

const SPACER_HEIGHT = {
  sm: 'h-4',
  md: 'h-8',
  lg: 'h-16',
  xl: 'h-24',
} as const;

export function SpacerBlock({ props }: BlockComponentProps) {
  return (
    <div
      aria-hidden
      className={SPACER_HEIGHT[choice(props, 'size', ['sm', 'md', 'lg', 'xl'] as const, 'md')]}
    />
  );
}

// ---- divider ---------------------------------------------------------------------------------

const DIVIDER_SPACE = {
  sm: 'py-2',
  md: 'py-4',
  lg: 'py-8',
} as const;

export function DividerBlock({ props }: BlockComponentProps) {
  const space = DIVIDER_SPACE[choice(props, 'spacing', ['sm', 'md', 'lg'] as const, 'md')];

  if (choice(props, 'variant', ['line', 'dots'] as const, 'line') === 'dots') {
    return (
      <div className={`flex items-center justify-center gap-2 ${space}`} aria-hidden>
        {[0, 1, 2].map((dot) => (
          <span key={dot} className="bg-muted-foreground size-1.5 rounded-full" />
        ))}
      </div>
    );
  }

  return (
    <div className={space}>
      <Separator />
    </div>
  );
}
