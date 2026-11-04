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

/**
 * The blocks whoever is composing may add at all: not retired (`BlockRegistration.retired`), and with the permission
 * the block asks for held (`BlockRegistration.permission`, asked for the department of the row). The palette draws them
 * and the instructions for Claude list them (PR 3.0a): one answer, so the two never offer different blocks.
 */
export function addableBlocks(
  blocks: readonly BlockRegistration[],
  holds: ((permission: string) => boolean) | undefined,
): BlockRegistration[] {
  return blocks.filter(
    (block) =>
      block.retired !== true &&
      (block.permission === undefined || holds === undefined || holds(block.permission)),
  );
}

/**
 * The blocks a page written with Claude may use: what may be added, without what is for fewer readers than the page,
 * which the save would refuse (#414). The download lists them and the import checks against them and takes the
 * fingerprint of the rules from them: one answer, or a file would be turned away for a block nobody offered.
 */
export function offeredBlocks(
  blocks: readonly BlockRegistration[],
  holds: (permission: string) => boolean,
  audiences: readonly { readonly type: string; readonly audience: PageVisibility }[],
  visibility: PageVisibility,
): BlockRegistration[] {
  const forFewerReaders = tooFewReaders(audiences, visibility);
  return addableBlocks(blocks, holds).filter((block) => !forFewerReaders(block.type));
}

/**
 * Whether a block is for fewer readers than the page, from the audiences `/api/me` publishes (#414). The palette shows
 * such a block disabled with the reason; the instructions for Claude leave it out, because the save would refuse it.
 * No visibility — a template, a module's briefing — means no readers of its own, and nothing is too narrow.
 */
export function tooFewReaders(
  audiences: readonly { readonly type: string; readonly audience: PageVisibility }[],
  visibility: PageVisibility | undefined,
): (type: string) => boolean {
  const byType = new Map(audiences.map((block) => [block.type, block.audience] as const));

  return (type) => {
    const audience = byType.get(type);
    return visibility !== undefined && audience !== undefined && beyondReaders(audience, visibility);
  };
}
