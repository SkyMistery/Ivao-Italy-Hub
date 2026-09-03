import { AlertDialog } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

/**
 * "Are you sure?" for the one kind of action that deserves the question: the one that cannot be
 * undone. Deleting a link is the first, deleting a grant is the next.
 *
 * It is a component and not a habit so that the wording, the order of the buttons and the colour
 * of the confirmation are the same everywhere — a dialog whose destructive button moves around is
 * a dialog people learn to dismiss without reading.
 */
export function ConfirmDialog({
  triggerText,
  title,
  description,
  confirmText,
  onConfirm,
  disabled = false,
}: {
  triggerText: string;
  title: string;
  description?: string;
  confirmText: string;
  onConfirm: () => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation();

  return (
    <AlertDialog
      title={title}
      {...(description === undefined ? {} : { description })}
      triggerText={triggerText}
      triggerButtonProps={{ variant: 'ghost', disabled }}
      confirmText={confirmText}
      confirmButtonProps={{ variant: 'destructive' }}
      cancelText={t('common.cancel')}
      onConfirm={() => onConfirm()}
    />
  );
}
