import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Vitest runs without globals here, so Testing Library cannot install its own hook: without this
// every render stays in the document and the next test finds two of everything.
afterEach(() => cleanup());

// jsdom declares these but does not implement them, and several Atmosphere components — anything
// built on a Radix popper: Select, Tabs, ScrollArea — measure or capture on mount. Stubs are
// enough: a test asserts on roles and text, never on a size or a pointer.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
};

// `ThemeProvider` asks the operating system whether it prefers dark, and jsdom has no
// `matchMedia` at all. Answering "no preference" is the right stub: a test that cares about a
// theme sets it explicitly, and one that does not should get the light one deterministically.
globalThis.matchMedia ??= ((query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: () => {},
  removeListener: () => {},
  addEventListener: () => {},
  removeEventListener: () => {},
  dispatchEvent: () => false,
})) as typeof globalThis.matchMedia;

Element.prototype.scrollIntoView = () => {};
Element.prototype.hasPointerCapture = () => false;
Element.prototype.setPointerCapture = () => {};
Element.prototype.releasePointerCapture = () => {};
