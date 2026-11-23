import { Button } from '@ivao/atmosphere-react';
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
  slugIsTheirs = true,
  onUseSuggestion,
}: {
  kind: ContentKind;
  department: Department;
  slug: string;
  parentId: number | null;
  id: number | null;
  /**
   * Whether whoever writes may change the slug (design 13 §4.1). When they may not, a taken address is not answered with
   * a slug to type: a new row is offered the free one (`onUseSuggestion`), a row that exists is told who changes it.
   */
  slugIsTheirs?: boolean;
  onUseSuggestion?: ((slug: string) => void) | undefined;
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

  // Taken, for whoever may not write the slug: the free one is theirs to take only while the row is new.
  const fixed = state === 'Taken' && !slugIsTheirs;
  const offered = fixed && onUseSuggestion !== undefined && suggestion !== null && suggestion !== undefined;
  const said = !fixed
    ? t(`content.address.state.${state}`, { suggestion: suggestion ?? '' })
    : offered
      ? t('content.address.takenNew')
      : t('content.address.takenFixed');

  return (
    <div className="flex flex-col items-start gap-1">
      <p role="status" aria-label={t('content.address.label')} className="flex flex-col gap-0.5 text-sm">
        <span className="text-foreground font-mono break-all">{shown}</span>
        <span className={state === 'Free' ? 'text-muted-foreground' : 'text-destructive'}>{said}</span>
      </p>
      {offered ? (
        <Button type="button" size="sm" variant="outline" onClick={() => onUseSuggestion(suggestion)}>
          {t('content.address.useSuggestion', { suggestion })}
        </Button>
      ) : null}
    </div>
  );
}
