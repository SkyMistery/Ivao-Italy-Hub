import type { Page } from '@playwright/test';

/**
 * How readable the text of a page is, measured in the browser rather than looked at.
 *
 * One function for every spec that asks, because the second one written from scratch got it wrong
 * within the hour: on 11 September 2026 a check of the header's drop down read an `oklab()` colour
 * with a regular expression, took its three numbers for red, green and blue, and reported white on
 * white as legible and dark on white as 1.3 : 1. The rule in the comment below had been in
 * `contrast.spec.ts` all along; it lives here now so that nobody has to rediscover it.
 */

export type Measured = { text: string; size: number; needs: number; measured: number };

/** The contrast of every element matching `selector` that has text and a box, against its ground. */
export async function measureContrast(page: Page, selector: string): Promise<Measured[]> {
  return page.evaluate((query) => {
    const canvas = document.createElement('canvas');
    canvas.width = 1;
    canvas.height = 1;
    const ctx = canvas.getContext('2d', { willReadFrequently: true })!;

    // Any colour — `rgb()`, `oklab()`, `color(...)` — rendered into the bytes the screen shows.
    // Reading the numbers out of the string with a regular expression is what a first version did,
    // twice, and an `oklab()` colour parsed that way comes out near black: a ratio invented, not
    // measured. A tuple and not `number[]`: with `noUncheckedIndexedAccess` an array index is
    // possibly undefined, and three channels are always three.
    type Rgb = [number, number, number];

    const paint = (colour: string, over?: Rgb): Rgb => {
      ctx.clearRect(0, 0, 1, 1);
      if (over) {
        ctx.fillStyle = `rgb(${over.join(',')})`;
        ctx.fillRect(0, 0, 1, 1);
      }
      ctx.fillStyle = colour;
      ctx.fillRect(0, 0, 1, 1);
      const pixel = ctx.getImageData(0, 0, 1, 1).data;
      return [pixel[0] ?? 0, pixel[1] ?? 0, pixel[2] ?? 0];
    };

    const channel = (value: number) => {
      const v = value / 255;
      return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
    };
    const luminance = (rgb: Rgb) =>
      0.2126 * channel(rgb[0]) + 0.7152 * channel(rgb[1]) + 0.0722 * channel(rgb[2]);
    const ratio = (a: Rgb, b: Rgb) => {
      const first = luminance(a);
      const second = luminance(b);
      return (Math.max(first, second) + 0.05) / (Math.min(first, second) + 0.05);
    };

    const opaque = (colour: string) =>
      colour !== '' && colour !== 'rgba(0, 0, 0, 0)' && colour !== 'transparent';

    // The real ground under an element: the chain of ancestors painted from the bottom up, so a
    // half transparent panel counts for what it shows and not for the colour it declares.
    const behind = (element: Element): Rgb => {
      const chain: string[] = [];
      for (let node: Element | null = element; node !== null; node = node.parentElement) {
        const background = getComputedStyle(node).backgroundColor;
        if (opaque(background)) {
          chain.unshift(background);
        }
      }

      let ground = paint(getComputedStyle(document.documentElement).backgroundColor || 'white');
      for (const colour of chain) {
        ground = paint(colour, ground);
      }

      return ground;
    };

    return [...document.querySelectorAll(query)]
      .filter((element) => (element.textContent ?? '').trim() !== '' && element.getClientRects().length > 0)
      .map((element) => {
        const style = getComputedStyle(element);
        const size = Number.parseFloat(style.fontSize);
        const bold = Number.parseInt(style.fontWeight, 10) >= 700;
        // WCAG calls text large at 24px, or 18.66px when bold; everything else needs 4.5 : 1.
        const large = size >= 24 || (bold && size >= 18.66);
        const ground = behind(element);

        return {
          text: (element.textContent ?? '').trim().slice(0, 40),
          size,
          needs: large ? 3 : 4.5,
          measured: Math.round(ratio(paint(style.color, ground), ground) * 100) / 100,
        };
      });
  }, selector);
}
