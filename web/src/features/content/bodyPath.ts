import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import type { Body } from '../../blocks';
import { useLocalized } from '../../shared/i18n/useLocalized';

/**
 * Reads `body.sections[i].blocks[j].props.x` against the body on screen. A path that no longer
 * resolves — the editor moved the block since — is shown as it came, which is still more useful
 * than nothing. Read by the list of what stops a page from being published, and by a tour's list of
 * what stops it from being ready, for the paths inside its briefing (T6b).
 */
export function useBodyPathDescription(body: Body): (path: string) => string {
  const { t } = useTranslation();
  const read = useLocalized();

  return (path: string) => {
    if (path === 'title') {
      return t('content.fields.title');
    }

    const parts: string[] = [];
    const indices = [...path.matchAll(/sections\[(\d+)\]/g)].map((match) => Number(match[1]));

    let sections = body.sections;
    let section = undefined;

    for (const index of indices) {
      section = sections[index];
      if (section === undefined) {
        return path;
      }

      parts.push(read(section.title) || section.key || t('content.editor.untitledSection'));
      sections = section.sections;
    }

    const blockIndex = /blocks\[(\d+)\]/.exec(path);
    if (section !== undefined && blockIndex !== null) {
      const block = section.blocks[Number(blockIndex[1])];
      const registration = registry.blocks.find((candidate) => candidate.type === block?.type);
      parts.push(registration === undefined ? (block?.type ?? '?') : t(registration.editorLabelKey));
    }

    const property = /\.props\.(.+)$/.exec(path);
    if (property !== null) {
      parts.push(property[1]!);
    }

    return parts.length === 0 ? path : parts.join(' › ');
  };
}
