import { Badge, Input, Label, Select, Textarea } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { NO_CHOICE, ProblemAlert, describeProblem } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { useMoment } from '../../shared/i18n/useMoment';
import { ConfirmDialog, Notice, useNotice } from '../../shared/ui';
import { menuParentsQuery } from '../menu/queries';

import { useReviewContent } from './mutations';
import { contentReviewQuery, pageTreeQuery, type ContentDetailDto } from './queries';

/** A translated label with nothing written yet, in every language of the division. */
function emptyLabel(locales: readonly string[]): Record<string, string> {
  return Object.fromEntries(locales.map((locale) => [locale, '']));
}

/**
 * The review of a page, on the page (G19, note 2026-09-13-contenuti-centralizzati §3.2).
 *
 * What it offers depends on two facts and nothing else: whether the page is waiting, and whether the
 * person looking may approve it. Somebody who may not approve marks a page ready — with a word and,
 * if they like, the menu entry it should have — and withdraws it while it waits. Somebody who may
 * approve reads what changed, corrects the address and the menu entry, and publishes it or sends it
 * back with a note. The server decides every one of these again.
 *
 * It sits above the editor rather than inside its toolbar, because while a page waits the editor
 * is read only and this is the part of the screen that still does something.
 */
export function ReviewPanel({
  content,
  locales,
  mayApprove,
  flush,
  onChanged,
}: {
  content: ContentDetailDto;
  locales: readonly string[];
  mayApprove: boolean;
  /** Stores what is on screen; false when it could not be stored, and then nothing is marked ready. */
  flush: () => Promise<boolean>;
  onChanged: () => void;
}) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const notice = useNotice();

  const review = useReviewContent(content.id);
  const waiting = content.status === 'Ready';

  const summary = useQuery({ ...contentReviewQuery(content.id), enabled: waiting });
  const tree = useQuery({ ...pageTreeQuery(), enabled: waiting && mayApprove });
  const menuParents = useQuery({ ...menuParentsQuery('Public'), enabled: waiting && mayApprove });

  const [note, setNote] = useState('');
  const [menuLabel, setMenuLabel] = useState<Record<string, string>>(() => emptyLabel(locales));
  const [menuParent, setMenuParent] = useState('');
  const [changelog, setChangelog] = useState('');
  const [slug, setSlug] = useState(content.slug);
  const [parent, setParent] = useState(content.parentId === null ? '' : String(content.parentId));

  const refusal = describeProblem(review.error, t, i18n.language);

  const menu = () =>
    Object.values(menuLabel).some((text) => text.trim() !== '')
      ? { parentId: menuParent === '' ? null : Number(menuParent), label: menuLabel }
      : null;

  const act = (request: Parameters<typeof review.mutate>[0], done: string) =>
    review.mutate(request, {
      onSuccess: () => {
        notice({ tone: 'success', title: t(done) });
        setNote('');
        onChanged();
      },
    });

  const menuFields = (
    <fieldset className="flex flex-col gap-2">
      <legend className="text-sm font-medium">{t('content.review.menu.title')}</legend>
      {locales.map((locale) => (
        <div key={locale} className="flex flex-col gap-1">
          <Label htmlFor={`review-menu-${locale}`}>
            {t('content.review.menu.label', { locale: locale.toUpperCase() })}
          </Label>
          <Input
            id={`review-menu-${locale}`}
            value={menuLabel[locale] ?? ''}
            onChange={(event) => setMenuLabel({ ...menuLabel, [locale]: event.target.value })}
          />
        </div>
      ))}
      {mayApprove ? (
        <div className="flex flex-col gap-1">
          <Label htmlFor="review-menu-parent">{t('content.review.menu.parent')}</Label>
          <Select
            id="review-menu-parent"
            {...(menuParent === '' ? {} : { value: menuParent })}
            onValueChange={(chosen) => setMenuParent(chosen === NO_CHOICE ? '' : chosen)}
            placeholder={t('content.review.menu.top')}
            items={[
              { value: NO_CHOICE, label: t('content.review.menu.top') },
              ...(menuParents.data?.items ?? [])
                .filter((item) => item.parentId === null)
                .map((item) => ({ value: String(item.id), label: read(item.label) || item.path })),
            ]}
          />
        </div>
      ) : null}
    </fieldset>
  );

  if (!waiting) {
    // An approver publishes directly: the editor's own button is theirs. Nothing to show here.
    if (mayApprove) {
      return null;
    }

    return (
      <div className="flex flex-col gap-2">
        {content.reviewNote === null ? null : (
          <Notice tone="warning" title={t('content.review.sentBack')} description={content.reviewNote} />
        )}
        <ProblemAlert summary={refusal} />
        <div className="flex flex-wrap items-center gap-3">
          <ConfirmDialog
            triggerText={t('content.review.ready')}
            triggerVariant="secondary"
            title={t('content.review.readyDialog.title')}
            description={t('content.review.readyDialog.description')}
            confirmText={t('content.review.readyDialog.confirm')}
            confirmVariant="primary"
            disabled={review.isPending}
            onConfirm={() => {
              void (async () => {
                if (!(await flush())) {
                  return;
                }
                const proposed = menu();
                act(
                  {
                    action: 'Ready',
                    note: note.trim() === '' ? null : note,
                    ...(proposed === null ? {} : { menu: proposed }),
                  },
                  'content.review.readyDone',
                );
              })();
            }}
          >
            <div className="flex flex-col gap-1">
              <Label htmlFor="review-note">{t('content.review.note')}</Label>
              <Textarea
                id="review-note"
                rows={3}
                maxLength={1000}
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
            </div>
            {menuFields}
          </ConfirmDialog>
        </div>
      </div>
    );
  }

  const facts = summary.data;

  return (
    <section
      aria-label={t('content.review.title')}
      className="border-border flex flex-col gap-3 rounded-lg border p-4"
    >
      <Notice
        tone="info"
        title={
          facts?.readyAt === null || facts === undefined
            ? t('content.review.waiting')
            : t('content.review.waitingSince', {
                date: moment(facts.readyAt, { time: false }),
                name: facts.readyByName ?? '',
              })
        }
        {...(content.reviewNote === null ? {} : { description: content.reviewNote })}
      />

      {facts === undefined ? null : (
        <div className="flex flex-col gap-2 text-sm">
          <p>
            <span className="text-muted-foreground">{t('content.review.address')} </span>
            <span className="font-mono">{facts.path}</span>
          </p>
          {facts.firstPublication ? (
            <p>{t('content.review.firstPublication')}</p>
          ) : (
            <ul className="flex flex-col gap-1" aria-label={t('content.review.sections')}>
              {facts.titleChanged ? (
                <li>
                  <Badge variant="flat" color="yellow" text={t('content.review.change.Changed')} />{' '}
                  {t('content.fields.title')}
                </li>
              ) : null}
              {facts.sections.map((section) => (
                <li key={section.key} className="flex items-center gap-2">
                  <Badge
                    variant="flat"
                    color={
                      section.change === 'Unchanged'
                        ? 'gray'
                        : section.change === 'Removed'
                          ? 'red'
                          : 'yellow'
                    }
                    text={t(`content.review.change.${section.change}`)}
                  />
                  <span>{read(section.title) || section.key}</span>
                </li>
              ))}
            </ul>
          )}
          {facts.menu === null ? null : (
            <p>
              <span className="text-muted-foreground">{t('content.review.menu.proposed')} </span>
              {read(facts.menu.label)}
            </p>
          )}
        </div>
      )}

      <ProblemAlert summary={refusal} />

      <div className="flex flex-wrap items-center gap-3">
        {mayApprove ? (
          <>
            <ConfirmDialog
              triggerText={t('content.review.approve')}
              triggerVariant="secondary"
              title={t('content.review.approveDialog.title')}
              description={t('content.review.approveDialog.description')}
              confirmText={t('content.review.approveDialog.confirm')}
              confirmVariant="primary"
              disabled={review.isPending}
              onConfirm={() => {
                const proposed = menu();
                act(
                  {
                    action: 'Approve',
                    changelog: changelog.trim() === '' ? null : changelog,
                    slug,
                    parentId: parent === '' ? null : Number(parent),
                    ...(proposed === null ? {} : { menu: proposed }),
                  },
                  'content.review.approveDone',
                );
              }}
            >
              <div className="flex flex-col gap-1">
                <Label htmlFor="review-changelog">{t('content.editor.publishDialog.changelog')}</Label>
                <Input
                  id="review-changelog"
                  maxLength={512}
                  value={changelog}
                  onChange={(event) => setChangelog(event.target.value)}
                />
              </div>
              <div className="flex flex-col gap-1">
                <Label htmlFor="review-parent">{t('content.fields.parentId')}</Label>
                <Select
                  id="review-parent"
                  {...(parent === '' ? {} : { value: parent })}
                  onValueChange={(chosen) => setParent(chosen === NO_CHOICE ? '' : chosen)}
                  placeholder={t('content.options.parentId.none')}
                  items={[
                    { value: NO_CHOICE, label: t('content.options.parentId.none') },
                    ...(tree.data ?? [])
                      .filter(
                        (node) =>
                          node.depth < 3 &&
                          node.id !== content.id &&
                          !node.path.startsWith(`${content.path}/`),
                      )
                      .map((node) => ({
                        value: String(node.id),
                        label: `/${node.path} — ${read(node.title) || node.path}`,
                      })),
                  ]}
                />
              </div>
              <div className="flex flex-col gap-1">
                <Label htmlFor="review-slug">{t('content.fields.slug')}</Label>
                <Input id="review-slug" value={slug} onChange={(event) => setSlug(event.target.value)} />
              </div>
              {menuFields}
            </ConfirmDialog>

            <ConfirmDialog
              triggerText={t('content.review.sendBack')}
              title={t('content.review.sendBackDialog.title')}
              description={t('content.review.sendBackDialog.description')}
              confirmText={t('content.review.sendBackDialog.confirm')}
              disabled={review.isPending}
              onConfirm={() =>
                act(
                  { action: 'SendBack', note: note.trim() === '' ? null : note },
                  'content.review.sendBackDone',
                )
              }
            >
              <div className="flex flex-col gap-1">
                <Label htmlFor="review-sendback-note">{t('content.review.note')}</Label>
                <Textarea
                  id="review-sendback-note"
                  rows={3}
                  maxLength={1000}
                  value={note}
                  onChange={(event) => setNote(event.target.value)}
                />
              </div>
            </ConfirmDialog>
          </>
        ) : (
          <ConfirmDialog
            triggerText={t('content.review.withdraw')}
            triggerVariant="secondary"
            title={t('content.review.withdrawDialog.title')}
            description={t('content.review.withdrawDialog.description')}
            confirmText={t('content.review.withdrawDialog.confirm')}
            confirmVariant="primary"
            disabled={review.isPending}
            onConfirm={() => act({ action: 'Withdraw' }, 'content.review.withdrawDone')}
          />
        )}
      </div>
    </section>
  );
}
