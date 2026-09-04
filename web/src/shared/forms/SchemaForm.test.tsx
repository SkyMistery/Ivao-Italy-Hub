import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { localized, readFields } from './schema';

/**
 * The generator has to draw every kind of field the design lists (§7.5), because the rule of the
 * project is that a screen extends it rather than falling back to a hand written form. One test per
 * kind is what makes that rule enforceable.
 */

const LOCALES = ['en', 'it'] as const;

const everySchema = z.object({
  title: localized(),
  body: localized().meta({ localized: true, multiline: true }),
  reference: z.string(),
  note: z.string().meta({ multiline: true }),
  weight: z.number().int(),
  published: z.boolean(),
  visibility: z.enum(['Public', 'Members']),
  contact: z.object({ email: z.string() }),
  aliases: z.array(z.object({ name: z.string() })),
  rowVersion: z.string().meta({ hidden: true }),
});

const labels = {
  test: {
    fields: {
      title: 'Title',
      body: 'Body',
      reference: 'Reference',
      note: 'Note',
      weight: 'Weight',
      published: 'Published',
      visibility: 'Visible to',
      contact: 'Contact',
      'contact.email': 'Email',
      aliases: 'Aliases',
      'aliases.name': 'Name',
      rowVersion: 'Version',
    },
    options: { visibility: { Public: 'Everybody', Members: 'Members' } },
  },
};

const defaults = {
  title: { en: '', it: '' },
  body: { en: '', it: '' },
  reference: '',
  note: '',
  weight: 0,
  published: true,
  visibility: 'Public' as const,
  contact: { email: '' },
  aliases: [],
  rowVersion: 'v1',
};

function renderForm(onSubmit = () => Promise.resolve()) {
  return renderWithProviders(
    <SchemaForm
      schema={everySchema}
      defaults={defaults}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(labels) },
  );
}

describe('the walk over the schema', () => {
  test('names every field, with the kind the design asks for', () => {
    expect(readFields(everySchema).map((field) => [field.path, field.kind])).toEqual([
      ['title', 'localized'],
      ['body', 'localized'],
      ['reference', 'text'],
      ['note', 'text'],
      ['weight', 'number'],
      ['published', 'boolean'],
      ['visibility', 'enum'],
      ['contact', 'object'],
      ['aliases', 'list'],
      ['rowVersion', 'text'],
    ]);
  });

  test('keeps an annotation that sits outside optional', () => {
    const schema = z.object({ note: z.string().meta({ multiline: true }).optional() });
    const [field] = readFields(schema);

    expect(field?.meta.multiline).toBe(true);
    expect(field?.optional).toBe(true);
  });

  test('refuses a kind it cannot draw rather than dropping the field', () => {
    const schema = z.object({ when: z.date() });

    expect(() => readFields(schema)).toThrow(/does not draw a "date" at "when"/);
  });

  test('reads the default a field declares, so nobody else has to know it', () => {
    const schema = z.object({ limit: z.number().int().default(10), free: z.number().int() });
    const [limit, free] = readFields(schema);

    expect(limit?.defaultValue).toBe(10);
    expect(free?.defaultValue).toBeUndefined();
  });

  test('carries the closed set of a number that has one', () => {
    const schema = z.object({
      level: z
        .number()
        .int()
        .meta({ choices: [1, 2, 3] }),
    });
    const [field] = readFields(schema);

    expect(field?.kind).toBe('number');
    expect(field?.kind === 'number' && field.choices).toEqual([1, 2, 3]);
  });
});

describe('the form it draws', () => {
  test('a translated field becomes one tab per language of the division', () => {
    renderForm();

    const tabs = screen.getAllByRole('tab', { name: /English/ });
    expect(tabs).toHaveLength(2); // one for `title`, one for `body`
    expect(screen.getAllByRole('tab', { name: /Italian/ })).toHaveLength(2);
  });

  test('a plain string is an input and a multiline one is a textarea', () => {
    renderForm();

    expect(screen.getByLabelText('Reference').tagName).toBe('INPUT');
    expect(screen.getByLabelText('Note').tagName).toBe('TEXTAREA');
  });

  test('a number is a number input', () => {
    renderForm();

    expect(screen.getByLabelText('Weight')).toHaveAttribute('type', 'number');
  });

  test('a boolean is a switch, already reflecting the default', () => {
    renderForm();

    expect(screen.getByRole('switch', { name: 'Published' })).toBeChecked();
  });

  test('an enum is a select whose choices are translated', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.click(screen.getByRole('combobox'));
    expect(screen.getByRole('option', { name: 'Everybody' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Members' })).toBeInTheDocument();
  });

  test('a nested object is a fieldset with its own fields', () => {
    renderForm();

    const fieldset = screen.getByRole('group', { name: 'Contact' });
    expect(within(fieldset).getByLabelText('Email')).toBeInTheDocument();
  });

  test('an array of objects can be added to and removed from', async () => {
    const user = userEvent.setup();
    renderForm();

    expect(screen.queryByLabelText('Name')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Add/ }));
    expect(screen.getByLabelText('Name')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Remove/ }));
    expect(screen.queryByLabelText('Name')).not.toBeInTheDocument();
  });

  test('a number with a closed set of values is a select, not a free input', async () => {
    // A number and not a `z.enum` on purpose: a string inside a block's properties is extracted as
    // the text of the page for the search index, and a heading level is not text.
    const user = userEvent.setup();
    const onSubmit = vi.fn(() => Promise.resolve());

    renderWithProviders(
      <SchemaForm
        schema={z.object({
          level: z
            .number()
            .int()
            .meta({ choices: [1, 2, 3] }),
        })}
        defaults={{ level: 1 }}
        locales={LOCALES}
        labels="test"
        onSubmit={onSubmit}
        submitLabel="Save"
      />,
      {
        i18n: createTestI18n({
          test: { fields: { level: 'Level' }, options: { level: { 1: 'One', 2: 'Two', 3: 'Three' } } },
        }),
      },
    );

    await user.click(screen.getByRole('combobox'));
    await user.click(screen.getByRole('option', { name: 'Two' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    // A number goes back as a number: a select hands out strings, and the payload must not.
    expect(onSubmit).toHaveBeenCalledWith({ level: 2 });
  });

  test('an optional choice offers a way back to nothing chosen', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn(() => Promise.resolve());

    renderWithProviders(
      <SchemaForm
        schema={z.object({ department: z.enum(['ED', 'FOD']).optional() })}
        defaults={{ department: 'ED' as const }}
        locales={LOCALES}
        labels="test"
        onSubmit={onSubmit}
        submitLabel="Save"
      />,
      {
        i18n: createTestI18n({
          test: {
            fields: { department: 'Department' },
            options: { department: { none: 'Every department', ED: 'ED', FOD: 'FOD' } },
          },
        }),
      },
    );

    await user.click(screen.getByRole('combobox'));
    await user.click(screen.getByRole('option', { name: 'Every department' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    // Not the sentinel the select needed in the DOM: the payload says the field is absent.
    expect(onSubmit).toHaveBeenCalledWith({ department: undefined });
  });

  test('a hidden field is never drawn but is still submitted', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn(() => Promise.resolve());
    renderForm(onSubmit);

    expect(screen.queryByLabelText('Version')).not.toBeInTheDocument();

    await user.type(screen.getAllByRole('textbox')[0] as HTMLElement, 'Something');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ rowVersion: 'v1' }));
  });
});
