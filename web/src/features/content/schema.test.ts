import { expect, test } from 'vitest';

import { CONTENT_SLUG_MAX_LENGTH, contentMetadataSchema } from './schema';

// The review of #554, finding 2: the slug of a page, a news item or a document is proposed from the title in a field its
// writer may not see, so the proposal is cut where the column ends — `ContentWriteDtoValidator.MaxSlugLength`, 160.
test.each(['Page', 'News', 'Document'] as const)(
  'the slug of a %s is proposed no longer than the column',
  (kind) => {
    const hidden = contentMetadataSchema(kind, [], undefined, undefined, false);

    expect(CONTENT_SLUG_MAX_LENGTH).toBe(160);
    expect(hidden.shape.slug.meta()).toMatchObject({
      slugFrom: 'title',
      slugMaxLength: CONTENT_SLUG_MAX_LENGTH,
      hidden: true,
    });
  },
);
