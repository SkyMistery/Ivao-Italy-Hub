import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Checkbox, H4, Input, Label, Select, Subtle, Switch, Textarea } from '@ivao/atmosphere-react';
import { ChevronDown, ChevronUp, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  Controller,
  FormProvider,
  useFieldArray,
  useForm,
  useFormContext,
  useWatch,
  type FieldErrors,
  type UseFormReturn,
} from 'react-hook-form';
import { useEffect, useRef, type ReactElement } from 'react';
import type { z } from 'zod';

import { ICON_NAMES, iconByName } from '../icons';
import { MediaPicker, type MediaLibraryQuery } from '../ui/MediaPicker';

import { LocaleFields } from './LocaleFields';
import { LocaleTabs } from './LocaleTabs';
import { ProblemAlert } from './ProblemAlert';
import { NO_CHOICE, blankEntry, readFields, type FieldNode } from './schema';
import { slugify } from './slug';
import { useProblemDetails } from './useProblemDetails';

/**
 * What a field needs to know beyond its own schema. One object rather than four props because it
 * is threaded through every level of a nested form, and because two of the four only matter to two
 * kinds of field: a form with no media field never has to be handed a library.
 */
interface FormEnvironment {
  locales: readonly string[];
  labels: string;
  /** The page a media field chooses from. A media field without one throws, and says why. */
  mediaLibrary?: MediaLibraryQuery | undefined;
  /** The two facts about the division a media field and an instant need. */
  division?: { defaultLocale: string; timezone: string } | undefined;
}

/**
 * The form generator. A back office screen declares a zod schema that mirrors the write DTO and
 * gets the form; it never writes a field, a label or an error line by hand (design M0 §7.5).
 *
 * The same generator draws the properties of a block, which is why nothing here knows what an
 * entity is: it is handed a schema and a prefix for the labels, and an entity and a block look
 * exactly alike from in here.
 *
 * Labels come from i18n under `<labels>.fields.<path>`, and the choices of a select under
 * `<labels>.options.<path>.<value>`: a screen carries no user facing string either.
 */
export function SchemaForm<TValues extends Record<string, unknown>>({
  schema,
  defaults,
  locales,
  labels,
  onSubmit,
  submitLabel,
  secondaryAction,
  mediaLibrary,
  division,
}: {
  schema: z.ZodType<TValues, TValues>;
  defaults: TValues;
  /** The languages of the division; a translated field gets one tab per language. */
  locales: readonly string[];
  /** i18n prefix for labels, for example `links`. */
  labels: string;
  /** Rejecting with an `ApiError` is how the server's refusal reaches the fields. */
  onSubmit: (values: TValues) => Promise<unknown>;
  submitLabel: string;
  secondaryAction?: React.ReactNode;
  /**
   * The page of the media library a `.meta({ media: true })` field chooses from. The generator
   * cannot build it: which department's library to show is a fact of the screen, not of the schema.
   */
  mediaLibrary?: MediaLibraryQuery;
  /**
   * The default language and the time zone of the division. Only two kinds of field need them — a
   * media field, to announce a thumbnail, and an instant, to say what a UTC time is locally — so a
   * form with neither is never asked for them.
   */
  division?: { defaultLocale: string; timezone: string };
}) {
  const { t } = useTranslation();
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: defaults as never,
  });
  const problem = useProblemDetails(form);
  const fields = readFields(schema);
  const env: FormEnvironment = { locales, labels, mediaLibrary, division };

  useProposedSlugs(form, fields, division?.defaultLocale);

  const submit = form.handleSubmit(async (values) => {
    problem.reset();
    try {
      await onSubmit(values);
    } catch (error) {
      problem.apply(error);
    }
  });

  return (
    <FormProvider {...form}>
      <form onSubmit={(event) => void submit(event)} className="flex flex-col gap-6" noValidate>
        <ProblemAlert summary={problem.summary} />

        <div className="flex flex-col gap-5">
          {fields.map((field) => (
            <Field key={field.path} node={field} env={env} />
          ))}
        </div>

        <div className="flex flex-wrap items-center gap-3">
          <Button type="submit" isLoading={form.formState.isSubmitting}>
            {submitLabel}
          </Button>
          {secondaryAction}
          <span className="sr-only">{t('form.submitHint')}</span>
        </div>
      </form>
    </FormProvider>
  );
}

/**
 * An address that writes itself from the title, until somebody writes it themselves.
 *
 * Asked for by Carmine after the demo of M1: the slug was typed from scratch next to a title that
 * had just been written, and `ContentWriteDtoValidator` had been saying "the editor proposes one
 * from the title" since M0 without anybody having built that half.
 *
 * The whole subtlety is when to **stop**. Following the title for ever would rewrite the address of
 * a page under the person editing its title, and an address outlives the page; following it only
 * while the field is empty would stop after the first letter typed into the title. So the rule is:
 * follow while the field still holds exactly what was last proposed. An empty field at the start
 * counts as that, and anything a person types into it ends the following for good — including
 * clearing it, which is somebody saying "I will write this myself".
 *
 * It is deliberately not a `useFieldArray`-style feature of one screen: `slugFrom` is an annotation
 * of the schema, so the second field that needs it is a line rather than a copy of this.
 */
function useProposedSlugs<TValues extends Record<string, unknown>>(
  // The form of whatever entity is on screen: this reads two of its fields by name and writes one,
  // which is as much as an annotation can promise about a schema it has never seen.
  form: UseFormReturn<TValues, unknown, TValues>,
  fields: FieldNode[],
  defaultLocale: string | undefined,
): void {
  // Written into rather than replaced: neither of these may be a dependency of the effect below,
  // or deciding to stop following would immediately re-run the thing that was following.
  const proposed = useRef<Record<string, string>>({});
  const written = useRef<Set<string>>(new Set());
  const values = form.watch();

  useEffect(() => {
    for (const field of fields) {
      const source = field.meta.slugFrom;
      if (source === undefined || written.current.has(field.path)) {
        continue;
      }

      const current = values[field.path];
      const last = proposed.current[field.path] ?? '';

      // Somebody has written in it. `undefined` is a field the form has not registered yet, which
      // is not the same as an empty one and must not stop anything.
      if (current !== undefined && current !== '' && current !== last) {
        written.current.add(field.path);
        continue;
      }

      const next = slugify(readSource(values[source], defaultLocale));
      if (next !== last) {
        proposed.current[field.path] = next;
        form.setValue(field.path as never, next as never, { shouldDirty: true });
      }
    }
  }, [fields, values, defaultLocale, form]);
}

/**
 * The text a proposal reads. A translated source is read in the default language of the division,
 * which is the language the address is published in; the first language that has anything written
 * in it stands in while that one is still empty, so a title written in Italian first still proposes
 * something rather than nothing.
 */
function readSource(value: unknown, defaultLocale: string | undefined): string {
  if (typeof value === 'string') {
    return value;
  }

  if (typeof value !== 'object' || value === null) {
    return '';
  }

  const translations = value as Record<string, unknown>;
  const preferred = defaultLocale === undefined ? undefined : translations[defaultLocale];

  if (typeof preferred === 'string' && preferred.trim() !== '') {
    return preferred;
  }

  for (const written of Object.values(translations)) {
    if (typeof written === 'string' && written.trim() !== '') {
      return written;
    }
  }

  return '';
}

/**
 * `node.path` is where the field is in the schema, and therefore what its label is looked up by;
 * `name` is where its value is in the form, which for an entry of a repeatable list carries an
 * index. They are the same everywhere except inside a list, and keeping them apart is what stops
 * the second entry of a list from asking i18n for `aliases.1.name`.
 */
function Field({ node, name = node.path, env }: { node: FieldNode; name?: string; env: FormEnvironment }) {
  const { t, i18n } = useTranslation();
  const { register, control, formState } = useFormContext();
  const { locales, labels } = env;

  if (node.meta.hidden === true) {
    // Carried and submitted, never drawn: the row version is the reason this exists.
    return null;
  }

  // A field that holds other fields is named the same way any other is. The children of `seo` are
  // written flat in the language files — `"seo.title"` beside `"seo"` — rather than nested, so the
  // group keeps a name of its own: i18next resolves a dotted key either way, while a nested `seo`
  // would be an object where a word has to be. Measured rather than assumed, and it is the shape
  // the tests of this file have used since M0.
  const label = t(`${labels}.fields.${node.path}`);

  // The sentence under a field, drawn only when the language files carry one. It is a convention
  // rather than a schema flag on purpose: a hint is words, and words live in `locales/`.
  //
  // It exists because a label was doing two jobs. `grants.fields.expiresAt` used to read "Expires
  // (YYYY-MM-DD, empty for never)", and `DataList` builds a column header from the same key -- so
  // the grants table had a header five lines tall. One key, one job: the label names the field, the
  // hint explains it, and the header gets the short one for free.
  const hintKey = `${labels}.hints.${node.path}`;
  const hint = i18n.exists(hintKey) ? t(hintKey) : undefined;

  const error = errorAt(formState.errors, name);

  switch (node.kind) {
    case 'localized':
      return (
        <LocaleFields
          path={name}
          label={label}
          hint={hint}
          locales={locales}
          multiline={node.meta.multiline === true}
          error={error}
        />
      );

    case 'localizedObject':
      return <LocalizedObject node={node} name={name} env={env} label={label} hint={hint} error={error} />;

    case 'media': {
      if (env.mediaLibrary === undefined || env.division === undefined) {
        // The same discipline as an unknown type: the generator says what is missing instead of
        // drawing a field that cannot work. A media identifier is never typed by hand.
        throw new Error(
          `SchemaForm draws the media field at "${node.path}" only when it is given mediaLibrary ` +
            'and division. Hand it the library of the department the screen is about.',
        );
      }

      const library = env.mediaLibrary;
      const defaultLocale = env.division.defaultLocale;

      return (
        <Row id={name} label={label} hint={hint} error={error}>
          <Controller
            control={control}
            name={name}
            render={({ field }) => (
              <MediaPicker
                query={library}
                value={typeof field.value === 'number' ? field.value : null}
                // Undefined and not null when nothing is chosen: an optional field that is absent
                // is absent, and a null would be a value the contract does not have.
                onChange={(chosen) => field.onChange(chosen ?? undefined)}
                locale={i18n.language}
                defaultLocale={defaultLocale}
              />
            )}
          />
        </Row>
      );
    }

    case 'icon':
      return <IconChoice node={node} name={name} labels={labels} label={label} hint={hint} error={error} />;

    case 'instant':
      return (
        <Instant
          node={node}
          name={name}
          label={label}
          hint={hint}
          error={error}
          locale={i18n.language}
          timezone={env.division?.timezone}
        />
      );

    case 'text': {
      // Bound to a constant so the narrowing survives into the render callback below: `node` is a
      // parameter, and TypeScript will not carry a narrowing on one into a closure.
      const choices = node.choices;

      return (
        <Row id={name} label={label} hint={hint} error={error}>
          {choices !== null ? (
            <Controller
              control={control}
              name={name}
              render={({ field }) => (
                <Select
                  // The label of the row points at this id: see the note on the enum above.
                  id={name}
                  {...(typeof field.value === 'string' && field.value !== '' ? { value: field.value } : {})}
                  // Back to "nothing chosen", the same gesture an optional enum has: without it the
                  // first category somebody picks could never be taken off again.
                  onValueChange={(chosen) => field.onChange(chosen === NO_CHOICE ? '' : chosen)}
                  // The labels are the caller's, already in the language on screen. For a set only
                  // known at runtime — the permission catalogue, which depends on the modules
                  // installed — the label *is* the value, and that is what `choices` hands over.
                  items={[
                    ...(node.optional
                      ? [{ value: NO_CHOICE, label: t(`${labels}.options.${node.path}.none`) }]
                      : []),
                    ...choices,
                  ]}
                />
              )}
            />
          ) : node.meta.multiline === true ? (
            <Textarea id={name} rows={6} {...register(name)} />
          ) : (
            <Input id={name} {...register(name)} />
          )}
        </Row>
      );
    }

    case 'number':
      return (
        <Row id={name} label={label} hint={hint} error={error}>
          {node.choices === null ? (
            <Input id={name} type="number" {...register(name, { valueAsNumber: true })} />
          ) : (
            <Controller
              control={control}
              name={name}
              render={({ field }) => (
                <Select
                  {...(typeof field.value === 'number' ? { value: String(field.value) } : {})}
                  onValueChange={(chosen) => field.onChange(Number(chosen))}
                  items={node.choices!.map((choice) => ({
                    value: String(choice),
                    label: t(`${labels}.options.${node.path}.${choice}`),
                  }))}
                />
              )}
            />
          )}
        </Row>
      );

    case 'boolean':
      return (
        <div className="flex items-center gap-3">
          <Controller
            control={control}
            name={name}
            render={({ field }) => (
              <Switch
                id={name}
                checked={Boolean(field.value)}
                onCheckedChange={field.onChange}
                onBlur={field.onBlur}
              />
            )}
          />
          <Label htmlFor={name}>{label}</Label>
          <FieldError error={error} />
        </div>
      );

    case 'enum':
      return (
        <Row id={name} label={label} hint={hint} error={error}>
          <Controller
            control={control}
            name={name}
            render={({ field }) => (
              <Select
                // ⚠️ The label of a row points at this id, and without it the select has no
                // accessible name at all — `getByLabel` finds nothing, and neither does a screen
                // reader. Every generated select was missing it until G13; the public filters had
                // it right, which is where the shape was copied from.
                id={name}
                {...(typeof field.value === 'string' ? { value: field.value } : {})}
                // An optional enum needs a way back to "nothing chosen", and a select has no such
                // gesture: leaving it out would make the first choice permanent.
                onValueChange={(chosen) => field.onChange(chosen === NO_CHOICE ? undefined : chosen)}
                items={[
                  ...(node.optional
                    ? [{ value: NO_CHOICE, label: t(`${labels}.options.${node.path}.none`) }]
                    : []),
                  ...node.options.map((option) => ({
                    value: option,
                    label: t(`${labels}.options.${node.path}.${option}`),
                  })),
                ]}
              />
            )}
          />
        </Row>
      );

    case 'multi':
      return (
        <Row id={name} label={label} hint={hint} error={error}>
          <Controller
            control={control}
            name={name}
            render={({ field }) => {
              const chosen: string[] = Array.isArray(field.value) ? (field.value as string[]) : [];

              return (
                <div className="flex flex-wrap gap-x-6 gap-y-2" role="group" aria-label={label}>
                  {node.options.map((option) => (
                    <div key={option.value} className="flex items-center gap-2">
                      <Checkbox
                        id={`${name}.${option.value}`}
                        checked={chosen.includes(option.value)}
                        onCheckedChange={(ticked) =>
                          // Kept in the order the set declares rather than in the order they were
                          // ticked: what is stored is a set, and a diff of two arrays that hold
                          // the same values in a different order is a change nobody made.
                          field.onChange(
                            ticked === true
                              ? node.options
                                  .map((candidate) => candidate.value)
                                  .filter((value) => value === option.value || chosen.includes(value))
                              : chosen.filter((value) => value !== option.value),
                          )
                        }
                        onBlur={field.onBlur}
                      />
                      <Label htmlFor={`${name}.${option.value}`}>{option.label}</Label>
                    </div>
                  ))}
                </div>
              );
            }}
          />
        </Row>
      );

    case 'object':
      return (
        <fieldset className="border-border flex flex-col gap-4 rounded-md border p-4">
          <legend className="px-1">
            <H4>{label}</H4>
          </legend>
          {node.children.map((child) => (
            <Field
              key={child.path}
              node={child}
              name={`${name}${child.path.slice(node.path.length)}`}
              env={env}
            />
          ))}
        </fieldset>
      );

    case 'list':
      return <RepeatableList node={node} name={name} env={env} label={label} />;
  }
}

/** A list of objects: add, remove, reorder, and the same generator again for each entry. */
function RepeatableList({
  node,
  name,
  env,
  label,
}: {
  node: Extract<FieldNode, { kind: 'list' }>;
  name: string;
  env: FormEnvironment;
  label: string;
}) {
  const { t } = useTranslation();
  const { control } = useFormContext();
  const { fields, append, remove, move } = useFieldArray({ control, name });

  return (
    <fieldset className="border-border flex flex-col gap-4 rounded-md border p-4">
      <legend className="px-1">
        <H4>{label}</H4>
      </legend>

      {fields.map((entry, index) => (
        <div key={entry.id} className="border-border flex flex-col gap-4 rounded-md border p-3">
          {node.children.map((child) => (
            <Field
              key={child.path}
              node={child}
              name={`${name}.${index}${child.path.slice(node.path.length)}`}
              env={env}
            />
          ))}
          <div className="flex flex-wrap items-center gap-1">
            {/* Up and down rather than dragging, and they stay when dragging arrives in G11: this
                is the pair that works from a keyboard, and reordering is something an editor does
                far more often than adding. */}
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={index === 0}
              aria-label={t('form.moveUp')}
              onClick={() => move(index, index - 1)}
            >
              <ChevronUp aria-hidden className="size-4" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              disabled={index === fields.length - 1}
              aria-label={t('form.moveDown')}
              onClick={() => move(index, index + 1)}
            >
              <ChevronDown aria-hidden className="size-4" />
            </Button>
            <Button type="button" variant="ghost" size="sm" onClick={() => remove(index)}>
              <Trash2 aria-hidden className="mr-2 size-4" />
              {t('form.removeEntry')}
            </Button>
          </div>
        </div>
      ))}

      <div>
        {/* A new entry starts at what its own fields say they hold when empty, and never at `{}`:
            a translated field with no value is an input React cannot control, and the coordinator
            would be typing into a box that forgets what they wrote. */}
        <Button
          type="button"
          variant="secondary"
          size="sm"
          onClick={() => append(blankEntry(node.children, node.path, env.locales))}
        >
          <Plus aria-hidden className="mr-2 size-4" />
          {t('form.addEntry')}
        </Button>
      </div>
    </fieldset>
  );
}

/**
 * A translated object: the language tabs of any translated field, and inside each one the very same
 * generator drawing the same shape. `Seo` is the first of them — a title, a description and a
 * picture per language (design M1 section 9.2) — and the point is that a coordinator fills in
 * fields instead of writing the JSON the column used to hold.
 */
function LocalizedObject({
  node,
  name,
  env,
  label,
  hint,
  error,
}: {
  node: Extract<FieldNode, { kind: 'localizedObject' }>;
  name: string;
  env: FormEnvironment;
  label: string;
  hint: string | undefined;
  error: string | undefined;
}) {
  const { control } = useFormContext();
  const value = (useWatch({ control, name }) ?? {}) as Record<string, Record<string, unknown>>;

  // A language counts as written when anything in it is: it is the same question the badge on a
  // translated string asks, asked of an object.
  const isWritten = (locale: string) =>
    Object.values(value[locale] ?? {}).some(
      (entry) => typeof entry === 'number' || (typeof entry === 'string' && entry.trim() !== ''),
    );

  return (
    <LocaleTabs
      label={label}
      hint={hint}
      locales={env.locales}
      isWritten={isWritten}
      error={error}
      renderContent={(locale) => (
        <div className="flex flex-col gap-4">
          {node.children.map((child) => (
            <Field
              key={child.path}
              node={child}
              name={`${name}.${locale}${child.path.slice(node.path.length)}`}
              env={env}
            />
          ))}
        </div>
      )}
    />
  );
}

/**
 * An icon, out of the allow list. It is drawn as a grid of pictures rather than as a select, and
 * for a reason worth writing down: Atmosphere's `Select` takes a plain string per option, so a
 * select could only ever list the names — and a name without its picture is exactly the choice
 * nobody can make. The set stays closed either way, which is what the rule is about (design M1
 * section 1.5), and a radio group is what a keyboard already knows how to walk.
 */
function IconChoice({
  node,
  name,
  labels,
  label,
  hint,
  error,
}: {
  node: Extract<FieldNode, { kind: 'icon' }>;
  name: string;
  labels: string;
  label: string;
  hint: string | undefined;
  error: string | undefined;
}) {
  const { t } = useTranslation();
  const { control } = useFormContext();

  return (
    <fieldset className="flex flex-col gap-1">
      <Label asChild>
        <legend>{label}</legend>
      </Label>
      <FieldHint hint={hint} />

      <Controller
        control={control}
        name={name}
        render={({ field }) => (
          <div role="radiogroup" aria-label={label} className="flex flex-wrap gap-2">
            {node.optional ? (
              <IconOption
                chosen={typeof field.value !== 'string' || field.value === ''}
                label={t(`${labels}.options.${node.path}.none`)}
                onChoose={() => field.onChange(undefined)}
              />
            ) : null}

            {ICON_NAMES.map((iconName) => (
              <IconOption
                key={iconName}
                chosen={field.value === iconName}
                label={iconName}
                icon={iconName}
                onChoose={() => field.onChange(iconName)}
              />
            ))}
          </div>
        )}
      />

      <FieldError error={error} />
    </fieldset>
  );
}

/**
 * The picture of every icon of the allow list, drawn once when this module loads rather than looked
 * up while a form renders. The table is frozen and never changes, so there is nothing to recompute
 * — and a component read out of a map inside a render is one React has to treat as new every pass.
 */
const ICON_GLYPHS: Readonly<Record<string, ReactElement>> = Object.fromEntries(
  ICON_NAMES.map((iconName) => {
    const Icon = iconByName(iconName)!;
    return [iconName, <Icon aria-hidden className="size-5" />];
  }),
);

function IconOption({
  chosen,
  label,
  icon,
  onChoose,
}: {
  chosen: boolean;
  label: string;
  icon?: string;
  onChoose: () => void;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={chosen}
      aria-label={label}
      title={label}
      onClick={onChoose}
      className={`border-border bg-card flex size-10 items-center justify-center rounded-md border ${
        chosen ? 'ring-primary ring-2' : ''
      }`}
    >
      {icon === undefined ? (
        <span className="text-muted-foreground text-xs">&mdash;</span>
      ) : (
        (ICON_GLYPHS[icon] ?? null)
      )}
    </button>
  );
}

/**
 * A day, or an instant. What the form carries is **always ISO in UTC**, whatever the browser's own
 * time zone happens to be, because that is what the server stores and what a hub read from two
 * countries has to mean the same thing in both.
 *
 * The input is fed the UTC wall clock by slicing the string rather than by parsing it into a
 * `Date`: parsing drags the browser's zone into a value that has nothing to do with it, which is
 * the classic way a date moves by a day overnight.
 *
 * A **day** shows no second line. "The same day, elsewhere" is not a fact a date has, and echoing
 * one would only ever be a chance to read the wrong day. An **instant** does show the division's
 * local time under it, which is the rule `DateCell` follows in every list.
 */
function Instant({
  node,
  name,
  label,
  hint,
  error,
  locale,
  timezone,
}: {
  node: Extract<FieldNode, { kind: 'instant' }>;
  name: string;
  label: string;
  hint: string | undefined;
  error: string | undefined;
  locale: string;
  timezone: string | undefined;
}) {
  const { control } = useFormContext();

  if (node.withTime && timezone === undefined) {
    throw new Error(
      `SchemaForm draws the instant at "${node.path}" only when it is given division.timezone: ` +
        'a UTC time with nothing beside it is a time somebody has to convert in their head.',
    );
  }

  const width = node.withTime ? 16 : 10;

  return (
    <Row id={name} label={label} hint={hint} error={error}>
      <Controller
        control={control}
        name={name}
        render={({ field }) => {
          const iso = typeof field.value === 'string' ? field.value : '';

          return (
            <div className="flex flex-col gap-1">
              <Input
                id={name}
                type={node.withTime ? 'datetime-local' : 'date'}
                className="max-w-xs"
                value={iso.slice(0, width)}
                onBlur={field.onBlur}
                onChange={(event) => {
                  const written = event.target.value;
                  field.onChange(
                    written === '' ? undefined : node.withTime ? `${written}:00Z` : `${written}T00:00:00Z`,
                  );
                }}
              />
              {node.withTime && iso !== '' && timezone !== undefined ? (
                <Subtle className="tabular-nums">{localTime(iso, locale, timezone)}</Subtle>
              ) : null}
            </div>
          );
        }}
      />
    </Row>
  );
}

/** The same instant where the division lives, named, so nobody converts it in their head. */
function localTime(iso: string, locale: string, timezone: string): string {
  const instant = new Date(iso.endsWith('Z') ? iso : `${iso}Z`);
  if (Number.isNaN(instant.getTime())) {
    return '';
  }

  const shown = new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'short',
    timeZone: timezone,
  }).format(instant);

  return `${shown} ${timezone}`;
}

function Row({
  id,
  label,
  hint,
  error,
  children,
}: {
  id: string;
  label: string;
  hint: string | undefined;
  error: string | undefined;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1">
      <Label htmlFor={id}>{label}</Label>
      <FieldHint hint={hint} />
      {children}
      <FieldError error={error} />
    </div>
  );
}

export function FieldHint({ hint }: { hint: string | undefined }) {
  if (hint === undefined) {
    return null;
  }

  return <p className="text-muted-foreground text-sm">{hint}</p>;
}

function FieldError({ error }: { error: string | undefined }) {
  if (error === undefined) {
    return null;
  }

  return (
    <p role="alert" className="text-destructive text-sm">
      {error}
    </p>
  );
}

/** The message react-hook-form put at that path, dotted and indexed paths included. */
function errorAt(errors: FieldErrors, path: string): string | undefined {
  let current: unknown = errors;

  for (const segment of path.split('.')) {
    if (current === null || typeof current !== 'object') {
      return undefined;
    }
    current = (current as Record<string, unknown>)[segment];
  }

  const message = (current as { message?: unknown } | undefined)?.message;
  return typeof message === 'string' ? message : undefined;
}
