import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';

import { SchemaForm } from './SchemaForm';

/**
 * A form that applies as it is written (`onChange`, the seventh extension, G15). Three things make
 * it usable rather than merely reactive: nothing is applied for merely being drawn, a pause applies
 * once and not once per letter, and a value the schema refuses applies nothing at all.
 */

const schema = z.object({
  title: z.string().min(1),
  note: z.string(),
});

const labels = { test: { fields: { title: 'Title', note: 'Note' } } };

function renderLive(onChange: (values: { title: string; note: string }) => void) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={{ title: 'Written', note: '' }}
      locales={['en']}
      labels="test"
      onChange={onChange}
    />,
    { i18n: createTestI18n(labels) },
  );
}

test('a live form applies after a pause, once, and only what the schema accepts', async () => {
  const onChange = vi.fn();
  const user = userEvent.setup();
  renderLive(onChange);

  // Drawing a form is not writing in it: selecting a block must not mark the page as changed.
  await new Promise((resolve) => setTimeout(resolve, 250));
  expect(onChange).not.toHaveBeenCalled();

  // And it has no button: there is nothing to send.
  expect(screen.queryByRole('button')).toBeNull();

  await user.type(screen.getByLabelText('Note'), 'three');

  await waitFor(() => expect(onChange).toHaveBeenCalledTimes(1));
  expect(onChange).toHaveBeenLastCalledWith({ title: 'Written', note: 'three' });

  // A required field emptied halfway through is not a value: the thing being edited keeps the last
  // one that was, and the field says what is wrong.
  await user.clear(screen.getByLabelText('Title'));
  await new Promise((resolve) => setTimeout(resolve, 250));
  expect(onChange).toHaveBeenCalledTimes(1);

  await user.type(screen.getByLabelText('Title'), 'Back');
  await waitFor(() => expect(onChange).toHaveBeenCalledTimes(2));
  expect(onChange).toHaveBeenLastCalledWith({ title: 'Back', note: 'three' });
});

test('a click inside the pause applies at once, and the mouse going down does not', async () => {
  // A button outside the form, as the editor's toolbar is (decisions/2026-11-08-l-ultima-battuta.md).
  const onChange = vi.fn();
  const pressed = vi.fn();
  const user = userEvent.setup();
  renderWithProviders(
    <>
      <SchemaForm
        schema={schema}
        defaults={{ title: 'Written', note: '' }}
        locales={['en']}
        labels="test"
        onChange={onChange}
      />
      <button
        type="button"
        onClick={() => {
          pressed(onChange.mock.calls.length);
        }}
      >
        Elsewhere
      </button>
    </>,
    { i18n: createTestI18n(labels) },
  );

  await user.type(screen.getByLabelText('Note'), 'x');
  const button = screen.getByRole('button', { name: 'Elsewhere' });

  // The focus leaves the field with the mouse on its way down: a screen that redrew now would move the button from
  // under it, and the click would be lost.
  await user.pointer({ keys: '[MouseLeft>]', target: button });
  expect(screen.getByLabelText('Note')).not.toHaveFocus();
  expect(onChange).not.toHaveBeenCalled();

  // The click: applied before its own handler runs.
  await user.pointer({ keys: '[/MouseLeft]', target: button });
  expect(onChange).toHaveBeenCalledTimes(1);
  expect(onChange).toHaveBeenLastCalledWith({ title: 'Written', note: 'x' });
  expect(pressed).toHaveBeenCalledWith(1);

  // And once: the pause that was running applies nothing more.
  await new Promise((resolve) => setTimeout(resolve, 250));
  expect(onChange).toHaveBeenCalledTimes(1);
});
