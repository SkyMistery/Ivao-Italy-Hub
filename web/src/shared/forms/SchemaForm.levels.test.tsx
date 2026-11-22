import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';
import { choiceItem, type ChoiceOption } from './schema';

/**
 * A choice drawn to the right of the one before it (`ChoiceOption.level`): a page under a page, in the choice of where a
 * page sits (design 13 §3.2). Drawn as padding, and never as spaces in its label: those were read aloud, stood in front of
 * what the select's typeahead matches, and showed in the closed select (the review of #550, item 6).
 */

const pages: ChoiceOption[] = [
  { value: '1', label: '/about - About' },
  { value: '2', label: '/about/team - Team', level: 1 },
];

const labels = { test: { fields: { parent: 'Parent' } } };

test('a choice a level down is pushed right by its padding, and says only its label', async () => {
  const user = userEvent.setup();
  renderWithProviders(
    <SchemaForm
      schema={z.object({ parent: z.string().meta({ choices: pages }) })}
      defaults={{}}
      locales={['en']}
      labels="test"
      submitLabel="Save"
      onSubmit={vi.fn()}
    />,
    { i18n: createTestI18n(labels) },
  );

  const box = screen.getByRole('combobox', { name: 'Parent' });
  await user.click(box);

  // Its name is its label and nothing in front of it.
  const nested = screen.getByRole('option', { name: '/about/team - Team' });
  expect(nested.textContent).toBe('/about/team - Team');

  // Pushed right in place of the item's own padding, not beside it; the level is not an attribute of the page.
  expect(nested).toHaveClass('pl-12');
  expect(nested).not.toHaveClass('pl-8');
  expect(nested).not.toHaveAttribute('level');

  // A choice at the top keeps the padding it always had.
  expect(screen.getByRole('option', { name: '/about - About' })).toHaveClass('pl-8');

  // The closed select shows what was chosen as it is written.
  await user.click(nested);
  expect(box.textContent).toBe('/about/team - Team');
});

test('a level past the last is drawn at the last, and no level is the choice as it was', () => {
  expect(choiceItem({ value: 'a', label: 'A' })).toEqual({ value: 'a', label: 'A' });
  expect(choiceItem({ value: 'a', label: 'A', level: 0 })).toEqual({ value: 'a', label: 'A' });
  expect(choiceItem({ value: 'a', label: 'A', level: 2, disabled: true })).toEqual({
    value: 'a',
    label: 'A',
    disabled: true,
    className: 'pl-16',
  });
  expect(choiceItem({ value: 'a', label: 'A', level: 9 })).toEqual({
    value: 'a',
    label: 'A',
    className: 'pl-16',
  });
});
