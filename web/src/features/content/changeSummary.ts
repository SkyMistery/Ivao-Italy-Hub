import type { TFunction } from 'i18next';

import type { LocalizedString } from '../../shared/api/bootstrap';

import type { ContentReviewDto } from './queries';

/** The longest line a version keeps (`cms_content_versions.changelog`). */
export const CHANGELOG_MAX = 512;

/** The fields a version keeps, and the key that names each one in a line. */
const FIELD_LABELS: Readonly<Record<string, string>> = {
  title: 'content.fields.title',
  summary: 'content.fields.summary',
  seo: 'content.fields.seo',
  collections: 'content.fields.collections',
  coverMediaId: 'content.fields.coverMediaId',
  fileMediaId: 'content.fields.fileMediaId',
  showFooter: 'content.changes.footer',
};

/**
 * The line a publication starts from (issue #352, note `2026-10-23-la-storia-delle-versioni`): what changed since the
 * version online — the sections by name, the blocks by number, the fields around the body —, written in the language on
 * screen for the person to keep or rewrite. The comparison is the server's, the one an approver reads
 * (`GET /api/content/{id}/review`); this only puts it in words.
 *
 * `read` names a section by its title in the language on screen; a section without one is named by its key.
 */
export function changeSummary(
  changes: ContentReviewDto,
  t: TFunction,
  read: (value: LocalizedString | null | undefined) => string,
): string {
  if (changes.firstPublication) {
    return t('content.changes.first');
  }

  const parts: string[] = [];

  for (const change of ['Added', 'Changed', 'Removed'] as const) {
    const names = changes.sections
      .filter((section) => section.change === change)
      .map((section) => read(section.title ?? null) || section.key);

    if (names.length > 0) {
      parts.push(t(`content.changes.sections${change}`, { names: names.join(', ') }));
    }
  }

  const blocks = (['added', 'changed', 'removed'] as const)
    .filter((change) => changes.blocks[change] > 0)
    .map((change) => t(`content.changes.blocks.${change}`, { count: changes.blocks[change] }));

  if (blocks.length > 0) {
    parts.push(t('content.changes.blocks.line', { list: blocks.join(', ') }));
  }

  if (changes.fields.length > 0) {
    parts.push(
      t('content.changes.fields', {
        names: changes.fields.map((field) => t(FIELD_LABELS[field] ?? field)).join(', '),
      }),
    );
  }

  const line = parts.length === 0 ? t('content.changes.none') : parts.join(' ');

  return line.length <= CHANGELOG_MAX ? line : `${line.slice(0, CHANGELOG_MAX - 1)}…`;
}
