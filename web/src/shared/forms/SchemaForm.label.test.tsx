import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { readFields } from './schema';

/**
 * A field that says its own name (E10o, note 2026-10-12-un-campo-dice-il-suo-nome): a form whose
 * schema is built from rows — the questions of an event in person, written by its staff in every
 * language of the division — names each field with `.meta({ label })`, already in the language on
 * screen, because no language file holds the text of a row. Every other field is named by its i18n
 * key, as it always was.
 */

const LOCALES = ['en', 'it'] as const;

const sizes = [
  { value: 's', label: 'Small' },
  { value: 'l', label: 'Large' },
];

/** The five kinds a form built from rows draws, under an object that keeps its i18n name. */
const questionsSchema = z.object({
  answers: z.object({
    q1: z.boolean().meta({ label: 'Will you have dinner with us?' }),
    q2: z.string().meta({ choices: sizes, label: 'T-shirt size' }).optional(),
    q3: z.array(z.string()).meta({ multi: true, choices: sizes, label: 'Sizes for the family' }),
    q4: z.number().int().optional().meta({ label: 'How many guests?' }),
    q5: z.string().meta({ multiline: true, label: 'Anything we should know?' }),
  }),
});

const questionsLabels = { test: { fields: { answers: 'Your answers' } } };

function renderQuestions(
  onSubmit = vi.fn((values: { answers: Record<string, unknown> }) => Promise.resolve(values)),
) {
  renderWithProviders(
    <SchemaForm
      schema={questionsSchema}
      defaults={{ answers: { q1: false, q2: '', q3: [], q5: '' } }}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={onSubmit}
    />,
    { i18n: createTestI18n(questionsLabels) },
  );

  return onSubmit;
}

describe('a field with a name of its own', () => {
  test('carries the name through the walk, wherever the annotation sits', () => {
    const [answers] = readFields(questionsSchema);
    const children = answers?.kind === 'object' ? answers.children : [];

    expect(children.map((child) => child.meta.label)).toEqual([
      'Will you have dinner with us?',
      'T-shirt size',
      'Sizes for the family',
      'How many guests?',
      'Anything we should know?',
    ]);
  });

  test('a yes or no is a switch named by the question', () => {
    renderQuestions();

    expect(screen.getByRole('switch', { name: 'Will you have dinner with us?' })).not.toBeChecked();
  });

  test('one choice is a select named by the question', () => {
    renderQuestions();

    expect(screen.getByRole('combobox', { name: 'T-shirt size' })).toBeInTheDocument();
  });

  test('several choices are a group named by the question', () => {
    renderQuestions();

    expect(screen.getByRole('group', { name: 'Sizes for the family' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Large' })).toBeInTheDocument();
  });

  test('a number and a text are named by the question', () => {
    renderQuestions();

    expect(screen.getByLabelText('How many guests?')).toHaveAttribute('type', 'number');
    expect(screen.getByLabelText('Anything we should know?').tagName).toBe('TEXTAREA');
  });

  test('the object around them keeps its i18n name, and no key is printed', () => {
    renderQuestions();

    expect(screen.getByRole('group', { name: 'Your answers' })).toBeInTheDocument();
    // The failure this guards against is the i18next default: a missing key comes back as the key.
    expect(screen.queryByText(/test\.(fields|options)\./)).not.toBeInTheDocument();
  });

  test('an optional select says "none" with the core word, and choosing it empties the field', async () => {
    const user = userEvent.setup();
    const onSubmit = renderQuestions();

    await user.click(screen.getByRole('combobox', { name: 'T-shirt size' }));
    await user.click(screen.getByRole('option', { name: 'Large' }));
    await user.click(screen.getByRole('combobox', { name: 'T-shirt size' }));

    // The core's word, in the language on screen: no key beside the field could hold it.
    expect(screen.queryByRole('option', { name: /test\.options/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'No choice' }));
    await user.click(screen.getByRole('button', { name: 'Save' }));

    // Back to "nothing chosen", the same empty value a select of `choices` has always given back.
    expect(onSubmit.mock.lastCall?.[0].answers.q2).toBe('');
  });

  test('the core word reads in the language on screen', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <SchemaForm
        schema={z.object({ size: z.string().meta({ choices: sizes, label: 'Taglia' }).optional() })}
        defaults={{ size: '' }}
        locales={LOCALES}
        labels="test"
        submitLabel="Salva"
        onSubmit={vi.fn()}
      />,
      { i18n: italian() },
    );

    await user.click(screen.getByRole('combobox', { name: 'Taglia' }));
    expect(screen.getByRole('option', { name: 'Nessuna scelta' })).toBeInTheDocument();
  });
});

describe('a field without a name of its own', () => {
  test('is named by its i18n key, as it always was', () => {
    renderWithProviders(
      <SchemaForm
        schema={z.object({ reference: z.string(), published: z.boolean() })}
        defaults={{ reference: '', published: false }}
        locales={LOCALES}
        labels="test"
        submitLabel="Save"
        onSubmit={vi.fn()}
      />,
      { i18n: createTestI18n({ test: { fields: { reference: 'Reference', published: 'Published' } } }) },
    );

    expect(screen.getByLabelText('Reference').tagName).toBe('INPUT');
    expect(screen.getByRole('switch', { name: 'Published' })).toBeInTheDocument();
  });

  test('an optional select still says "none" with its own key, not the core word', async () => {
    const user = userEvent.setup();
    renderWithProviders(
      <SchemaForm
        schema={z.object({ category: z.string().meta({ choices: sizes }).optional() })}
        defaults={{ category: '' }}
        locales={LOCALES}
        labels="test"
        submitLabel="Save"
        onSubmit={vi.fn()}
      />,
      {
        i18n: createTestI18n({
          test: { fields: { category: 'Category' }, options: { category: { none: 'No category' } } },
        }),
      },
    );

    await user.click(screen.getByRole('combobox', { name: 'Category' }));
    expect(screen.getByRole('option', { name: 'No category' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'No choice' })).not.toBeInTheDocument();
  });
});

/** The test harness in Italian: the core's own Italian words, read from `locales/it`. */
function italian() {
  const i18n = createTestI18n();
  void i18n.changeLanguage('it');
  return i18n;
}
