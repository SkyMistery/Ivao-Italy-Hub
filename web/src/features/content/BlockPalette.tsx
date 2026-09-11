import {
  AccordionContent,
  AccordionItem,
  AccordionRoot,
  AccordionTrigger,
  Button,
} from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';
import { type BlockRegistration } from '../../shared/modules';
import { SectionHeader } from '../../shared/ui';

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
}: {
  /** The section a component would be added to, and what to call it; `null` when none is chosen. */
  target: { id: string; name: string } | null;
  /** What the template allows in that section. Free rules when there is no template. */
  rule: SectionRule;
  onAdd: (type: string) => void;
}) {
  const { t } = useTranslation();

  const groups = groupsOf(registry.blocks);

  const everyGroup = groups.map((group) => group.group);
  const everySubgroup = groups.flatMap((group) =>
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
    <div className="flex flex-col gap-3 xl:sticky xl:top-20 xl:max-h-[calc(100vh-6rem)] xl:self-start xl:overflow-y-auto">
      <SectionHeader
        title={t('content.editor.components')}
        actions={
          <Button type="button" variant="ghost" size="sm" onClick={toggleAll}>
            {everythingOpen ? t('content.editor.collapseAll') : t('content.editor.expandAll')}
          </Button>
        }
      />

      {/* What a click will do, said before it is clicked rather than after nothing happens. */}
      <p className="text-muted-foreground text-xs">
        {target === null
          ? t('content.editor.componentsHint')
          : t('content.editor.addsTo', { section: target.name })}
      </p>

      <AccordionRoot type="multiple" value={openGroups} onValueChange={setOpenGroups} className="w-full">
        {groups.map((group) => (
          <AccordionItem key={group.group} value={group.group}>
            <AccordionTrigger>{t(`blocks.groups.${group.group}`)}</AccordionTrigger>
            <AccordionContent>
              <div className="flex flex-col gap-1">
                <Entries blocks={group.blocks} target={target} rule={rule} onAdd={onAdd} />

                {group.subgroups.length === 0 ? null : (
                  <AccordionRoot
                    type="multiple"
                    value={openSubgroups}
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
                            <Entries blocks={subgroup.blocks} target={target} rule={rule} onAdd={onAdd} />
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
}: {
  blocks: readonly BlockRegistration[];
  target: { id: string; name: string } | null;
  rule: SectionRule;
  onAdd: (type: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <>
      {blocks.map((block) => {
        const Icon = block.icon;

        // ⚠️ Disabled, not hidden. The outline's own palette *filters* by the template, because it
        // is drawn inside the one section it adds to. This one is beside the page and the target
        // changes as you click around: a list that changed shape every time would be a palette
        // nobody could learn. The reason is on the button instead.
        const allowed = rule.allowedBlocks === null || rule.allowedBlocks.includes(block.type);
        const disabled = target === null || !allowed;

        return (
          <Button
            key={block.type}
            type="button"
            variant="ghost"
            size="sm"
            className="justify-start"
            disabled={disabled}
            {...(target === null ? {} : allowed ? {} : { title: t('content.editor.notAllowedHere') })}
            onClick={() => onAdd(block.type)}
          >
            <Icon aria-hidden className="mr-2 size-4 shrink-0" />
            {t(block.editorLabelKey)}
          </Button>
        );
      })}
    </>
  );
}
