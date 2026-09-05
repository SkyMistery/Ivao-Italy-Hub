import { zodResolver } from '@hookform/resolvers/zod';
import { Button, H4, Input, Label, Select, Switch, Textarea } from '@ivao/atmosphere-react';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  Controller,
  FormProvider,
  useFieldArray,
  useForm,
  useFormContext,
  type FieldErrors,
} from 'react-hook-form';
import type { z } from 'zod';

import { LocaleFields } from './LocaleFields';
import { ProblemAlert } from './ProblemAlert';
import { NO_CHOICE, readFields, type FieldNode } from './schema';
import { useProblemDetails } from './useProblemDetails';

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
}) {
  const { t } = useTranslation();
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: defaults as never,
  });
  const problem = useProblemDetails(form);
  const fields = readFields(schema);

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
            <Field key={field.path} node={field} locales={locales} labels={labels} />
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
 * `node.path` is where the field is in the schema, and therefore what its label is looked up by;
 * `name` is where its value is in the form, which for an entry of a repeatable list carries an
 * index. They are the same everywhere except inside a list, and keeping them apart is what stops
 * the second entry of a list from asking i18n for `aliases.1.name`.
 */
function Field({
  node,
  name = node.path,
  locales,
  labels,
}: {
  node: FieldNode;
  name?: string;
  locales: readonly string[];
  labels: string;
}) {
  const { t, i18n } = useTranslation();
  const { register, control, formState } = useFormContext();

  if (node.meta.hidden === true) {
    // Carried and submitted, never drawn: the row version is the reason this exists.
    return null;
  }

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
                  {...(typeof field.value === 'string' && field.value !== '' ? { value: field.value } : {})}
                  onValueChange={(chosen) => field.onChange(chosen)}
                  // The values are the labels, and deliberately: a set only known at runtime — the
                  // permission catalogue, which depends on the modules installed — cannot have an
                  // i18n key per member, and its members are identifiers rather than prose.
                  items={choices.map((choice) => ({ value: choice, label: choice }))}
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
              locales={locales}
              labels={labels}
            />
          ))}
        </fieldset>
      );

    case 'list':
      return <RepeatableList node={node} name={name} locales={locales} labels={labels} label={label} />;
  }
}

/** A list of objects: add, remove, and the same generator again for each entry. */
function RepeatableList({
  node,
  name,
  locales,
  labels,
  label,
}: {
  node: Extract<FieldNode, { kind: 'list' }>;
  name: string;
  locales: readonly string[];
  labels: string;
  label: string;
}) {
  const { t } = useTranslation();
  const { control } = useFormContext();
  const { fields, append, remove } = useFieldArray({ control, name });

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
              locales={locales}
              labels={labels}
            />
          ))}
          <div>
            <Button type="button" variant="ghost" size="sm" onClick={() => remove(index)}>
              <Trash2 aria-hidden className="mr-2 size-4" />
              {t('form.removeEntry')}
            </Button>
          </div>
        </div>
      ))}

      <div>
        <Button type="button" variant="secondary" size="sm" onClick={() => append({})}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('form.addEntry')}
        </Button>
      </div>
    </fieldset>
  );
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
