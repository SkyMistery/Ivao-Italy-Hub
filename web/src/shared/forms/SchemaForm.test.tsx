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
