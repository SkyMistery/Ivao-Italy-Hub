import { createContext, useContext } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * The language a page is being *looked at* in, when it is not the language of the site.
 *
 * The editor reads it (Carmine, 11 September 2026): the site was in English, the form opened on
 * the Italian tab, and what was typed changed nothing on the page — it looked broken. So the
 * editor lets the person choose which language the page is drawn in, and every translated value
 * read on screen (`useLocalized`) and every tab of a translated field (`LocaleTabs`) follows that
 * choice. Everywhere else it is `null`, and the site's language decides as it always did.
 */
export const PreviewLocaleContext = createContext<string | null>(null);

export function usePreviewLocale(): string | null {
  return useContext(PreviewLocaleContext);
}

/**
 * Which of a field's languages is the one on screen: the language the page is being looked at in, or the site's, when
 * the field has it; its first language otherwise. The tab a translated field opens on (`LocaleTabs`), and the language a
 * table pasted or imported is written in (`TableTools`). Before it the first language of the division opened, whatever
 * was on screen — Italian in the form, English on the page, and what was typed changed nothing anybody could see.
 */
export function useLocaleOnScreen(locales: readonly string[]): string | undefined {
  const { i18n } = useTranslation();
  const preferred = usePreviewLocale() ?? i18n.language;
  return locales.includes(preferred) ? preferred : locales[0];
}
