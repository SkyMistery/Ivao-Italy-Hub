import { useTranslation } from 'react-i18next';

import type { Body } from '../../blocks';
import { languageNames } from '../../shared/forms';
import { Notice } from '../../shared/ui';

import { useBodyPathDescription } from './bodyPath';
import type { ContentPublishProblemsDto } from './queries';

/**
 * What is missing before this page can be published, said where the editor can act on it
 * (design M0 §5.5, §7.7).
 *
 * The server answers with a path per problem — `body.sections[0].blocks[1].props.text` — because
 * that is what it can name: it knows the envelope and nothing else. The path is turned back into
 * "Hero › Text › text" here, against the body on screen, so a coordinator is told which block to
 * open rather than handed a JSON pointer.
 *
 * ⚠️ It used to speak only **after** a refusal, and Carmine asked for it the other way round after
 * running the demo: the list is now the answer of `GET /api/content/{id}/publish-problems`, which
 * runs the very same checks without publishing. So there is one list rather than two, it is there
 * before anybody presses the button, and it empties itself when the last thing is fixed and saved.
 * The tone is a warning and not an error: nothing has gone wrong — this is a page that is not
 * finished yet.
 */

export function PublishProblems({
  body,
  problems,
}: {
  body: Body;
  /** What the server says stands in the way, or nothing while it is being asked. */
  problems: ContentPublishProblemsDto | undefined;
}) {
  const { t, i18n } = useTranslation();
  const describe = useBodyPathDescription(body);

  const entries = Object.entries(problems?.errors ?? {});
  if (entries.length === 0) {
    return null;
  }

  return (
    <Notice
      tone="warning"
      title={t('content.editor.publishMissing')}
      description={
        <ul className="flex flex-col gap-1 text-sm">
          {entries.map(([path, keys]) => {
            const missing = problems?.localized?.[path] ?? [];

            return (
              <li key={path} className="flex flex-col">
                <span className="font-medium">{describe(path)}</span>
                <span className="opacity-90">
                  {missing.length > 0
                    ? t('errors.localized.missingIn', { locales: languageNames(missing, i18n.language) })
                    : keys.map((key) => t(key)).join(' ')}
                </span>
              </li>
            );
          })}
        </ul>
      }
    />
  );
}
