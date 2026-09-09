import { Button } from '@ivao/atmosphere-react';
import {
  DndContext,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core';
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { ArrowDown, ArrowUp, Copy, GripVertical, Lock, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import type { BlockEnvelope, Body, SectionEnvelope } from '../../blocks';
import { useLocalized } from '../../shared/i18n/useLocalized';

import { ruleFor, type SectionRule } from './templateRules';

/**
 * The left panel of the editor: what the page is made of, and what may be done to it (design M0
 * §7.7). It is a list and not a canvas — a drag and drop page builder was decided against
 * (CLAUDE.md §7) — so a section moves by being dragged within its list, or with two arrows, and a
 * block with the same two.
 *
 * ⚠️ The arrows are not a leftover of the version before dnd-kit and are not to be tidied away:
 * dragging is a pointer, and the arrows are the whole of this panel that works from a keyboard
 * (design M1 §9.3). Whoever removes them removes reordering for anybody who cannot use a mouse.
 *
 * What a locked section allows is decided here in one place: its blocks may have their properties
 * edited and nothing else — no arrows, no handle, no palette. That is the template still speaking,
 * through `templateRules`.
 */

export interface Selection {
  readonly kind: 'section' | 'block';
  readonly id: string;
}

/** Which list a dragged row belongs to, carried by the row rather than read off its identifier. */
type RowKind = 'section' | 'block';

export function SectionTree({
  body,
  rules,
  selection,
  onSelect,
  onAddSection,
  onAddBlock,
  onMoveSection,
  onMoveBlock,
  onReorderSections,
  onReorderBlocks,
  onDuplicateBlock,
  onRemoveSection,
  onRemoveBlock,
}: {
  body: Body;
  rules: ReadonlyMap<string, SectionRule>;
  selection: Selection | null;
  /** `null` is the page itself, whose properties are the row's own: address, title, audience, SEO. */
  onSelect: (selection: Selection | null) => void;
  onAddSection: () => void;
  onAddBlock: (sectionId: string, type: string) => void;
  onMoveSection: (id: string, delta: -1 | 1) => void;
  onMoveBlock: (id: string, delta: -1 | 1) => void;
  /** A section dropped onto another one of the same list. */
  onReorderSections: (activeId: string, overId: string) => void;
  onReorderBlocks: (activeId: string, overId: string) => void;
  onDuplicateBlock: (id: string) => void;
  onRemoveSection: (id: string) => void;
  onRemoveBlock: (id: string) => void;
}) {
  const { t } = useTranslation();

  // A drag has to start further than a click, or selecting a section by clicking its name becomes
  // a lottery between a selection and a one pixel drag.
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 4 } }));

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (over === null || active.id === over.id) {
      return;
    }

    const kind = active.data.current?.['kind'] as RowKind | undefined;
    const move = kind === 'block' ? onReorderBlocks : onReorderSections;

    // Crossing from one list into another is refused by the body helpers rather than here: a drop
    // onto a row of another section simply moves nothing.
    move(String(active.id), String(over.id));
  };

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={onDragEnd}>
      <div className="flex flex-col gap-4">
        {/* ⚠️ The page is the first thing in the tree, and picking it opens its own properties in
            the same panel a section and a block use. Before 9 September 2026 those lived in a form
            above the editor that measured 1182 pixels — taller than the window — so both the page
            and the buttons that save it were below the fold. One panel, three kinds of thing. */}
        <button
          type="button"
          onClick={() => onSelect(null)}
          className={`rounded-md border px-3 py-2 text-left text-sm font-medium ${
            selection === null ? 'border-primary bg-accent' : 'border-border'
          }`}
        >
          {t('content.editor.page')}
        </button>

        {body.sections.length === 0 ? (
          <p className="text-muted-foreground text-sm">{t('content.editor.noSections')}</p>
        ) : (
          <SortableContext
            items={body.sections.map((section) => section.id)}
            strategy={verticalListSortingStrategy}
          >
            {body.sections.map((section) => (
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
            ))}
          </SortableContext>
        )}

        <div>
          <Button type="button" variant="secondary" size="sm" onClick={onAddSection}>
            <Plus aria-hidden className="mr-2 size-4" />
            {t('content.editor.addSection')}
          </Button>
        </div>
      </div>
    </DndContext>
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
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, style } = useRow(section.id, 'section');

  const name = read(section.title) || section.key || t('content.editor.untitledSection');
  const selected = selection?.kind === 'section' && selection.id === section.id;

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`border-border flex flex-col gap-2 rounded-md border p-3 ${depth > 0 ? 'ml-4' : ''}`}
    >
      <div className="flex flex-wrap items-center gap-2">
        {rule.locked ? null : (
          <DragHandle setRef={setActivatorNodeRef} attributes={attributes} listeners={listeners} />
        )}

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

      <SortableContext items={section.blocks.map((block) => block.id)} strategy={verticalListSortingStrategy}>
        <ul className="flex flex-col gap-1">
          {section.blocks.map((block) => (
            <BlockNode
              key={block.id}
              block={block}
              locked={rule.locked}
              selected={selection?.kind === 'block' && selection.id === block.id}
              onSelect={onSelect}
              onMoveBlock={onMoveBlock}
              onDuplicateBlock={onDuplicateBlock}
              onRemoveBlock={onRemoveBlock}
            />
          ))}
        </ul>
      </SortableContext>

      {rule.locked ? null : <AddBlock sectionId={section.id} rule={rule} onAddBlock={onAddBlock} />}

      <SortableContext
        items={section.sections.map((nested) => nested.id)}
        strategy={verticalListSortingStrategy}
      >
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
      </SortableContext>
    </div>
  );
}

function BlockNode({
  block,
  locked,
  selected,
  onSelect,
  onMoveBlock,
  onDuplicateBlock,
  onRemoveBlock,
}: {
  block: BlockEnvelope;
  locked: boolean;
  selected: boolean;
  onSelect: (selection: Selection) => void;
  onMoveBlock: (id: string, delta: -1 | 1) => void;
  onDuplicateBlock: (id: string) => void;
  onRemoveBlock: (id: string) => void;
}) {
  const { t } = useTranslation();
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, style } = useRow(block.id, 'block');

  const registration = registry.blocks.find((candidate) => candidate.type === block.type);
  const Icon = registration?.icon;

  return (
    <li ref={setNodeRef} style={style} className="flex flex-wrap items-center gap-2">
      {/* A locked section still lets its blocks be edited; what it forbids is changing which blocks
          are there, and in what order. */}
      {locked ? null : (
        <DragHandle setRef={setActivatorNodeRef} attributes={attributes} listeners={listeners} />
      )}

      <button
        type="button"
        onClick={() => onSelect({ kind: 'block', id: block.id })}
        className={`flex flex-1 items-center gap-2 text-left text-sm ${selected ? 'text-primary' : ''}`}
      >
        {Icon === undefined ? null : <Icon aria-hidden className="size-4" />}
        {registration === undefined
          ? t('blocks.unknownShort', { type: block.type })
          : t(registration.editorLabelKey)}
      </button>

      {locked ? null : (
        <>
          <IconButton label={t('content.editor.moveUp')} onClick={() => onMoveBlock(block.id, -1)}>
            <ArrowUp aria-hidden className="size-4" />
          </IconButton>
          <IconButton label={t('content.editor.moveDown')} onClick={() => onMoveBlock(block.id, 1)}>
            <ArrowDown aria-hidden className="size-4" />
          </IconButton>
          <IconButton label={t('content.editor.duplicate')} onClick={() => onDuplicateBlock(block.id)}>
            <Copy aria-hidden className="size-4" />
          </IconButton>
          <IconButton label={t('content.editor.removeBlock')} onClick={() => onRemoveBlock(block.id)}>
            <Trash2 aria-hidden className="size-4" />
          </IconButton>
        </>
      )}
    </li>
  );
}

function useRow(id: string, kind: RowKind) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } =
    useSortable({ id, data: { kind } });

  return {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    isDragging,
    style: { transform: CSS.Translate.toString(transform), transition, opacity: isDragging ? 0.6 : 1 },
  };
}

/**
 * What is dragged. A handle rather than the whole row, because the row is made of buttons: making
 * it all draggable turns every click on "remove" into a race between a press and a drag.
 */
function DragHandle({
  setRef,
  attributes,
  listeners,
}: {
  setRef: (element: HTMLElement | null) => void;
  attributes: ReturnType<typeof useRow>['attributes'];
  listeners: ReturnType<typeof useRow>['listeners'];
}) {
  const { t } = useTranslation();

  return (
    <button
      type="button"
      ref={setRef}
      aria-label={t('content.editor.reorder')}
      title={t('content.editor.reorder')}
      className="text-muted-foreground cursor-grab touch-none"
      {...attributes}
      {...listeners}
    >
      <GripVertical aria-hidden className="size-4" />
    </button>
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
