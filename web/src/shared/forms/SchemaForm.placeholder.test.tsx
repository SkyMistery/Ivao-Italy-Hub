import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';

/**
 * What a closed select says while nothing is chosen, in the language on screen (note
 * 2026-11-06-il-segnaposto-delle-select). Left to itself, Atmosphere says "Select an option" in
 * English: the form of a grant said it under an Italian interface, three times.
 */

const LOCALES = ['en', 'it'] as const;
const ATMOSPHERE_DEFAULT = 'Select an option';

const sizes = [
  { value: 's', label: 'Small' },
  { value: 'l', label: 'Large' },
];

const labels = {
  test: {
    fields: { kind: 'Kind', size: 'Size', seats: 'Seats', category: 'Category', parent: 'Parent' },
    options: {
      kind: { A: 'First', B: 'Second' },
      seats: { 1: 'One', 2: 'Two' },
      category: { A: 'First', B: 'Second', none: 'Every category' },
    },
  },
};

function draw(schema: z.ZodType<Record<string, unknown>, Record<string, unknown>>, language: 'en' | 'it') {
  const i18n = createTestI18n(labels);
  void i18n.changeLanguage(language);

  renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{}}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={vi.fn()}
    />,
    { i18n },
  );
}

/** The three kinds of field a select is drawn for, all required. */
const required = z.object({
  kind: z.enum(['A', 'B']),
  size: z.string().meta({ choices: sizes }),
  seats: z.number().meta({ choices: [1, 2] }),
});

describe('a required select with nothing chosen', () => {
  test.each([
    ['en', 'Choose an option'],
    ['it', 'Scegli un’opzione'],
  ] as const)('asks for a choice in %s, on every kind of field', (language, word) => {
    draw(required, language);

    const boxes = screen.getAllByRole('combobox');
    expect(boxes).toHaveLength(3);

    for (const box of boxes) {
      expect(box).toHaveTextContent(word);
      expect(box).not.toHaveTextContent(ATMOSPHERE_DEFAULT);
    }
  });
});

describe('an optional select with nothing chosen', () => {
  test('says the word of its own "none", the one the list offers', async () => {
    const user = userEvent.setup();
    draw(z.object({ category: z.enum(['A', 'B']).optional() }), 'en');

    const box = screen.getByRole('combobox', { name: 'Category' });
    expect(box).toHaveTextContent('Every category');

    await user.click(box);
    expect(screen.getByRole('option', { name: 'Every category' })).toBeInTheDocument();
  });

  test.each([
    ['en', 'No choice'],
    ['it', 'Nessuna scelta'],
  ] as const)(
    'says the core word in %s when the language files hold none of its own, and never prints a key',
    async (language, word) => {
      const user = userEvent.setup();
      draw(z.object({ parent: z.string().optional().meta({ choices: sizes }) }), language);

      const box = screen.getByRole('combobox');
      expect(box).toHaveTextContent(word);

      await user.click(box);
      expect(screen.getByRole('option', { name: word })).toBeInTheDocument();
      expect(screen.queryByText(/test\.options/)).not.toBeInTheDocument();
    },
  );

  test('says the core word when the field carries its own name', () => {
    draw(z.object({ size: z.string().meta({ choices: sizes, label: 'Taglia' }).optional() }), 'it');

    expect(screen.getByRole('combobox', { name: 'Taglia' })).toHaveTextContent('Nessuna scelta');
  });
});
