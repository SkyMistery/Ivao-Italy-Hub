import { createFileRoute } from '@tanstack/react-router';

import { CORE_BLOCK_TYPES } from '../../blocks';
import { PublicListScreen } from '../../features/content/PublicListScreen';
import { publicListSearchSchema } from '../../features/content/publicSearch';

/**
 * `/documents`: everything the division has published that is meant to be kept rather than read
 * once, grouped by the shelf it was filed under.
 *
 * ⚠️ `/documents` and not `/docs`: `ContentEntry.Url` writes it this way and that is what ends up in
 * `search_index`, so plan §8.2 was corrected rather than the code (design M1 §3.3).
 */
export const Route = createFileRoute('/_public/documents/')({
  validateSearch: publicListSearchSchema,
  component: PublicDocumentsPage,
});

function PublicDocumentsPage() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  return (
    <PublicListScreen
      type={CORE_BLOCK_TYPES.documentList}
      titles="documents"
      filters={search}
      onFilter={(patch) => void navigate({ search: patch })}
      layout={{ groupByCategory: true }}
    />
  );
}
