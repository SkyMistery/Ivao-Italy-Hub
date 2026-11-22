import { describe, expect, it } from 'vitest';

import { contentEditorSearchSchema, contentReturnHref, contentReturnSearch } from './contentSearch';

describe('where the form of a row goes back to (review of #550, item 1)', () => {
  it('goes back to the tree of the site when it was opened from the tree, naming no department', () => {
    const back = contentReturnSearch('Page', 'ED', 'tree');

    expect(back).toEqual({ kind: 'Page', view: 'tree' });
    expect(contentReturnHref(back)).toBe('/staff/content?kind=Page&view=tree');
  });

  it('goes back to the list of its kind in its department when it was opened from anywhere else', () => {
    expect(contentReturnSearch('Page', 'ED', undefined)).toEqual({ kind: 'Page', department: 'ED' });
    expect(contentReturnSearch('Page', 'ED', 'list')).toEqual({ kind: 'Page', department: 'ED' });
    expect(contentReturnHref(contentReturnSearch('News', 'FOD', undefined))).toBe(
      '/staff/content?kind=News&department=FOD',
    );
  });

  it('has no tree to go back to for what is not a page', () => {
    expect(contentReturnSearch('Template', 'ED', 'tree')).toEqual({ kind: 'Template', department: 'ED' });
    expect(contentReturnSearch('Document', 'ED', 'tree')).toEqual({ kind: 'Document', department: 'ED' });
  });

  it('reads a view it does not know as none', () => {
    expect(contentEditorSearchSchema.parse({ view: 'map' }).view).toBeUndefined();
    expect(contentEditorSearchSchema.parse({ view: 'tree' }).view).toBe('tree');
  });
});
