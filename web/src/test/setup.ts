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

Element.prototype.scrollIntoView = () => {};
Element.prototype.hasPointerCapture = () => false;
Element.prototype.setPointerCapture = () => {};
Element.prototype.releasePointerCapture = () => {};
