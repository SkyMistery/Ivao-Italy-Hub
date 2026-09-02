import { H1, Lead } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

/**
 * Placeholder home page. In M1 the public site renders a published `cms_contents` row; the shell
 * around it already takes its title from `division.name` through `/api/me`.
 */
export function HomePage() {
  const { t } = useTranslation();

  return (
    <>
      <H1>{t('home.heading')}</H1>
      <Lead>{t('home.lead')}</Lead>
    </>
  );
}
