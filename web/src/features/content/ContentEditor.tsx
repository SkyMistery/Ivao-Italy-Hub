import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Eye, Pencil, Send } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { ContentRenderer, columnsOf, readBody, type Body } from '../../blocks';
import type { Department } from '../../shared/api/bootstrap';
import { SchemaForm } from '../../shared/forms';
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
  updateBlock,
  updateSection,
} from './body';
import { emptyContent, toFormValues } from './mutations';
import { PublishProblems } from './publishProblems';
import { contentQuery, type ContentDetailDto } from './queries';
import { contentMetadataSchema, type ContentFormValues } from './schema';
import { SectionTree, type Selection } from './SectionTree';
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
  department,
  locales,
  onSave,
  onPublish,
  onDelete,
  publishError,
  busy,
}: {
  content: ContentDetailDto | null;
  department: Department;
  locales: readonly string[];
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

  const rules = template.data === undefined ? NO_RULES : templateRules(readBody(template.data.body));

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
        schema={contentMetadataSchema}
        defaults={content === null ? emptyContent(department, locales) : toFormValues(content, locales)}
        locales={locales}
        labels="content"
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

      {preview ? (
        <div className="border-border overflow-hidden rounded-lg border">
          <ContentRenderer body={body} staff />
        </div>
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
                  defaultProps(registration.schema, locales),
                  // A data block starts live: capturing is a decision somebody makes, and one that
                  // only means anything once the page is published.
                  registration.kind === 'Data' ? 'live' : null,
                );

                change(added.body);
                setSelection({ kind: 'block', id: added.id });
              }}
              onMoveSection={(id, delta) => change(moveSection(body, id, delta))}
              onMoveBlock={(id, delta) => change(moveBlock(body, id, delta))}
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
              <SectionProperties
                key={section.id}
                section={section}
                rule={ruleFor(rules, section.key)}
                locales={locales}
                onApply={(values) => {
                  const withSettings = updateSection(body, section.id, {
                    title: values.title,
                    background: values.background,
                    padding: values.padding,
                    width: values.width,
                  });

                  // Narrowing the layout has to pull the blocks back into a column that still
                  // exists, or the server refuses the save and the editor cannot say why.
                  change(clampColumns(withSettings, section.id, values.layout, columnsOf(values.layout)));
                }}
              />
            ) : block !== undefined ? (
              <BlockProperties
                key={block.block.id}
                block={block.block}
                section={block.section}
                locales={locales}
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
