import { createContext, useContext } from 'react';

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
