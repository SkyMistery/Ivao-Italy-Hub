import { Button } from '@ivao/atmosphere-react';
import { FileWarning, Lock } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import type { Body } from '../../blocks';
import type { Department, LocalizedString } from '../../shared/api/bootstrap';
import { useLocalized } from '../../shared/i18n/useLocalized';

import { findSection } from './body';
import type { AlignableDifference, TemplateDifference } from './templateDiff';
import { MANAGE_TEMPLATES } from './templateRules';

/**
 * What the template says about this page, in the two places it has to be said (design M1 §9.1,
 * §9.3, plan §G11 tasks 1–3).
 *
 * The rule is unchanged and both halves of this file exist to say so out loud: a template never
 * rewrites a page by itself, and what a visitor reads is the published version until somebody
 * publishes again. So there is a line per difference and a button per line — and no button that
 * applies them all, because a button that rewrites a page in one press is a button somebody presses
 * by mistake.
 */

export function TemplateDifferences({
  body,
  differences,
  onAlign,
}: {
  body: Body;
  differences: readonly TemplateDifference[];
  onAlign: (difference: AlignableDifference) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  if (differences.length === 0) {
    return null;
  }

  /** What to call a section: what it is titled here, or the key the two sides are matched by. */
  const name = (difference: TemplateDifference): string => {
    const title: LocalizedString | null | undefined =
      difference.kind === 'added' ? difference.section.title : findSection(body, difference.id)?.title;

    return read(title) || difference.key;
  };

  return (
    <div role="status" className="border-border bg-muted/30 flex flex-col gap-3 rounded-md border p-4">
      <p className="flex items-center gap-2 font-medium">
        <FileWarning aria-hidden className="size-4" />
        {t('content.editor.template.differences')}
      </p>

      {/* The sentence that stops this panel from reading as an alarm: nothing on screen has changed
          for anybody, and nothing will until this page is published again. */}
      <p className="text-muted-foreground text-sm">{t('content.editor.template.publicUnchanged')}</p>

      <ul className="flex flex-col gap-3">
        {differences.map((difference) => (
          <li key={`${difference.kind}:${difference.key}`} className="flex flex-wrap items-start gap-2">
            <div className="flex min-w-0 flex-1 flex-col">
              <span className="text-sm">
                {t(`content.editor.template.${difference.kind}`, { section: name(difference) })}
              </span>

              {difference.kind === 'changed' ? (
                <span className="text-muted-foreground text-sm">
                  {difference.reasons
                    .map((reason) => t(`content.editor.template.reasons.${reason}`))
                    .join(' ')}
                </span>
              ) : null}
            </div>

            {difference.kind === 'changed' ? (
              // Deliberately no action. Aligning this one would mean throwing away blocks somebody
              // wrote, and what to do about it is a decision rather than a button.
              <span className="text-muted-foreground text-sm">
                {t('content.editor.template.yoursToDecide')}
              </span>
            ) : (
              <Button type="button" variant="secondary" size="sm" onClick={() => onAlign(difference)}>
                {t(`content.editor.template.apply.${difference.kind}`)}
              </Button>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Why the structure of this section cannot be touched, said above the properties that can. A
 * disabled button with no explanation produces a ticket; a line that names the template and the
 * permission does not.
 */
export function LockedByTemplate({
  template,
  canManage,
}: {
  /**
   * Which template fixes it, and whose it is — the permission is held on a department and never in
   * general. Null while the template is still being fetched, or when this reader cannot open it.
   */
  template: { title: LocalizedString; department: Department } | null;
  canManage: boolean;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const title = template === null ? '' : read(template.title);

  return (
    <p className="text-muted-foreground flex items-start gap-2 text-sm">
      <Lock aria-hidden className="mt-0.5 size-4 shrink-0" />
      <span>
        {template === null || title === '' ? (
          t('content.editor.template.lockedByUnknown')
        ) : (
          <>
            {t('content.editor.template.lockedBy', { template: title })}{' '}
            {canManage
              ? t('content.editor.template.lockedYouMay', { department: template.department })
              : t('content.editor.template.lockedWhoMay', {
                  permission: MANAGE_TEMPLATES,
                  department: template.department,
                })}
          </>
        )}
      </span>
    </p>
  );
}
