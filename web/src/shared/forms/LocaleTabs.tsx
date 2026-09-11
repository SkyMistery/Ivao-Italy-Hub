import { Badge, Label, Tabs } from '@ivao/atmosphere-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { usePreviewLocale } from '../i18n/previewLocale';

import { FieldHint } from './SchemaForm';

/**
 * One tab per language of the division, with a badge on the ones nothing has been written into yet.
 *
 * It exists because two different fields need exactly this frame: a translated string
 * (`LocaleFields`) and a translated object (`Seo`, and whatever M1 adds after it). What differs is
 * only what goes inside a tab, so that is the parameter and the rest is written once.
 *
 * ⚠️ `w-full` is not decoration. Atmosphere's `Tabs` pins itself to `w-[400px]`, so without it a
 * translated field is drawn 400px wide next to inputs the width of the form. The class merges
 * rather than fights because that library passes it through `cn`.
 */
export function LocaleTabs({
  label,
  hint,
  locales,
  isWritten,
  renderContent,
  error,
}: {
  label: string;
  hint?: string | undefined;
  locales: readonly string[];
  /** Whether that language holds anything: what puts the "empty" badge on a tab. */
  isWritten: (locale: string) => boolean;
  renderContent: (locale: string) => ReactNode;
  error: string | undefined;
}) {
  const { t, i18n } = useTranslation();
  const names = new Intl.DisplayNames([i18n.language], { type: 'language' });

  // The tab that opens: the language the page is being looked at in, or the site's. Before this
  // the first language of the division opened, whatever was on screen — Italian in the form,
  // English on the page, and what was typed changed nothing anybody could see.
  const preferred = usePreviewLocale() ?? i18n.language;
  const opening = locales.includes(preferred) ? preferred : locales[0];

  const tabs = Object.fromEntries(
    locales.map((locale) => [
      locale,
      {
        trigger: (
          <span className="flex items-center gap-2">
            {names.of(locale) ?? locale}
            {isWritten(locale) ? null : (
              <Badge variant="flat" color="yellow" size="sm" text={t('form.empty')} />
            )}
          </span>
        ),
        content: <div className="flex flex-col gap-2 pt-2">{renderContent(locale)}</div>,
      },
    ]),
  );

  return (
    <fieldset className="flex flex-col gap-1">
      <Label asChild>
        <legend>{label}</legend>
      </Label>
      <FieldHint hint={hint} />
      {/* Remounted when the language looked at changes, so the open tab follows it: the tabs are
          uncontrolled, and a default only counts once. */}
      <Tabs
        key={opening}
        className="w-full"
        tabs={tabs}
        {...(opening === undefined ? {} : { defaultValue: opening })}
      />
      {error === undefined ? null : (
        <p role="alert" className="text-destructive text-sm">
          {error}
        </p>
      )}
    </fieldset>
  );
}
