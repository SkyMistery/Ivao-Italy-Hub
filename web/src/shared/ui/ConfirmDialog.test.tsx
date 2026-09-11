import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

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
