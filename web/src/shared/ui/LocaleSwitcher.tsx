import { Select } from '@ivao/atmosphere-react';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { LOCALE_COOKIE } from '../../app/i18n';
import { api, unwrap } from '../api/client';

/**
 * Which language the hub is read in.
 *
 * Two things remember it, and on purpose. The `hub.lang` cookie is the browser's answer, written
 * here, and it is all an anonymous visitor has; `hub_users.locale` is the member's answer, written
 * by `PUT /api/me/locale`, and it follows them to another browser. Neither writes the other's
 * (design M0 §7.6).
 *
 * A visitor who is not signed in simply does not make the call, and nothing about the switcher
 * changes: the division decides which languages exist, not the session.
 */
export function LocaleSwitcher({ locales, signedIn }: { locales: readonly string[]; signedIn: boolean }) {
  const { i18n } = useTranslation();

  const remember = useMutation({
    mutationFn: async (locale: string) => {
      unwrap(await api.PUT('/api/me/locale', { body: { locale } }));
    },
  });

  // The browser owns the names of languages, so the division does not have to carry one per
  // language it might add.
  const names = new Intl.DisplayNames([i18n.language], { type: 'language' });
  const current = locales.find((locale) => i18n.language.startsWith(locale)) ?? locales[0];

  const choose = (locale: string) => {
    void i18n.changeLanguage(locale);
    document.cookie = `${LOCALE_COOKIE}=${locale}; path=/; max-age=31536000; samesite=lax`;
    if (signedIn) {
      remember.mutate(locale);
    }
  };

  return (
    <Select
      {...(current === undefined ? {} : { value: current })}
      onValueChange={choose}
      items={locales.map((locale) => ({ value: locale, label: names.of(locale) ?? locale }))}
    />
  );
}
