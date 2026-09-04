import { Alert, Button, H1, H2, H3, H4 } from '@ivao/atmosphere-react';
import { CircleCheck, Info, OctagonAlert, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../shared/api/bootstrap';
import { useLocalized } from '../shared/i18n/useLocalized';
import type { BlockComponentProps } from '../shared/modules';
import { MarkdownContent } from '../shared/ui';

import { CALLOUT_TONES } from './schemas';

/**
 * How the five blocks of the core are drawn (design M0 §5.4). Four of them draw what an editor
 * typed; the fifth draws what the hub knows, which is what makes live and frozen visible at all.
 *
 * A component is handed its properties and draws them. It decides nothing about the page around
 * it, and it never reads the language off a prop: `useLocalized` knows which one is on screen.
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
