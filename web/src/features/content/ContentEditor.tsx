import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Eye, Pencil, Send } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { columnsOf, readBody, type Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { SchemaForm, writtenValues, type ChoiceOption } from '../../shared/forms';
import type { MediaLibraryQuery } from '../../shared/ui';
import { ConfirmDialog, SectionHeader } from '../../shared/ui';

import { BlockProperties, SectionProperties } from './BlockProperties';
import {
  addBlock,
  addSection,
  clampColumns,
  defaultProps,
  duplicateBlock,
  findBlock,
  findSection,
  moveBlock,
  moveSection,
  removeBlock,
  removeSection,
  reorderBlocks,
  reorderSections,
  updateBlock,
  updateSection,
} from './body';
import { emptyContent, toFormValues } from './mutations';
import { PreviewFrame } from './PreviewFrame';
import { PublishProblems } from './publishProblems';
import { contentQuery, type ContentDetailDto, type ContentKind } from './queries';
import { contentMetadataSchema, type ContentFormValues } from './schema';
import { SectionTree, type Selection } from './SectionTree';
import { applyDifference, templateDiff } from './templateDiff';
import { LockedByTemplate, TemplateDifferences } from './TemplatePanel';
import { NO_RULES, ruleFor, templateRules } from './templateRules';

/**
 * The list editor of M0 (design M0 §7.7). Metadata at the top, what the page is made of on the
 * left, whatever is selected on the right, and a preview that is the very same renderer a visitor
 * gets — because "what will this look like" and "what does this look like" must not be two pieces
 * of code that can disagree.
 *
 * The draft lives in state and is saved whole. Publishing is a separate action on the *saved* row,
 * and is refused while there are unsaved changes: publishing what is on screen rather than what is
 * stored would be a page that says something nobody saved.
 */
export function ContentEditor({
  content,
  kind,
  categories,
  department,
  locales,
  division,
  mediaLibrary,
  canManageTemplates,
  onSave,
  onPublish,
  onDelete,
  publishError,
  busy,
}: {
  content: ContentDetailDto | null;
  /** Which kind this row is. Fixed by the list it was opened from, never a field on the form. */
  kind: ContentKind;
  /** The shelves of this department, already resolved into the language on screen. */
  categories: readonly ChoiceOption[];
  department: Department;
  locales: readonly string[];
  /** The two facts the `seo` field needs: which language is the fallback, and where the division is. */
  division: { defaultLocale: string; timezone: string };
  /** The library the picture of `seo` is chosen from — this department's. */
  mediaLibrary: MediaLibraryQuery;
  /**
   * Whether this member may change a template of that department. Asked as a question rather than
   * handed the bootstrap, because the answer is about the template's department and not this page's
   * — and which department that is, only the loaded template knows.
   */
  canManageTemplates: (department: Department) => boolean;
  onSave: (values: ContentFormValues, body: Body) => Promise<unknown>;
  /** Null for a row that does not exist yet: there is nothing to publish until it is saved once. */
  onPublish: (() => void) | null;
  onDelete: (() => void) | null;
  publishError: unknown;
  busy: boolean;
}) {
  const { t } = useTranslation();

  const [body, setBody] = useState<Body>(() => readBody(content?.body));
  const [selection, setSelection] = useState<Selection | null>(null);
  const [preview, setPreview] = useState(false);
  const [unsaved, setUnsaved] = useState(false);

  // What the template still says about this page: which sections are fixed, which are locked, and
  // which blocks may go in them. The page itself does not carry any of it (see `templateRules`).
  const template = useQuery({
    ...contentQuery(content?.templateId ?? 0),
    enabled: typeof content?.templateId === 'number',
  });

  const templateBody = template.data === undefined ? null : readBody(template.data.body);
  const rules = templateBody === null ? NO_RULES : templateRules(templateBody);

  // And what it says that this page does not. Nothing here changes anything by itself: the list is
  // read out, and one line at a time is acted on (design M1 §9.1).
  const differences = templateDiff(body, templateBody);

  const change = (next: Body) => {
    setBody(next);
    setUnsaved(true);
  };

  const section = selection?.kind === 'section' ? findSection(body, selection.id) : undefined;
  const block = selection?.kind === 'block' ? findBlock(body, selection.id) : undefined;

  return (
    <div className="flex flex-col gap-8">
      <PublishProblems body={body} error={publishError} />

      <SchemaForm
        // Remounted whenever the stored row moves on, so the version the form carries is the one
        // the server last returned; keeping a stale one would answer 409 on the next save.
        key={content?.rowVersion ?? 'new'}
        schema={contentMetadataSchema(kind, categories)}
        defaults={content === null ? emptyContent(department, locales, kind) : toFormValues(content, locales)}
        locales={locales}
        labels="content"
        division={division}
        mediaLibrary={mediaLibrary}
        onSubmit={async (values) => {
          await onSave(values, body);
          setUnsaved(false);
        }}
        submitLabel={t('content.editor.saveDraft')}
        secondaryAction={
          <>
            <Button type="button" variant="ghost" onClick={() => setPreview((shown) => !shown)}>
              {preview ? (
                <Pencil aria-hidden className="mr-2 size-4" />
              ) : (
                <Eye aria-hidden className="mr-2 size-4" />
              )}
              {preview ? t('content.editor.backToEditing') : t('content.editor.preview')}
            </Button>

            {onPublish === null ? null : (
              <Button
                type="button"
                variant="secondary"
                disabled={unsaved || busy}
                onClick={onPublish}
                title={unsaved ? t('content.editor.saveBeforePublishing') : undefined}
              >
                <Send aria-hidden className="mr-2 size-4" />
                {t('content.editor.publish')}
              </Button>
            )}

            {onDelete === null ? null : (
              <ConfirmDialog
                triggerText={t('common.delete')}
                title={t('content.delete.title')}
                description={t('content.delete.description')}
                confirmText={t('common.delete')}
                disabled={busy}
                onConfirm={onDelete}
              />
            )}
          </>
        }
      />

      {unsaved ? (
        <p className="text-muted-foreground text-sm">{t('content.editor.saveBeforePublishing')}</p>
      ) : null}

      <TemplateDifferences
        body={body}
        differences={differences}
        onAlign={(difference) => change(applyDifference(body, templateBody, difference))}
      />

      {preview ? (
        <PreviewFrame body={body} />
      ) : (
        <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
          <div className="flex flex-col gap-4">
            <SectionHeader title={t('content.editor.structure')} />
            <SectionTree
              body={body}
              rules={rules}
              selection={selection}
              onSelect={setSelection}
              onAddSection={() => {
                const added = addSection(body, locales);
                change(added.body);
                setSelection({ kind: 'section', id: added.id });
              }}
              onAddBlock={(sectionId, type) => {
                const registration = registry.blocks.find((candidate) => candidate.type === type);
                if (registration === undefined) {
                  return;
                }

                const added = addBlock(
                  body,
                  sectionId,
                  type,
                  // The blank properties, minus the optional ones nobody has written into: a block
                  // added and never opened must not carry an empty translated value, which
                  // publication would read as a page translated into one language only.
                  writtenValues(registration.schema, defaultProps(registration.schema, locales)),
                  // A data block starts live: capturing is a decision somebody makes, and one that
                  // only means anything once the page is published.
                  registration.kind === 'Data' ? 'live' : null,
                );

                change(added.body);
                setSelection({ kind: 'block', id: added.id });
              }}
              onMoveSection={(id, delta) => change(moveSection(body, id, delta))}
              onMoveBlock={(id, delta) => change(moveBlock(body, id, delta))}
              onReorderSections={(activeId, overId) => change(reorderSections(body, activeId, overId))}
              onReorderBlocks={(activeId, overId) => change(reorderBlocks(body, activeId, overId))}
              onDuplicateBlock={(id) => {
                const copy = duplicateBlock(body, id);
                change(copy.body);
                setSelection({ kind: 'block', id: copy.id });
              }}
              onRemoveSection={(id) => {
                change(removeSection(body, id));
                setSelection(null);
              }}
              onRemoveBlock={(id) => {
                change(removeBlock(body, id));
                setSelection(null);
              }}
            />
          </div>

          <div className="flex flex-col gap-4">
            <SectionHeader title={t('content.editor.properties')} />

            {section !== undefined ? (
              <>
                {ruleFor(rules, section.key).locked ? (
                  <LockedByTemplate
                    template={
                      template.data === undefined
                        ? null
                        : { title: template.data.title, department: template.data.ownerDepartment }
                    }
                    canManage={
                      template.data !== undefined && canManageTemplates(template.data.ownerDepartment)
                    }
                  />
                ) : null}

                <SectionProperties
                  key={section.id}
                  section={section}
                  rule={ruleFor(rules, section.key)}
                  isTemplate={content?.isTemplate === true}
                  locales={locales}
                  division={division}
                  mediaLibrary={mediaLibrary}
                  onApply={(values) => {
                    const withSettings = updateSection(body, section.id, {
                      title: values.title,
                      background: values.background,
                      mediaId: values.mediaId ?? null,
                      padding: values.padding,
                      width: values.width,
                      // What a template imposes, and only on a template: on a page the form does
                      // not draw these, and writing them would be a 400 from the envelope
                      // validator — a page carrying them could lift its own restrictions.
                      ...(content?.isTemplate === true
                        ? {
                            ...(typeof values.key === 'string' && values.key.trim() !== ''
                              ? { key: values.key.trim() }
                              : {}),
                            required: values.required === true,
                            locked: values.locked === true,
                            // Nothing ticked means "any block", which is the absence of the key
                            // and not an empty list: an empty one would allow nothing at all.
                            allowedBlocks:
                              values.allowedBlocks === undefined || values.allowedBlocks.length === 0
                                ? null
                                : [...values.allowedBlocks],
                          }
                        : {}),
                    });

                    // Narrowing the layout has to pull the blocks back into a column that still
                    // exists, or the server refuses the save and the editor cannot say why.
                    change(clampColumns(withSettings, section.id, values.layout, columnsOf(values.layout)));
                  }}
                />
              </>
            ) : block !== undefined ? (
              <BlockProperties
                key={block.block.id}
                block={block.block}
                section={block.section}
                locales={locales}
                division={division}
                mediaLibrary={mediaLibrary}
                onApplyProps={(props) => change(updateBlock(body, block.block.id, { props }))}
                onEnvelope={(patch) => change(updateBlock(body, block.block.id, patch))}
              />
            ) : (
              <p className="text-muted-foreground text-sm">{t('content.editor.nothingSelected')}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
