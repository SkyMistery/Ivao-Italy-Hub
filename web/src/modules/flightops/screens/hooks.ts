import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { useState } from 'react';

import type { Suggestion } from '../../../shared/forms';
import { listSearchSchema, type ListSearch } from '../../../shared/list';
import { aircraftTypesQuery } from '../api';

/**
 * What every back office screen of the tours shares: where it hangs, how its list reads the address, the field
 * that offers aircraft types. Written once for the module's screens.
 */

/** The route context of the back office, where every screen of the module hangs. */
export function useStaff() {
  return useRouteContext({ from: '/_staff' });
}

/** The search of a list, as the manifest's `validateSearch` produced it, and a way to change it. */
export function useListSearch() {
  // Parsed again rather than cast: a module route is not in the generated tree, so its search is untyped here.
  const search = listSearchSchema.parse(useSearch({ strict: false }));
  const navigate = useNavigate();

  return {
    search,
    onSearchChange: (patch: Partial<ListSearch>) =>
      void navigate({
        search: ((previous: ListSearch) => ({ ...previous, ...patch })) as never,
        to: '.',
      }),
  };
}

/** The types a field offers, asked again as the text changes. */
export function useTypeSuggestions() {
  const [typed, setTyped] = useState('');
  const types = useQuery({ ...aircraftTypesQuery(typed), placeholderData: keepPreviousData });

  const suggestions: Suggestion[] = (types.data ?? []).map((type) => ({
    value: type.icaoCode,
    label: `${type.icaoCode} — ${[type.manufacturer, type.model].filter(Boolean).join(' ')}`,
  }));

  return { suggestions, onSuggestSearch: (_field: string, text: string) => setTyped(text) };
}

/**
 * The suggestions, with the value a row opened with in front when the server did not offer it: a closed field would
 * otherwise put back the value it was loaded with.
 */
export function keepingCurrent(suggestions: readonly Suggestion[], current: readonly string[]): Suggestion[] {
  const kept = current.filter(
    (value) => value !== '' && !suggestions.some((suggestion) => suggestion.value === value),
  );

  return [...kept.map((value) => ({ value, label: value })), ...suggestions];
}

/** The row a form of a tour's tab edits, out of the address; null for a new one (`…/new`). */
export function useRowId(
  name: 'hubId' | 'rotationId' | 'ruleId' | 'constraintId' | 'tourRuleId',
): number | null {
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const raw = params[name] ?? 'new';
  return raw === 'new' ? null : Number(raw);
}
