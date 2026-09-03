import { Alert } from '@ivao/atmosphere-react';
import { TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';

/**
 * What went wrong when no field can say it: the row was changed by somebody else, or it belongs to
 * a department this member may not write. `useProblemDetails` decides the sentence; this only
 * draws it.
 */
export function ProblemAlert({ summary }: { summary: string | null }) {
  const { t } = useTranslation();

  if (summary === null) {
    return null;
  }

  return <Alert variant="destructive" Icon={TriangleAlert} title={t('errors.title')} description={summary} />;
}
