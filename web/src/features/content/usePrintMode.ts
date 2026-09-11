import { useEffect, useState } from 'react';
import { flushSync } from 'react-dom';

/**
 * Whether the page is being printed, for `PrintContext` (G14): true between the browser's
 * `beforeprint` and `afterprint`, whichever way the print was asked for — the button on the
 * footer, the menu of the browser, Ctrl+P.
 *
 * ⚠️ `flushSync` is not a habit: the browser lays the paper out **as soon as the listeners of
 * `beforeprint` return**, and a state update left to the ordinary batching would land on the page
 * one frame too late — after the tabs had already been photographed folded. The way back is
 * allowed to be lazy.
 */
export function usePrintMode(): boolean {
  const [printing, setPrinting] = useState(false);

  useEffect(() => {
    const before = () => {
      flushSync(() => setPrinting(true));
    };
    const after = () => setPrinting(false);

    window.addEventListener('beforeprint', before);
    window.addEventListener('afterprint', after);

    return () => {
      window.removeEventListener('beforeprint', before);
      window.removeEventListener('afterprint', after);
    };
  }, []);

  return printing;
}
