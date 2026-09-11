import { Button } from '@ivao/atmosphere-react';
import { Monitor, Smartphone, Tablet } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, PickingContext, type Body } from '../../blocks';

/**
 * The draft as a visitor would read it, at three widths (design M1 §9.3).
 *
 * ⚠️ It is **not** an emulator and must not grow into one: no device frame, no user agent, no
 * touch. It is a `max-width` on the very same renderer the public site uses — which is the whole
 * point of having one renderer, and the reason "what will this look like" cannot disagree with
 * "what this looks like". A page that breaks at 390 pixels breaks here too, because the CSS that
 * decides is the page's own.
 */

const WIDTHS = [
  { key: 'phone', maxWidth: 390, Icon: Smartphone },
  { key: 'tablet', maxWidth: 768, Icon: Tablet },
  /** As wide as it is given, which is what the editor's own column already is. */
  { key: 'desktop', maxWidth: null, Icon: Monitor },
] as const;

type PreviewWidth = (typeof WIDTHS)[number]['key'];

/** What the frame can show instead of the draft: the version visitors read now, or why not. */
export type PublishedView =
  | { readonly state: 'loading' }
  | { readonly state: 'none' }
  | { readonly state: 'ready'; readonly body: Body };

export function PreviewFrame({
  body,
  locales,
  locale,
  onLocale,
  published,
  comparing = false,
  onCompare,
}: {
  body: Body;
  /** The languages of the division and the one the page is drawn in (Carmine, 11 September 2026). */
  locales?: readonly string[] | undefined;
  locale?: string | undefined;
  onLocale?: ((locale: string) => void) | undefined;
  /**
   * The published version, when the frame is asked to show it instead of the draft. Absent for a
   * row that does not exist yet: there is nothing to compare with.
   */
  published?: PublishedView | undefined;
  comparing?: boolean;
  onCompare?: ((comparing: boolean) => void) | undefined;
}) {
  const { t, i18n } = useTranslation();
  const [width, setWidth] = useState<PreviewWidth>('desktop');

  const chosen = WIDTHS.find((candidate) => candidate.key === width) ?? WIDTHS[2];
  const names = new Intl.DisplayNames([i18n.language], { type: 'language' });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-3">
        <div role="group" aria-label={t('content.editor.previewWidth')} className="flex items-center gap-1">
          {WIDTHS.map(({ key, Icon }) => {
            const label = t(`content.editor.previewWidths.${key}`);

            return (
              <Button
                key={key}
                type="button"
                size="sm"
                variant={key === width ? 'secondary' : 'ghost'}
                aria-label={label}
                aria-pressed={key === width}
                title={label}
                onClick={() => setWidth(key)}
              >
                <Icon aria-hidden className="size-4" />
              </Button>
            );
          })}
        </div>

        {/* Which language the page is drawn in — and, through `PreviewLocaleContext`, which tab of
            every translated field opens. The site's own language does not change. */}
        {locales !== undefined && locales.length > 1 && onLocale !== undefined ? (
          <div
            role="group"
            aria-label={t('content.editor.previewLanguage')}
            className="flex items-center gap-1"
          >
            {locales.map((candidate) => (
              <Button
                key={candidate}
                type="button"
                size="sm"
                variant={candidate === locale ? 'secondary' : 'ghost'}
                aria-pressed={candidate === locale}
                title={names.of(candidate) ?? candidate}
                onClick={() => onLocale(candidate)}
              >
                {candidate.toUpperCase()}
              </Button>
            ))}
          </div>
        ) : null}

        {/* The draft, or what visitors read now (Carmine, 11 September 2026: "compare with the
            published version — decidedly yes"). The published one is drawn by the same renderer
            with no picking around it: it is looked at, not composed. */}
        {published !== undefined && onCompare !== undefined ? (
          <div
            role="group"
            aria-label={t('content.editor.compare.label')}
            className="ml-auto flex items-center gap-1"
          >
            <Button
              type="button"
              size="sm"
              variant={comparing ? 'ghost' : 'secondary'}
              aria-pressed={!comparing}
              onClick={() => onCompare(false)}
            >
              {t('content.editor.compare.draft')}
            </Button>
            <Button
              type="button"
              size="sm"
              variant={comparing ? 'secondary' : 'ghost'}
              aria-pressed={comparing}
              onClick={() => onCompare(true)}
            >
              {t('content.editor.compare.published')}
            </Button>
          </div>
        ) : null}
      </div>

      {comparing && published !== undefined ? (
        <p
          role="status"
          aria-label={t('content.editor.compare.label')}
          className="text-muted-foreground text-sm"
        >
          {published.state === 'ready'
            ? t('content.editor.compare.showingPublished')
            : published.state === 'none'
              ? t('content.editor.compare.none')
              : t('content.editor.compare.loading')}
        </p>
      ) : null}

      <div className="border-border overflow-hidden rounded-lg border">
        <div
          // Named, because it is what the suite measures: a narrow preview that quietly stayed the
          // width of its column would look exactly like a working one in a screenshot.
          role="region"
          aria-label={t('content.editor.preview')}
          className="mx-auto"
          style={chosen.maxWidth === null ? undefined : { maxWidth: `${chosen.maxWidth}px` }}
        >
          {comparing && published !== undefined ? (
            published.state === 'ready' ? (
              <PickingContext.Provider value={null}>
                <ContentRenderer body={published.body} staff />
              </PickingContext.Provider>
            ) : null
          ) : (
            <ContentRenderer body={body} staff />
          )}
        </div>
      </div>
    </div>
  );
}
