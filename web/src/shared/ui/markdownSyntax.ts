import { directiveFromMarkdown } from 'mdast-util-directive';
import { gfmStrikethroughFromMarkdown } from 'mdast-util-gfm-strikethrough';
import { directive } from 'micromark-extension-directive';
import { gfmStrikethrough } from 'micromark-extension-gfm-strikethrough';

import { isTextColour, textColourClass } from './textColours';

/**
 * The two pieces of syntax `MarkdownContent` has beyond CommonMark (design of the editor, phase 3, §4.6): `~~struck~~` and
 * `:ocean[a coloured word]`, and nothing else.
 *
 * ⚠️ Only those two, and not `remark-gfm` and `remark-directive` whole, because a body already saved draws as it did
 * (design §6). Measured on 7 November 2026 with both packages: with `remark-gfm` a Markdown table, a footnote and the box of
 * a task list went from visible text to nothing under an `allowedElements`, and `www.…` became a link; with
 * `remark-directive` `10:30` lost its `:30`, `Frequenza:LIRF_APP` its second half, and `:::note` swallowed the lines under it.
 * So the strikethrough is the part of GFM that is wanted, double tilde only (`~30 minuti` is not a mark), and a directive
 * is only the kind that sits inside a line.
 */

interface MarkdownNode {
  type: string;
  name?: string;
  children?: MarkdownNode[];
  value?: string;
  alt?: string;
  data?: { hName: string; hProperties: { className: string } };
  position?: { start: { offset?: number }; end: { offset?: number } };
}

/** A name that can be a colour. `:30` of `10:30[1]` is a number, and only ever text. */
const COLOUR_NAME = /^[a-z]+$/;

const PICTURE_WORDS = /^!\[([^\]]*)\]/;

/**
 * Every directive that is not a colour goes back to being what somebody wrote. Without a label it is a colon in a sentence
 * (`10:30`, `Frequenza:LIRF_APP`), so it is the source, whole. With one it is a colour outside the set: the words stay, plain
 * (design §4.6), which is also what the search reads (`BlockDocumentWalker.Prose`).
 */
function drawColours(node: MarkdownNode, source: string) {
  if (node.children === undefined) {
    return;
  }

  node.children = node.children.flatMap((child) => {
    drawColours(child, source);

    const written = source.slice(child.position?.start.offset, child.position?.end.offset);

    // ⚠️ The words of a picture are read into `alt` before this runs, and a directive in them has no words: `![foto
    // 10:30](…)` came out as «foto 10». A colon there is taken back from the source.
    if (child.type === 'image' || child.type === 'imageReference') {
      const words = PICTURE_WORDS.exec(written)?.[1];
      return [words?.includes(':') === true ? { ...child, alt: words } : child];
    }

    if (child.type !== 'textDirective') {
      return [child];
    }

    const name = child.name ?? '';

    if (child.children === undefined || child.children.length === 0 || !COLOUR_NAME.test(name)) {
      return [{ type: 'text', value: written }];
    }

    return isTextColour(name)
      ? [{ ...child, data: { hName: 'span', hProperties: { className: textColourClass(name) } } }]
      : child.children;
  });
}

/** A plugin of `unified`, which hands the processor as `this`: the shape `remark-gfm` itself is written in. */
export function hubMarkdown(this: {
  data: () => { micromarkExtensions?: unknown[]; fromMarkdownExtensions?: unknown[] };
}) {
  const data = this.data();

  (data.micromarkExtensions ??= []).push(gfmStrikethrough({ singleTilde: false }), {
    text: directive().text,
  });
  (data.fromMarkdownExtensions ??= []).push(gfmStrikethroughFromMarkdown(), directiveFromMarkdown());

  return (tree: MarkdownNode, file: { toString: () => string }) => drawColours(tree, file.toString());
}
