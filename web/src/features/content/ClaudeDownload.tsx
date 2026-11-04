import { Button } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import type { Body } from '../../blocks';
import type { LocalizedString } from '../../shared/api/bootstrap';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { aboutKeyOf } from '../../shared/modules';
import { useNotice } from '../../shared/ui';
import { activeLinksQuery } from '../links/queries';
import { bootstrapQuery } from '../me/queries';

import { CLAUDE_KINDS, composeInstructions, offeredBlocks } from './claudeInstructions';
import { beyondReaders, type PageVisibility } from './palette';
import { menuDestinationPagesQuery, type ContentDetailDto, type ContentKind } from './queries';
import { siteAddresses } from './siteAddresses';
import type { SectionRule } from './templateRules';

/** The permission whoever downloads must hold on the row: the instructions are for writing its draft (note §3). */
const EDIT = 'Content.Edit';

/** Where the interactive block's own guidelines are served (`EmbedEndpoints.GuidelinesPattern`). */
const EMBED_GUIDELINES = '/embed/guidelines';

/** A file of the browser's own making, saved by the browser: nothing is uploaded or stored anywhere. */
function save(name: string, text: string) {
  const url = URL.createObjectURL(new Blob([text], { type: 'text/markdown;charset=utf-8' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = name;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

/**
 * «Download for Claude» in the toolbar of the editor of a row of `cms_contents` (PR 3.0a; note
 * `2026-10-30-una-pagina-scritta-con-claude` §3).
 *
 * Only for a row that exists — before the first «save draft» there is no row and no version to name — of a kind a
 * page can be written as (a page, a piece of news, a document), not a template, not a page waiting for approval, and
 * for whoever may edit it. One press stores what is on screen first (`flush`), so that the draft the instructions show
 * is the draft the version names; then composes them (`claudeInstructions.ts`) and hands the file to the browser.
 */
export function ClaudeDownload({
  content,
  kind,
  title,
  summary,
  visibility,
  body,
  locales,
  template,
  holds,
  flush,
  draftBase,
  disabled,
}: {
  content: ContentDetailDto | null;
  kind: ContentKind;
  title: LocalizedString;
  summary: LocalizedString | null;
  visibility: PageVisibility;
  body: Body;
  locales: readonly string[];
  template: ReadonlyMap<string, SectionRule>;
  holds: (permission: string) => boolean;
  /** Stores what is on screen; false when it could not, and the line under the toolbar has said why. */
  flush: () => Promise<boolean>;
  /** The version of the row the draft is based on, read after the store. */
  draftBase: () => string | null;
  disabled: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const notice = useNotice();
  const queryClient = useQueryClient();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const [composing, setComposing] = useState(false);

  if (content === null || content.isTemplate || !CLAUDE_KINDS.includes(kind) || !holds(EDIT)) {
    return null;
  }

  const download = async () => {
    setComposing(true);
    try {
      // What is on screen first: the version the header names is the version of the draft it shows.
      if (!(await flush())) {
        notice({ tone: 'error', title: t('content.claude.download.notSaved') });
        return;
      }

      const rowVersion = draftBase();
      if (rowVersion === null) {
        return;
      }

      const [pages, links] = await Promise.all([
        queryClient.fetchQuery(menuDestinationPagesQuery('')),
        queryClient.fetchQuery(activeLinksQuery('')),
      ]);

      const audiences = new Map(
        (bootstrap?.registries.blocks ?? []).map((block) => [block.type, block.audience] as const),
      );
      const blocks = offeredBlocks(registry.blocks, holds, (type) => {
        const audience = audiences.get(type);
        return audience !== undefined && beyondReaders(audience, visibility);
      });

      const { markdown } = await composeInstructions({
        content: { id: content.id, kind },
        rowVersion,
        title,
        summary,
        body,
        visibility,
        locales,
        blocks,
        template,
        addresses: siteAddresses(pages, links, t, read),
        embedGuidelines: blocks.some((block) => block.carriesSource === true)
          ? `${window.location.origin}${EMBED_GUIDELINES}`
          : null,
        words: {
          blockName: (block) => t(block.editorLabelKey),
          blockAbout: (block) => t(aboutKeyOf(block), block.about?.values ?? {}),
        },
        style: null,
      });

      save(`${content.slug}.claude.md`, markdown);
    } catch {
      notice({ tone: 'error', title: t('content.claude.download.failed') });
    } finally {
      setComposing(false);
    }
  };

  return (
    <Button
      type="button"
      variant="ghost"
      disabled={disabled || composing}
      title={t('content.claude.download.hint')}
      onClick={() => void download()}
    >
      <Download className="size-4" aria-hidden />
      {t('content.claude.download.label')}
    </Button>
  );
}
