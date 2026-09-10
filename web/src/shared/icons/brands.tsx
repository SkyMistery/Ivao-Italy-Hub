import type { ComponentProps } from 'react';

/**
 * The marks of the places a division has an account, drawn here because `lucide` has none.
 *
 * This is the case `docs/UI-GUIDELINES.md` §2 anticipated and the folder was made for: *if an icon
 * `lucide` genuinely lacks is needed, it is drawn by hand here, in the same style — never inline in
 * a screen.* Lucide carried brand marks until version 1 and then dropped every one of them, which
 * is checked and not assumed: there is no `twitter`, `facebook`, `instagram`, `youtube` or
 * `discord` in the package this repository installs.
 *
 * ⚠️ **They are simplified marks, not the brands' own artwork.** Each is drawn to lucide's grid —
 * 24×24, `currentColor`, 2px strokes where it strokes — so that a row of them sits evenly beside
 * the rest of the set. A division that wants the official logotypes should put them in the media
 * library and link them; this is what a 16 pixel square in a footer can honestly be.
 *
 * They take the same props as a lucide icon, because everything that draws one — the footer, the
 * property form of a block, the allow list — treats the whole set alike.
 */

type MarkProps = ComponentProps<'svg'>;

function Mark({ children, ...props }: MarkProps) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      viewBox="0 0 24 24"
      width={24}
      height={24}
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      {children}
    </svg>
  );
}

/** Two crossing strokes: the mark X uses, and the one shape of the five that is exactly itself. */
export function XMark(props: MarkProps) {
  return (
    <Mark {...props}>
      <path d="M4 4 L20 20" />
      <path d="M20 4 L4 20" />
    </Mark>
  );
}

/** A rounded square, a circle inside it, and the dot in the corner. */
export function InstagramMark(props: MarkProps) {
  return (
    <Mark {...props}>
      <rect x="3" y="3" width="18" height="18" rx="5" />
      <circle cx="12" cy="12" r="4" />
      <circle cx="17" cy="7" r="1" fill="currentColor" stroke="none" />
    </Mark>
  );
}

/** The rounded plate and the play mark in the middle of it. */
export function YoutubeMark(props: MarkProps) {
  return (
    <Mark {...props}>
      <rect x="2" y="5" width="20" height="14" rx="4" />
      <path d="M10 9.5 L15 12 L10 14.5 Z" fill="currentColor" />
    </Mark>
  );
}

/** The circle and the letter inside it. */
export function FacebookMark(props: MarkProps) {
  return (
    <Mark {...props}>
      <circle cx="12" cy="12" r="9" />
      <path d="M14.5 8h-1.2a1.8 1.8 0 0 0-1.8 1.8V12" />
      <path d="M10 12h4" />
      <path d="M13.5 12v5" />
    </Mark>
  );
}

/** The wide face with its two eyes, which is what the mark reads as at the size a footer uses. */
export function DiscordMark(props: MarkProps) {
  return (
    <Mark {...props}>
      <path d="M8 6.5a13 13 0 0 1 8 0" />
      <path d="M8 17.5a13 13 0 0 0 8 0" />
      <path d="M8 6.5C5.5 7.5 4 10 4 13c0 2 .6 3.4 1.4 4.3.5.6 1.3.4 1.6-.3l.6-1.4" />
      <path d="M16 6.5c2.5 1 4 3.5 4 6.5 0 2-.6 3.4-1.4 4.3-.5.6-1.3.4-1.6-.3l-.6-1.4" />
      <circle cx="9.5" cy="12.5" r="1.2" fill="currentColor" stroke="none" />
      <circle cx="14.5" cy="12.5" r="1.2" fill="currentColor" stroke="none" />
    </Mark>
  );
}
