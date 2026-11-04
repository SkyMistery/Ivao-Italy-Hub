import { Button } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { EMBED_GUIDELINES_PATH, readBody } from '../../blocks';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { aboutKeyOf } from '../../shared/modules';
import { useNotice } from '../../shared/ui';
import { activeLinksQuery } from '../links/queries';
import { bootstrapQuery } from '../me/queries';

import { CLAUDE_KINDS, composeInstructions } from './claudeInstructions';
import { contentSettingsQuery, styleText } from './contentSettings';
import { addableBlocks, tooFewReaders } from './palette';
import { publicPageQuery, siteAddressesQuery, type ContentDetailDto, type ContentKind } from './queries';
import { pagesOfTheSite, siteAddresses } from './siteAddresses';
import type { SectionRule } from './templateRules';

/** The permission whoever downloads must hold on the row: the instructions are for writing its draft (note §3). */
const EDIT = 'Content.Edit';

/**
 * What the read of the library's links asks at the door. The site's addresses ask `Content.Edit`, which whoever downloads
 * holds (decisions/2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere.md).
 */
const LINKS_VIEW = 'Links.View';

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
 * for whoever may edit it. One press stores what is on screen first (`flush`), then composes the instructions from
 * **the row as that store answered it** (`draftBase`): its title, summary, readers and body beside its version, so that
 * the two fingerprints of the header are of the same save even when somebody goes on typing while it runs.
 */
export function ClaudeDownload({
  content,
  kind,
  locales,
  template,
  holds,
  flush,
  draftBase,
  disabled,
}: {
  content: ContentDetailDto | null;
  kind: ContentKind;
  locales: readonly string[];
  template: ReadonlyMap<string, SectionRule>;
  holds: (permission: string) => boolean;
  /** Stores what is on screen; false when it could not, and the line under the toolbar has said why. */
  flush: () => Promise<boolean>;
  /** The row the draft is based on, as the server last answered it to this editor; read after the store. */
  draftBase: () => ContentDetailDto | null;
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

  // ⚠️ The links only when their door would open: `Content.Edit` given by a grant does not bring `Links.View` with it,
  // and a refused read would have refused the whole download (review of #449, finding 1). What somebody may not read is
  // not an address they may be offered.
  const mayRead = (permission: string) =>
    bootstrap !== undefined && holdsPermissionAnywhere(bootstrap, permission);

  // The page of the division's style, as text, or null (decisions/2026-11-04-la-pagina-dello-stile.md): read with the
  // rights of whoever downloads — the settings say its address only when they can read it, and the public read of the
  // site is the one that answers, with the published version. Anything that goes wrong leaves the part out, as the note
  // of 30 October asks of a page that is not there; it never stops the download.
  const readStyle = async (): Promise<string | null> => {
    try {
      const settings = await queryClient.fetchQuery({ ...contentSettingsQuery, staleTime: 0 });
      if (settings.stylePagePath === null) {
        return null;
      }

      const answer = await queryClient.fetchQuery({
        ...publicPageQuery(settings.stylePagePath),
        staleTime: 0,
      });
      if (answer.page === null) {
        return null;
      }

      const text = styleText(readBody(answer.page.body), locales, read);
      return text === '' ? null : text;
    } catch {
      return null;
    }
  };

  const download = async () => {
    setComposing(true);
    try {
      // What is on screen first: the version the header names is the version of the draft it shows.
      if (!(await flush())) {
        notice({ tone: 'error', title: t('content.claude.download.notSaved') });
        return;
      }

      const row = draftBase();
      if (row === null) {
        return;
      }

      const [pages, links, style] = await Promise.all([
        queryClient.fetchQuery({ ...siteAddressesQuery(), staleTime: 0 }),
        mayRead(LINKS_VIEW) ? queryClient.fetchQuery(activeLinksQuery('')) : undefined,
        readStyle(),
      ]);

      // The palette's own answer, and not a block for fewer readers than the page: the save would refuse it (#414).
      const forFewerReaders = tooFewReaders(bootstrap?.registries.blocks ?? [], row.visibility);
      const blocks = addableBlocks(registry.blocks, holds).filter((block) => !forFewerReaders(block.type));

      const { markdown } = await composeInstructions({
        content: { id: row.id, kind },
        rowVersion: row.rowVersion,
        title: row.title,
        summary: row.summary,
        body: readBody(row.body),
        visibility: row.visibility,
        locales,
        blocks,
        template,
        addresses: siteAddresses(pagesOfTheSite(pages), links, t, read),
        embedGuidelines: blocks.some((block) => block.carriesSource === true)
          ? `${window.location.origin}${EMBED_GUIDELINES_PATH}`
          : null,
        words: {
          blockName: (block) => t(block.editorLabelKey),
          blockAbout: (block) => t(aboutKeyOf(block), block.about?.values ?? {}),
        },
        style,
      });

      save(`${row.slug}.claude.md`, markdown);
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
