import { H1, Lead } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

/**
 * The public page of the ATC operations module. A placeholder, and deliberately one: what the
 * department will actually publish here — the roster, the bookings, who is online — is M3.
 *
 * What it proves today is the whole of design M0 §6.5 in one screen: this file lives inside
 * `web/src/modules/atc/`, the core never imports it, the route that shows it is registered from the
 * module's own manifest, and its words come from the module's own i18n namespace.
 */
export function AtcPage() {
  const { t } = useTranslation();

  return (
    <>
      <H1>{t('atc.title')}</H1>
      <Lead>{t('atc.lead')}</Lead>
    </>
  );
}
