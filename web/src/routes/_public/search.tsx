import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { SearchResults } from '../../features/search/SearchResults';
import { searchQuery } from '../../features/search/queries';

/**
 * Recipe 2 (design M0 §7.3): the search, whose state is the address.
 *
 * `?q=` in the URL and not in a `useState` is the whole point of putting it there: a result worth
 * showing somebody is a link worth sending them, the back button walks the searches, and a reload
 * lands where the reader was.
 *
 * What comes back is already narrowed by the query filter, so a visitor who is nobody finds only
 * what is public — the same rows they could open, and nothing that would 404 in their face.
 */
const searchParams = z.object({
  q: z.string().catch(''),
  page: z.number().int().min(1).catch(1),
});

export const Route = createFileRoute('/_public/search')({
  validateSearch: searchParams,
  component: SearchPage,
});

function SearchPage() {
  const { i18n } = useTranslation();
  const { q, page } = Route.useSearch();
  const navigate = Route.useNavigate();

  const answer = useQuery(searchQuery(q, page, i18n.language));

  return (
    <SearchResults
      query={q}
      onQueryChange={(value) =>
        // Replaced rather than pushed: typing eight letters must not leave eight entries in the
        // history somebody then has to press "back" through.
        void navigate({ search: { q: value, page: 1 }, replace: true })
      }
      answer={answer.data}
      pending={answer.isFetching}
    />
  );
}
