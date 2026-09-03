import { Badge, Button, Input, Label, Tabs, Textarea } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';
import { useFormContext, useWatch } from 'react-hook-form';

/**
 * One translated field, a tab per language of the division. A field is a single JSON column and
 * never a row per language (plan §16.1), so what is on screen is one value with several entries.
 *
 * The badge on a tab says whether that language has been written yet, and the button copies from a
 * language that has: the common case is a coordinator writing the Italian first and wanting the
 * English to start from it rather than from nothing.
 */
export function LocaleFields({
  path,
  label,
  locales,
  multiline,
  error,
}: {
  path: string;
  label: string;
  locales: readonly string[];
  multiline: boolean;
  error: string | undefined;
}) {
  const { t, i18n } = useTranslation();
  const { register, control, setValue } = useFormContext();
  const value = (useWatch({ control, name: path }) ?? {}) as Record<string, string>;

  const names = new Intl.DisplayNames([i18n.language], { type: 'language' });
  const written = (locale: string) => (value[locale] ?? '').trim().length > 0;

  const tabs = Object.fromEntries(
    locales.map((locale) => [
      locale,
      {
        trigger: (
          <span className="flex items-center gap-2">
            {names.of(locale) ?? locale}
            {written(locale) ? null : (
              <Badge variant="flat" color="yellow" size="sm" text={t('form.empty')} />
            )}
          </span>
        ),
        content: (
          <div className="flex flex-col gap-2 pt-2">
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
                    {t('form.copyFrom', { language: names.of(other) ?? other })}
                  </Button>
                ))}
            </div>
          </div>
        ),
      },
    ]),
  );

  return (
    <fieldset className="flex flex-col gap-1">
      <Label asChild>
        <legend>{label}</legend>
      </Label>
      <Tabs tabs={tabs} {...(locales[0] === undefined ? {} : { defaultValue: locales[0] })} />
      {error === undefined ? null : (
        <p role="alert" className="text-destructive text-sm">
          {error}
        </p>
      )}
    </fieldset>
  );
}
