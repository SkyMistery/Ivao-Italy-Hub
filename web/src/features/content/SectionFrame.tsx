import { Ban, Image as ImageIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { BACKGROUNDS, LAYOUTS, columnsOf, type Background, type Layout } from '../../blocks';

/**
 * The two things about a section you judge by eye: what sits behind it, and how its blocks are
 * divided into columns. Pictures, one click, applied at once — no form and no "apply".
 *
 * ⚠️ Why they are **not** in the form beside it. `SchemaForm` draws a select and waits for a button,
 * which is right for a title or an address: you write, you read it back, you save. A background is
 * not written, it is *chosen while looking at the page* — and since 9 September the page is right
 * there. HQ's builder puts the same two on the section's own bar for the same reason; ours are in
 * the panel rather than over the page because the renderer is shared with the public site and must
 * not grow editor chrome (`blocks/picking.ts`).
 *
 * They are therefore **out** of `sectionSettingsSchema`: one place each, or a form holding a stale
 * background would undo a swatch the moment somebody pressed Apply.
 */

/**
 * What each background looks like in the strip. The renderer's own classes, so they cannot drift.
 * Two of the eight are not a colour and say so with a glyph (Carmine, 11 September 2026, with a
 * screenshot: the picture used to be a diagonal stripe that read as "forbidden", and "none" was a
 * white dot on a white panel): "none" is a struck circle, the picture is a picture.
 */
const SWATCH: Record<Background, string> = {
  none: 'bg-body border-border text-muted-foreground',
  muted: 'bg-muted border-muted',
  // The two that changed on 12 September: `accent` is the brand's pale blue rather than the theme's
  // fourth grey, and `aurora` is the fourth dark ground. Both spelled as the renderer spells them.
  accent: 'bg-ocean-50 border-ocean-50 dark:bg-ocean-900 dark:border-ocean-900',
  brand: 'bg-atmos-700 border-atmos-700',
  deep: 'bg-atmos-800 border-atmos-800',
  dark: 'bg-fuselage-900 border-fuselage-900',
  aurora: 'bg-product-aurora-dark border-product-aurora-dark',
  // A picture is chosen in the form below — this only says which of the grounds is on.
  image: 'bg-muted border-border text-muted-foreground',
};

const GLYPH: Partial<Record<Background, typeof Ban>> = { none: Ban, image: ImageIcon };

export function SectionFrame({
  background,
  layout,
  onBackground,
  onLayout,
}: {
  background: Background;
  layout: Layout;
  onBackground: (next: Background) => void;
  onLayout: (next: Layout) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-3">
      <Choice label={t('content.section.fields.background')}>
        {BACKGROUNDS.map((value) => {
          const Glyph = GLYPH[value];

          return (
            <button
              key={value}
              type="button"
              aria-pressed={value === background}
              title={t(`content.section.options.background.${value}`)}
              aria-label={t(`content.section.options.background.${value}`)}
              onClick={() => onBackground(value)}
              className={`flex size-7 items-center justify-center rounded-full border-2 ${SWATCH[value]} ${
                value === background ? 'ring-primary ring-2 ring-offset-2' : ''
              }`}
            >
              {Glyph === undefined ? null : <Glyph aria-hidden className="size-3.5" />}
            </button>
          );
        })}
      </Choice>

      <Choice label={t('content.section.fields.layout')}>
        {LAYOUTS.map((value) => (
          <button
            key={value}
            type="button"
            aria-pressed={value === layout}
            title={t(`content.section.options.layout.${value}`)}
            aria-label={t(`content.section.options.layout.${value}`)}
            onClick={() => onLayout(value)}
            className={`flex h-7 w-12 items-center gap-0.5 rounded-sm border p-1 ${
              value === layout ? 'border-primary ring-primary ring-1' : 'border-border'
            }`}
          >
            {/* The picture is the layout: as many bars as columns, in the proportions the renderer
                gives them. A name in a select could not say "one third and two thirds" as fast. */}
            {sharesOf(value).map((share, index) => (
              <span
                key={index}
                style={{ flexGrow: share }}
                className={`h-full rounded-xs ${value === layout ? 'bg-primary' : 'bg-muted-foreground/40'}`}
              />
            ))}
          </button>
        ))}
      </Choice>
    </div>
  );
}

/** The share of the width each column takes, which is what the diagram draws. */
function sharesOf(layout: Layout): number[] {
  switch (layout) {
    case '1/2+1/2':
      return [1, 1];
    case '1/3+2/3':
      return [1, 2];
    case '2/3+1/3':
      return [2, 1];
    case '3x1/3':
      return [1, 1, 1];
    default:
      return Array.from({ length: columnsOf(layout) }, () => 1);
  }
}

function Choice({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <span className="text-muted-foreground text-xs font-medium">{label}</span>
      {/* Room for the ring of the chosen one, which sits outside its button: the panel scrolls and
          therefore clips, and the first swatch against its edge lost the left of its ring (Carmine,
          11 September 2026, with a screenshot). */}
      <div className="flex flex-wrap items-center gap-2 p-1">{children}</div>
    </div>
  );
}
