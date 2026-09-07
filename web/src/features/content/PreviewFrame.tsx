import { Button } from '@ivao/atmosphere-react';
import { Monitor, Smartphone, Tablet } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, type Body } from '../../blocks';

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

export function PreviewFrame({ body }: { body: Body }) {
  const { t } = useTranslation();
  const [width, setWidth] = useState<PreviewWidth>('desktop');

  const chosen = WIDTHS.find((candidate) => candidate.key === width) ?? WIDTHS[2];

  return (
    <div className="flex flex-col gap-3">
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

      <div className="border-border overflow-hidden rounded-lg border">
        <div
          // Named, because it is what the suite measures: a narrow preview that quietly stayed the
          // width of its column would look exactly like a working one in a screenshot.
          role="region"
          aria-label={t('content.editor.preview')}
          className="mx-auto"
          style={chosen.maxWidth === null ? undefined : { maxWidth: `${chosen.maxWidth}px` }}
        >
          <ContentRenderer body={body} staff />
        </div>
      </div>
    </div>
  );
}
