import { Button, Input, Textarea } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';
import { useFormContext, useWatch } from 'react-hook-form';

import { LocaleTabs } from './LocaleTabs';

/**
 * One translated field, a tab per language of the division. A field is a single JSON column and
 * never a row per language (plan §16.1), so what is on screen is one value with several entries.
 *
 * The frame — the tabs and the badge that says a language is still empty — is `LocaleTabs`, which
 * a translated *object* uses too. What belongs to a translated string, and lives here, is the copy
 * button: the common case is a coordinator writing the Italian first and wanting the English to
 * start from it rather than from nothing.
 */
export function LocaleFields({
  path,
  label,
  hint,
  locales,
  multiline,
  error,
}: {
  path: string;
  label: string;
  /** The sentence under the field, when `<ns>.hints.<path>` exists. See `SchemaForm`. */
  hint?: string | undefined;
  locales: readonly string[];
  multiline: boolean;
  error: string | undefined;
}) {
  const { t, i18n } = useTranslation();
  const { register, control, setValue } = useFormContext();
  const value = (useWatch({ control, name: path }) ?? {}) as Record<string, string>;

  // "it" reads "Italian" to an English speaker and "italiano" to an Italian one: the browser owns
  // that table, so the division does not carry a name for every language it might add.
  const names = new Intl.DisplayNames([i18n.language], { type: 'language' });
  const nameOf = (locale: string) => names.of(locale) ?? locale;

  const written = (locale: string) => (value[locale] ?? '').trim().length > 0;

  return (
    <LocaleTabs
      label={label}
      hint={hint}
      locales={locales}
      isWritten={written}
      error={error}
      renderContent={(locale) => (
        <>
          {multiline ? (
            <Textarea id={`${path}.${locale}`} rows={6} {...register(`${path}.${locale}`)} />
          ) : (
            <Input id={`${path}.${locale}`} {...register(`${path}.${locale}`)} />
          )}

          <div className="flex flex-wrap gap-2">
            {locales
              .filter((other) => other !== locale && written(other))
              .map((other) => (
                <Button
                  key={other}
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={() =>
                    setValue(`${path}.${locale}`, value[other] ?? '', {
                      shouldDirty: true,
                      shouldValidate: true,
                    })
                  }
                >
                  {t('form.copyFrom', { language: nameOf(other) })}
                </Button>
              ))}
          </div>
        </>
      )}
    />
  );
}
