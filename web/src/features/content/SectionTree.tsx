import { Button } from '@ivao/atmosphere-react';
import { ArrowDown, ArrowUp, Copy, Lock, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import type { Body, SectionEnvelope } from '../../blocks';
import { useLocalized } from '../../shared/i18n/useLocalized';

import { ruleFor, type SectionRule } from './templateRules';

/**
 * The left panel of the editor: what the page is made of, and what may be done to it (design M0
 * §7.7). It is a list and not a canvas — a drag and drop page builder was decided against
 * (CLAUDE.md §7) — so a section moves with two arrows and a block with two more.
 *
 * What a locked section allows is decided here in one place: its blocks may have their properties
 * edited and nothing else. That is the template still speaking, through `templateRules`.
 */

export interface Selection {
  readonly kind: 'section' | 'block';
  readonly id: string;
}

export function SectionTree({
  body,
  rules,
  selection,
  onSelect,
  onAddSection,
  onAddBlock,
  onMoveSection,
  onMoveBlock,
  onDuplicateBlock,
  onRemoveSection,
  onRemoveBlock,
}: {
  body: Body;
  rules: ReadonlyMap<string, SectionRule>;
  selection: Selection | null;
  onSelect: (selection: Selection) => void;
  onAddSection: () => void;
  onAddBlock: (sectionId: string, type: string) => void;
  onMoveSection: (id: string, delta: -1 | 1) => void;
  onMoveBlock: (id: string, delta: -1 | 1) => void;
  onDuplicateBlock: (id: string) => void;
  onRemoveSection: (id: string) => void;
  onRemoveBlock: (id: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="flex flex-col gap-4">
      {body.sections.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('content.editor.noSections')}</p>
      ) : (
        body.sections.map((section) => (
          <SectionNode
            key={section.id}
            section={section}
            rule={ruleFor(rules, section.key)}
            rules={rules}
            selection={selection}
            onSelect={onSelect}
            onAddBlock={onAddBlock}
            onMoveSection={onMoveSection}
            onMoveBlock={onMoveBlock}
            onDuplicateBlock={onDuplicateBlock}
            onRemoveSection={onRemoveSection}
            onRemoveBlock={onRemoveBlock}
          />
        ))
      )}

      <div>
        <Button type="button" variant="secondary" size="sm" onClick={onAddSection}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('content.editor.addSection')}
        </Button>
      </div>
    </div>
  );
}

function SectionNode({
  section,
  rule,
  rules,
  selection,
  depth = 0,
  onSelect,
  onAddBlock,
  onMoveSection,
  onMoveBlock,
  onDuplicateBlock,
  onRemoveSection,
  onRemoveBlock,
}: {
  section: SectionEnvelope;
  rule: SectionRule;
  rules: ReadonlyMap<string, SectionRule>;
  selection: Selection | null;
  depth?: number;
  onSelect: (selection: Selection) => void;
  onAddBlock: (sectionId: string, type: string) => void;
  onMoveSection: (id: string, delta: -1 | 1) => void;
  onMoveBlock: (id: string, delta: -1 | 1) => void;
  onDuplicateBlock: (id: string) => void;
  onRemoveSection: (id: string) => void;
  onRemoveBlock: (id: string) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();

  const name = read(section.title) || section.key || t('content.editor.untitledSection');
  const selected = selection?.kind === 'section' && selection.id === section.id;

  return (
    <div className={`border-border flex flex-col gap-2 rounded-md border p-3 ${depth > 0 ? 'ml-4' : ''}`}>
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => onSelect({ kind: 'section', id: section.id })}
          className={`flex-1 text-left font-medium ${selected ? 'text-primary' : ''}`}
        >
          {name}
        </button>

        {rule.locked ? <Lock aria-label={t('content.editor.locked')} className="size-4 opacity-60" /> : null}

        {rule.locked ? null : (
          <>
            <IconButton label={t('content.editor.moveUp')} onClick={() => onMoveSection(section.id, -1)}>
              <ArrowUp aria-hidden className="size-4" />
            </IconButton>
            <IconButton label={t('content.editor.moveDown')} onClick={() => onMoveSection(section.id, 1)}>
              <ArrowDown aria-hidden className="size-4" />
            </IconButton>
          </>
        )}

        {rule.locked || rule.required ? null : (
          <IconButton label={t('content.editor.removeSection')} onClick={() => onRemoveSection(section.id)}>
            <Trash2 aria-hidden className="size-4" />
          </IconButton>
        )}
      </div>

      <ul className="flex flex-col gap-1">
        {section.blocks.map((block) => {
          const registration = registry.blocks.find((candidate) => candidate.type === block.type);
          const Icon = registration?.icon;
          const chosen = selection?.kind === 'block' && selection.id === block.id;

          return (
            <li key={block.id} className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => onSelect({ kind: 'block', id: block.id })}
                className={`flex flex-1 items-center gap-2 text-left text-sm ${chosen ? 'text-primary' : ''}`}
              >
                {Icon === undefined ? null : <Icon aria-hidden className="size-4" />}
                {registration === undefined
                  ? t('blocks.unknownShort', { type: block.type })
                  : t(registration.editorLabelKey)}
              </button>

              {/* A locked section still lets its blocks be edited; what it forbids is changing
                  which blocks are there, and in what order. */}
              {rule.locked ? null : (
                <>
                  <IconButton label={t('content.editor.moveUp')} onClick={() => onMoveBlock(block.id, -1)}>
                    <ArrowUp aria-hidden className="size-4" />
                  </IconButton>
                  <IconButton label={t('content.editor.moveDown')} onClick={() => onMoveBlock(block.id, 1)}>
                    <ArrowDown aria-hidden className="size-4" />
                  </IconButton>
                  <IconButton
                    label={t('content.editor.duplicate')}
                    onClick={() => onDuplicateBlock(block.id)}
                  >
                    <Copy aria-hidden className="size-4" />
                  </IconButton>
                  <IconButton label={t('content.editor.removeBlock')} onClick={() => onRemoveBlock(block.id)}>
                    <Trash2 aria-hidden className="size-4" />
                  </IconButton>
                </>
              )}
            </li>
          );
        })}
      </ul>

      {rule.locked ? null : <AddBlock sectionId={section.id} rule={rule} onAddBlock={onAddBlock} />}

      {section.sections.map((nested) => (
        <SectionNode
          key={nested.id}
          section={nested}
          rule={ruleFor(rules, nested.key)}
          rules={rules}
          selection={selection}
          depth={depth + 1}
          onSelect={onSelect}
          onAddBlock={onAddBlock}
          onMoveSection={onMoveSection}
          onMoveBlock={onMoveBlock}
          onDuplicateBlock={onDuplicateBlock}
          onRemoveSection={onRemoveSection}
          onRemoveBlock={onRemoveBlock}
        />
      ))}
    </div>
  );
}

/**
 * Which blocks may be put here. The list is the registry narrowed by whatever the template allows,
 * so a section that says "text and headings" offers exactly those two.
 */
function AddBlock({
  sectionId,
  rule,
  onAddBlock,
}: {
  sectionId: string;
  rule: SectionRule;
  onAddBlock: (sectionId: string, type: string) => void;
}) {
  const { t } = useTranslation();

  const allowed = registry.blocks.filter(
    (block) => rule.allowedBlocks === null || rule.allowedBlocks.includes(block.type),
  );

  if (allowed.length === 0) {
    return null;
  }

  // A palette rather than a select: adding a block is an action, and a select that fires one and
  // then sits there showing what was added reads as a choice that can be un-made.
  return (
    <div className="flex flex-wrap items-center gap-1 pt-1">
      <span className="text-muted-foreground pr-1 text-xs">{t('content.editor.addBlock')}</span>
      {allowed.map((block) => {
        const Icon = block.icon;
        return (
          <Button
            key={block.type}
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => onAddBlock(sectionId, block.type)}
          >
            <Icon aria-hidden className="mr-1 size-4" />
            {t(block.editorLabelKey)}
          </Button>
        );
      })}
    </div>
  );
}

function IconButton({
  label,
  onClick,
  children,
}: {
  label: string;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <Button type="button" variant="ghost" size="sm" aria-label={label} title={label} onClick={onClick}>
      {children}
    </Button>
  );
}
