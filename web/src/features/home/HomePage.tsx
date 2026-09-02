import { H1, Lead, Navbar } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

/**
 * Placeholder home page. In M1 the public site renders a published `cms_contents` row and the
 * title comes from `division.name` through `/api/me` instead of a translation key.
 */
export function HomePage() {
  const { t } = useTranslation();

  return (
    <div className="min-h-screen bg-body text-foreground">
      <Navbar title={t('app.title')} />
      <main className="mx-auto flex max-w-3xl flex-col gap-4 px-4 py-10">
        <H1>{t('home.heading')}</H1>
        <Lead>{t('home.lead')}</Lead>
      </main>
    </div>
  );
}
