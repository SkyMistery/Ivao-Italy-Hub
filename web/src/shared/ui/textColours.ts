/**
 * The colours a word of prose may take (design of the editor, phase 3, §4.6, and Carmine's answer 3): a closed set, written
 * `:ocean[parola]` and measured at 4.5 : 1 on every ground in both themes. Not the accent of a block, which colours
 * graphics at 3 : 1. The class of each is in `styles/index.css`.
 */
export const TEXT_COLOURS = ['brand', 'ocean', 'aurora', 'danger'] as const;

export type TextColour = (typeof TEXT_COLOURS)[number];

export const textColourClass = (colour: TextColour) => `text-colour-${colour}`;

export const isTextColour = (name: string): name is TextColour =>
  (TEXT_COLOURS as readonly string[]).includes(name);
