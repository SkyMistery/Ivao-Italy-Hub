import Markdown, { type Components } from 'react-markdown';

/**
 * Prose an editor wrote, rendered as prose. The library builds a React tree and never touches
 * `innerHTML`, and raw HTML in the source is not enabled: what an editor types is text, so a
 * `<script>` in a page stays four visible words (docs/UI-GUIDELINES.md).
 *
 * Every element is given its class here rather than in a global stylesheet, because the same
 * component renders inside a block, inside a card and inside the ui-kit, and a global rule would
 * reach all three whether it suited them or not.
 */
export function MarkdownContent({ source }: { source: string }) {
  return (
    <div className="flex flex-col gap-3 leading-relaxed">
      <Markdown components={ELEMENTS}>{source}</Markdown>
    </div>
  );
}

/**
 * ⚠️ Written once, outside the component, and that is the point (issue #351): each of these is the
 * **type** of an element the library draws, and a type that is a new function at every render is a
 * different component to React — so every paragraph was taken out of the page and put back each
 * time anything above it drew. In the editor that is every keystroke and every drag, and Chromium,
 * asked to measure the page in between, left the whole preview without a box.
 */
const ELEMENTS: Components = {
  h1: ({ children }) => <h2 className="text-xl font-semibold">{children}</h2>,
  h2: ({ children }) => <h3 className="text-lg font-semibold">{children}</h3>,
  h3: ({ children }) => <h4 className="font-semibold">{children}</h4>,
  p: ({ children }) => <p>{children}</p>,
  ul: ({ children }) => <ul className="list-disc pl-6">{children}</ul>,
  ol: ({ children }) => <ol className="list-decimal pl-6">{children}</ol>,
  code: ({ children }) => (
    <code className="bg-muted text-muted-foreground rounded px-1 py-0.5 text-sm">{children}</code>
  ),
  blockquote: ({ children }) => (
    <blockquote className="border-border text-muted-foreground border-l-2 pl-4">{children}</blockquote>
  ),
  a: ({ href, children }) => (
    // An address an editor typed is an outside address: it never carries our referrer, and it
    // never gets a handle on the window it came from.
    <a
      href={href}
      className="text-primary underline underline-offset-2"
      target="_blank"
      rel="noreferrer noopener"
    >
      {children}
    </a>
  ),
};
