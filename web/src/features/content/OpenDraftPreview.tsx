import { Button } from '@ivao/atmosphere-react';
import { useRouter } from '@tanstack/react-router';
import { ExternalLink } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { useNotice } from '../../shared/ui';

/**
 * «Open the preview» in the toolbar of the editor (design of phase 3, §4.7): what is on screen is stored first, then the saved
 * draft opens in a new tab, in the language the editor is showing.
 *
 * ⚠️ The tab opens **on the press**, empty, and is sent to the draft once it is stored. A browser lets a page open a window only
 * during the gesture that asks for it, and how long a gesture lasts is the browser's: `window.open` after an `await` is what a
 * popup blocker stops on a slow save, so that the button would work until the day it did not. Not the other way round either, a
 * link that opens at once with the save running behind it: the tab could read the draft before the save arrives and show
 * something older than what is on screen.
 *
 * `noopener` would make `window.open` answer `null`, and there would be no tab to send anywhere: the link back is cut by hand.
 */
export function OpenDraftPreview({
  id,
  locale,
  flush,
  disabled,
}: {
  id: number;
  locale: string;
  /** Stores what is on screen; false when it could not, and the line under the toolbar has said why. */
  flush: () => Promise<boolean>;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const notice = useNotice();
  const router = useRouter();

  const open = async () => {
    const tab = window.open('about:blank', '_blank');

    if (tab === null) {
      // A blocker that stops even this: nothing was saved for a tab that is not there, and the person is told what to allow.
      notice({ tone: 'error', title: t('content.editor.openPreview.blocked') });
      return;
    }

    tab.opener = null;

    if (!(await flush())) {
      tab.close();
      notice({ tone: 'error', title: t('content.editor.openPreview.notSaved') });
      return;
    }

    tab.location.href = router.buildLocation({
      to: '/staff/content/$id/preview',
      params: { id: String(id) },
      search: { lang: locale },
    }).href;
  };

  return (
    <Button
      type="button"
      variant="ghost"
      disabled={disabled}
      title={t('content.editor.openPreview.hint')}
      onClick={() => void open()}
    >
      <ExternalLink className="size-4" aria-hidden />
      {t('content.editor.openPreview.label')}
    </Button>
  );
}
