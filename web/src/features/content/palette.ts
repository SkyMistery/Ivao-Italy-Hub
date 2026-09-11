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
