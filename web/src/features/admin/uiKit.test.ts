import { expect, test } from 'vitest';

import { registry } from '../../app/registry';
import { UI_KIT_COMPONENTS } from '../../shared/ui';

import { UI_KIT_BLOCKS, UI_KIT_SECTIONS } from './uiKitSections';

/**
 * "registry ⇄ ui-kit" (design M0 §7.1). The gallery is what a component of the closed list and a
 * block of the registry are checked against: something that exists but is not shown there is
 * something nobody looks at, and it rots.
 *
 * In F8 this grows a third side — server ⇄ manifest ⇄ ui-kit — when a module declares blocks the
 * server also knows about.
 */

test('every component of the closed list has a section in the gallery', () => {
  const shown = UI_KIT_SECTIONS.map((section) => section.name);

  expect(shown).toEqual([...UI_KIT_COMPONENTS]);
});

test('the gallery shows nothing that is not on the closed list', () => {
  for (const section of UI_KIT_SECTIONS) {
    expect(UI_KIT_COMPONENTS).toContain(section.name);
  }
});

test('every block of the registry has a section in the gallery', () => {
  expect(UI_KIT_BLOCKS.map((section) => section.name)).toEqual(registry.blocks.map((block) => block.type));
});

test('a block registers example props that its own schema accepts', () => {
  // The other half of "every block is in the gallery": what is shown there has to be a block that
  // actually works, and example props its own schema refuses are a block nobody has ever mounted.
  for (const block of registry.blocks) {
    expect(block.schema.safeParse(block.example).success).toBe(true);
  }
});
