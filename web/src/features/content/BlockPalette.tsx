import { useDraggable } from '@dnd-kit/core';
import {
  AccordionContent,
  AccordionItem,
  AccordionRoot,
  AccordionTrigger,
  Button,
  Input,
} from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { type BlockRegistration } from '../../shared/modules';
import { fold } from '../../shared/search/highlight';
import { SectionHeader } from '../../shared/ui';

import type { PaletteDrag } from './DropZone';
import { groupsOf } from './palette';
import type { SectionRule } from './templateRules';

/**
 * The bar of components, on the left of the editor (asked for by Carmine on 10 September 2026, the
 * fourth round on the editor: "a bar on the left with the components, every group and subgroup
 * collapsible; the components are always added in code").
 *
 * ⚠️ **It is the registry, drawn.** Every drawer, every entry and every order in here comes from
 * what the blocks declare on themselves — `group`, `subgroup`, `icon`, `editorLabelKey` — and a
 * module's blocks arrive in the same list through its manifest. There is no table of palette
 * entries and no screen where anybody arranges them: a palette that could be arranged would be a
 * second place where the catalogue lives, and the two would drift the first time somebody added a
 * block (`CLAUDE.md` §2).
 *
 * ⚠️ **It does not replace the palette in the outline**, which is per section and filtered by the
 * template. It is a pointer affordance beside it, the way this editor has added every one of them:
 * dnd-kit above the arrows in G11, clicking the page itself beside the outline on 9 September.
 */
export function BlockPalette({
  target,
  rule,
  onAdd,
  draggable = false,
}: {
  /** The section a component would be added to, and what to call it; `null` when none is chosen. */
  target: { id: string; name: string } | null;
  /** What the template allows in that section. Free rules when there is no template. */
  rule: SectionRule;
  onAdd: (type: string) => void;
  /**
   * Whether an entry can also be **dragged** onto the page (G15, session 3). Only while the page is
   * in the middle: the outline has nowhere to drop onto and a drag of its own. The click stays, and
   * is the road from a keyboard.
   */
  draggable?: boolean;
}) {
  const { t } = useTranslation();

  const arranged = groupsOf(registry.blocks);

  // A word typed to find a component by its name (Carmine, 11 September 2026: "a field to search
  // for a certain one would be very handy"). Folded the way the site's search folds, so "citta"
  // finds "Città"; a drawer with nothing left in it is not drawn, and while a word is typed every
  // drawer with a match is open — a match inside a shut drawer would be a match nobody sees.
  const [query, setQuery] = useState('');
  const typed = fold(query.trim());
  const matches = (block: BlockRegistration) => typed === '' || fold(t(block.editorLabelKey)).includes(typed);

  const groups =
    typed === ''
      ? arranged
      : arranged
          .map((group) => ({
            ...group,
            blocks: group.blocks.filter(matches),
            subgroups: group.subgroups
              .map((subgroup) => ({ ...subgroup, blocks: subgroup.blocks.filter(matches) }))
              .filter((subgroup) => subgroup.blocks.length > 0),
          }))
          .filter((group) => group.blocks.length > 0 || group.subgroups.length > 0);

  const everyGroup = arranged.map((group) => group.group);
  const everySubgroup = arranged.flatMap((group) =>
    group.subgroups.map((subgroup) => `${group.group}.${subgroup.subgroup}`),
  );

  // Everything open to begin with: a palette that opens closed hides the thing it exists to show.
  // Controlled, so that one button can open or shut all of them at once -- with five groups and
  // five subgroups, doing it one at a time is the friction this panel was supposed to remove.
  //
  // ⚠️ Two lists and not one, because there is one accordion around the groups and **one more per
  // group** around its subgroups. A controlled accordion reports the values of its own items only,
  // so a single shared list would be emptied of every other accordion's drawers the first time one
  // of them was clicked -- opening Media would have shut Content.
  const [openGroups, setOpenGroups] = useState<string[]>(() => everyGroup);
  const [openSubgroups, setOpenSubgroups] = useState<string[]>(() => everySubgroup);

  const everythingOpen =
    openGroups.length === everyGroup.length && openSubgroups.length === everySubgroup.length;

  const toggleAll = () => {
    setOpenGroups(everythingOpen ? [] : everyGroup);
    setOpenSubgroups(everythingOpen ? [] : everySubgroup);
  };

  return (
    <div className="scroll-thin flex flex-col gap-3 xl:sticky xl:top-20 xl:max-h-[calc(100vh-6rem)] xl:self-start xl:overflow-y-auto">
      <SectionHeader
        title={t('content.editor.components')}
        actions={
          <Button type="button" variant="ghost" size="sm" onClick={toggleAll}>
            {everythingOpen ? t('content.editor.collapseAll') : t('content.editor.expandAll')}
          </Button>
        }
      />

      <Input
        type="search"
        value={query}
        onChange={(event) => setQuery(event.target.value)}
        placeholder={t('content.editor.searchComponents')}
        aria-label={t('content.editor.searchComponents')}
      />

      {/* What a click will do, said before it is clicked rather than after nothing happens — and
          when nothing can happen, why: a section the template fixes greys every entry out, and
          "Adds to: Welcome" over a greyed out list read as a palette that was broken. */}
      <p className="text-muted-foreground text-xs">
        {target === null
          ? t('content.editor.componentsHint')
          : rule.locked
            ? t('content.editor.lockedTarget', { section: target.name })
            : t('content.editor.addsTo', { section: target.name })}
      </p>

      {groups.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('content.editor.noComponentMatches', { query })}</p>
      ) : null}

      <AccordionRoot
        type="multiple"
        value={typed === '' ? openGroups : groups.map((group) => group.group)}
        onValueChange={setOpenGroups}
        className="w-full"
      >
        {groups.map((group) => (
          <AccordionItem key={group.group} value={group.group}>
            <AccordionTrigger>{t(`blocks.groups.${group.group}`)}</AccordionTrigger>
            <AccordionContent>
              <div className="flex flex-col gap-1">
                <Entries
                  blocks={group.blocks}
                  target={target}
                  rule={rule}
                  onAdd={onAdd}
                  draggable={draggable}
                />

                {group.subgroups.length === 0 ? null : (
                  <AccordionRoot
                    type="multiple"
                    value={
                      typed === ''
                        ? openSubgroups
                        : group.subgroups.map((subgroup) => `${group.group}.${subgroup.subgroup}`)
                    }
                    onValueChange={(next) =>
                      // Only this group's drawers are this accordion's to report; everybody else's
                      // are carried across untouched.
                      setOpenSubgroups((previous) => [
                        ...previous.filter((value) => !value.startsWith(`${group.group}.`)),
                        ...next,
                      ])
                    }
                    className="w-full"
                  >
                    {group.subgroups.map((subgroup) => (
                      <AccordionItem key={subgroup.subgroup} value={`${group.group}.${subgroup.subgroup}`}>
                        <AccordionTrigger className="text-xs">
                          {t(`blocks.subgroups.${subgroup.subgroup}`)}
                        </AccordionTrigger>
                        <AccordionContent>
                          <div className="flex flex-col gap-1">
                            <Entries
                              blocks={subgroup.blocks}
                              target={target}
                              rule={rule}
                              onAdd={onAdd}
                              draggable={draggable}
                            />
                          </div>
                        </AccordionContent>
                      </AccordionItem>
                    ))}
                  </AccordionRoot>
                )}
              </div>
            </AccordionContent>
          </AccordionItem>
        ))}
      </AccordionRoot>
    </div>
  );
}

function Entries({
  blocks,
  target,
  rule,
  onAdd,
  draggable,
}: {
  blocks: readonly BlockRegistration[];
  target: { id: string; name: string } | null;
  rule: SectionRule;
  onAdd: (type: string) => void;
  draggable: boolean;
}) {
  return (
    <>
      {blocks.map((block) => {
        // ⚠️ Disabled, not hidden. The outline's own palette *filters* by the template, because it
        // is drawn inside the one section it adds to. This one is beside the page and the target
        // changes as you click around: a list that changed shape every time would be a palette
        // nobody could learn. The reason is on the button instead.
        const allowed = rule.allowedBlocks === null || rule.allowedBlocks.includes(block.type);

        return (
          <Entry
            key={block.type}
            block={block}
            disabled={target === null || !allowed}
            reason={target !== null && !allowed}
            draggable={draggable}
            onAdd={onAdd}
          />
        );
      })}
    </>
  );
}

function Entry({
  block,
  disabled,
  reason,
  draggable,
  onAdd,
}: {
  block: BlockRegistration;
  disabled: boolean;
  /** Whether the button is disabled for a reason worth a tooltip — the template — and not for want of a target. */
  reason: boolean;
  draggable: boolean;
  onAdd: (type: string) => void;
}) {
  const { t } = useTranslation();
  const Icon = block.icon;

  // Draggable onto the page, and a drag has to start further than a click (`SectionTree` learnt the
  // same): otherwise adding a block by clicking becomes a lottery. The entry itself does not move —
  // the panel it sits in scrolls and would clip it — what moves is the `DragOverlay` the editor draws.
  // ⚠️ A dragged palette entry, and only that, is what the page's drop slots wait for (`PaletteDrag`).
  const data: PaletteDrag = { kind: 'palette', type: block.type };
  // Only the listeners, not dnd-kit's `attributes`: those describe a thing whose *only* road is the
  // drag — a role, a tab stop, an `aria-disabled` when the drag is off — and this is a button whose
  // road from a keyboard is the click. `aria-disabled` on an enabled button was read as disabled.
  const { setNodeRef, listeners } = useDraggable({
    id: `palette:${block.type}`,
    data,
    disabled: disabled || !draggable,
  });

  return (
    <Button
      ref={setNodeRef}
      type="button"
      variant="ghost"
      size="sm"
      className="justify-start"
      disabled={disabled}
      {...(reason ? { title: t('content.editor.notAllowedHere') } : {})}
      {...listeners}
      onClick={() => onAdd(block.type)}
    >
      <Icon aria-hidden className="mr-2 size-4 shrink-0" />
      {t(block.editorLabelKey)}
    </Button>
  );
}
