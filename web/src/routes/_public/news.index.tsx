import { createFileRoute } from '@tanstack/react-router';

import { CORE_BLOCK_TYPES } from '../../blocks';
import { PublicListScreen } from '../../features/content/PublicListScreen';
import { publicListSearchSchema } from '../../features/content/publicSearch';

/**
 * `/news`: what the division has published, newest first, with the pinned ones on top.
 *
 * The list is the `newsList` data block — the same one a page embeds — so there is no second
 * reader of `cms_contents` and no endpoint of its own (design M1 §1.2 and §3.3).
 *
 * A static route beats `/$slug`, so this address stays this page whatever anybody slugs a page.
 */
export const Route = createFileRoute('/_public/news/')({
  validateSearch: publicListSearchSchema,
  component: PublicNewsPage,
});

function PublicNewsPage() {
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  return (
    <PublicListScreen
      type={CORE_BLOCK_TYPES.newsList}
      titles="news"
      filters={search}
      onFilter={(patch) => void navigate({ search: patch })}
      layout={{ layout: 'cards', pinnedFirst: true }}
    />
  );
}
