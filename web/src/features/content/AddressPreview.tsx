import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import type { Department } from '../../shared/api/bootstrap';

import { contentAddressQuery, type ContentKind } from './queries';

/** Where the kinds that are not pages live: under a prefix of their own, by their slug. */
const PREFIX: Partial<Record<ContentKind, string>> = { News: '/news', Document: '/documents' };

/**
 * The address a row is about to have, said under the fields that compose it, and whether it may be
 * had (note 2026-09-13-contenuti-centralizzati, 3.7): free, taken — with the first free one —,
 * reserved by the application, too deep, or at the top of the site for somebody who may not put a
 * page there. It asks the check the save runs, so what it says is what the save will say.
 *
 * Not a field: the address is what the page and the slug *make*, and a field would be a second way
 * of writing it.
 */
export function AddressPreview({
  kind,
  department,
  slug,
  parentId,
  id,
}: {
  kind: ContentKind;
  department: Department;
  slug: string;
  parentId: number | null;
  id: number | null;
}) {
  const { t } = useTranslation();

  const trimmed = slug.trim();
  const address = useQuery({
    ...contentAddressQuery({ kind, department, slug: trimmed, parentId, id }),
    enabled: trimmed !== '' && kind !== 'Dashboard',
    // Not blank between one keystroke and the answer: "nothing" is the one thing it must not say.
    placeholderData: keepPreviousData,
  });

  if (trimmed === '' || kind === 'Dashboard' || address.data === undefined) {
    return null;
  }

  const { path, state, suggestion } = address.data;
  const shown = `${PREFIX[kind] ?? ''}${path}`;

  return (
    <p role="status" aria-label={t('content.address.label')} className="flex flex-col gap-0.5 text-sm">
      <span className="text-foreground font-mono break-all">{shown}</span>
      <span className={state === 'Free' ? 'text-muted-foreground' : 'text-destructive'}>
        {t(`content.address.state.${state}`, { suggestion: suggestion ?? '' })}
      </span>
    </p>
  );
}
