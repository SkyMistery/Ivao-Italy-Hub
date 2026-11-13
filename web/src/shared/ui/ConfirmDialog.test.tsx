import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { SchemaForm } from '../forms/SchemaForm';
import { createTestI18n, renderWithProviders } from '../../test/harness';

import { ConfirmDialog } from './ConfirmDialog';

/**
 * The question before an action, and — since G14 — the fields an answer is given with.
 */
test('the dialog opens on its trigger, carries fields, and confirms with its own button', async () => {
  const user = userEvent.setup();
  const onConfirm = vi.fn();

  renderWithProviders(
    <ConfirmDialog
      triggerText="Publish"
      title="Publish this version?"
      confirmText="Publish now"
      confirmVariant="primary"
      onConfirm={onConfirm}
    >
      <label>
        What changed
        <input />
      </label>
    </ConfirmDialog>,
  );

  expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  await user.click(screen.getByRole('button', { name: 'Publish' }));

  const dialog = screen.getByRole('alertdialog');
  await user.type(screen.getByLabelText('What changed'), 'First edition');
  await user.click(screen.getByRole('button', { name: 'Publish now' }));

  expect(onConfirm).toHaveBeenCalledTimes(1);
  expect(dialog).not.toBeInTheDocument();
});

/**
 * A reason written in a live form inside the dialog (#428), the way a screen asks it: the screen keeps what the form
 * applied, turns the confirmation off while it is empty, and confirms with it. The form applies 150 ms after the last
 * keystroke; a press inside that pause must still confirm what the box shows.
 */
const reasonSchema = z.object({ reason: z.string().meta({ multiline: true }) });
const reasonLabels = { test: { fields: { reason: 'Reason' } } };

function Removal({ onConfirm }: { onConfirm: (reason: string) => void }) {
  const [reason, setReason] = useState('');

  return (
    <ConfirmDialog
      triggerText="Take away"
      title="Take it away?"
      confirmText="Take away now"
      confirmDisabled={reason.trim() === ''}
      onOpenChange={(open) => {
        if (open) {
          setReason('');
        }
      }}
      onConfirm={() => onConfirm(reason.trim())}
    >
      <SchemaForm
        schema={reasonSchema}
        defaults={{ reason: '' }}
        locales={[]}
        labels="test"
        onChange={(values) => setReason(values.reason)}
      />
    </ConfirmDialog>
  );
}

const pause = () => new Promise((resolve) => setTimeout(resolve, 250));

test('a confirmation pressed right after the last keystrokes confirms what the box shows', async () => {
  const user = userEvent.setup();
  const onConfirm = vi.fn();
  renderWithProviders(<Removal onConfirm={onConfirm} />, { i18n: createTestI18n(reasonLabels) });

  await user.click(screen.getByRole('button', { name: 'Take away' }));
  const dialog = screen.getByRole('alertdialog');
  await user.type(screen.getByLabelText('Reason'), 'no show');
  await pause();
  await user.type(screen.getByLabelText('Reason'), ' twice');
  await user.click(screen.getByRole('button', { name: 'Take away now' }));

  expect(onConfirm).toHaveBeenCalledTimes(1);
  expect(onConfirm).toHaveBeenCalledWith('no show twice');
  expect(dialog).not.toBeInTheDocument();
});

test('a confirmation pressed from the keyboard right after the last keystrokes confirms what the box shows', async () => {
  const user = userEvent.setup();
  const onConfirm = vi.fn();
  renderWithProviders(<Removal onConfirm={onConfirm} />, { i18n: createTestI18n(reasonLabels) });

  await user.click(screen.getByRole('button', { name: 'Take away' }));
  await user.type(screen.getByLabelText('Reason'), 'no show');
  await pause();
  await user.type(screen.getByLabelText('Reason'), ' twice');
  screen.getByRole('button', { name: 'Take away now' }).focus();
  await user.keyboard('{Enter}');

  expect(onConfirm).toHaveBeenCalledTimes(1);
  expect(onConfirm).toHaveBeenCalledWith('no show twice');
});

test('a box emptied right before the confirmation confirms nothing, and the dialog stays', async () => {
  const user = userEvent.setup();
  const onConfirm = vi.fn();
  renderWithProviders(<Removal onConfirm={onConfirm} />, { i18n: createTestI18n(reasonLabels) });

  await user.click(screen.getByRole('button', { name: 'Take away' }));
  await user.type(screen.getByLabelText('Reason'), 'no show');
  await pause();
  await user.clear(screen.getByLabelText('Reason'));
  await user.click(screen.getByRole('button', { name: 'Take away now' }));

  expect(onConfirm).not.toHaveBeenCalled();
  expect(screen.getByRole('alertdialog')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Take away now' })).toBeDisabled();
});
