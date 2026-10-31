import {
  BLOCK_GROUPS,
  BLOCK_SUBGROUPS,
  type BlockGroup,
  type BlockRegistration,
  type BlockSubgroup,
} from '../../shared/modules';

interface PaletteGroup {
  readonly group: BlockGroup;
  /** The ones that declare no subgroup, drawn straight under the group's own heading. */
  readonly blocks: readonly BlockRegistration[];
  readonly subgroups: readonly { subgroup: BlockSubgroup; blocks: readonly BlockRegistration[] }[];
}

/**
 * The registry, arranged. The order of the drawers is the order of `BLOCK_GROUPS` and
 * `BLOCK_SUBGROUPS` -- not the order the blocks happen to be registered in, which is an accident of
 * how they were written -- and a drawer with nothing in it is not drawn at all, so a fork that
 * registers no data block simply has no Data drawer.
 */
export function groupsOf(blocks: readonly BlockRegistration[]): readonly PaletteGroup[] {
  return BLOCK_GROUPS.map((group) => {
    const mine = blocks.filter((block) => block.group === group);

    return {
      group,
      blocks: mine.filter((block) => block.subgroup === undefined),
      subgroups: BLOCK_SUBGROUPS.map((subgroup) => ({
        subgroup,
        blocks: mine.filter((block) => block.subgroup === subgroup),
      })).filter((entry) => entry.blocks.length > 0),
    };
  }).filter((entry) => entry.blocks.length > 0 || entry.subgroups.length > 0);
}

/** The visibilities of a row, from the most readers to the fewest: the order of the server's own enum. */
const WIDEST_FIRST = ['Public', 'Members', 'Staff', 'Department'] as const;

export type PageVisibility = (typeof WIDEST_FIRST)[number];

/**
 * Whether a block declared for `audience` is for fewer readers than a page of `visibility` — a queue of the staff on a
 * page anybody reads (note 2026-10-28-il-pubblico-di-un-blocco). The audience is the server's, from `/api/me`; this only
 * reads it to grey the entry out, and the save is what refuses (`errors.body.blockAudience`), so a disagreement here
 * would cost a click, never a page.
 */
export function beyondReaders(audience: PageVisibility, visibility: PageVisibility): boolean {
  return WIDEST_FIRST.indexOf(audience) > WIDEST_FIRST.indexOf(visibility);
}
