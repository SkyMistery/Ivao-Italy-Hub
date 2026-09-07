import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { Body } from '../../blocks';
import { renderWithProviders } from '../../test/harness';

import { LockedByTemplate, TemplateDifferences } from './TemplatePanel';
import type { TemplateDifference } from './templateDiff';

/**
 * What the editor says about a template that has moved on (design M1 §9.1).
 *
 * `templateDiff` is tested next door and answers *what* changed. What is asserted here is the half a
 * pure function cannot hold: that the panel offers an action for the two differences it can apply
 * and **no action at all** for the one it must not, and that a locked section says which template
 * fixed it and who may change that — because a control that refuses without a reason is how a
 * coordinator ends up writing a ticket.
 */

const words = englishCommon.content.editor.template;

const page: Body = {
  schemaVersion: 1,
  sections: [
    {
      id: 's_archive',
      key: 'archive',
      title: { en: 'Old news' },
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [],
      sections: [],
    },
  ],
};

const differences: TemplateDifference[] = [
  {
    kind: 'added',
    key: 'contacts',
    section: {
      id: 's_contacts',
      key: 'contacts',
      title: { en: 'Get in touch' },
      layout: 'stacked',
      background: 'none',
      padding: 'md',
      width: 'default',
      blocks: [],
      sections: [],
    },
  },
  { kind: 'removed', key: 'archive', id: 's_archive' },
  { kind: 'changed', key: 'hero', id: 's_hero', reasons: ['blocks', 'structure'] },
];

test('names each section the way the page does, and offers one action per difference', async () => {
  const align = vi.fn();
  const user = userEvent.setup();

  renderWithProviders(<TemplateDifferences body={page} differences={differences} onAlign={align} />);

  // The title on screen, not the key the two sides are matched by: `archive` is called "Old news"
  // to whoever wrote it.
  expect(screen.getByText(words.removed.replace('{{section}}', 'Old news'))).toBeInTheDocument();
  expect(screen.getByText(words.added.replace('{{section}}', 'Get in touch'))).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: words.apply.added }));
  expect(align).toHaveBeenCalledWith(differences[0]);
});

test("the difference that would delete somebody's work has no button at all", () => {
  renderWithProviders(<TemplateDifferences body={page} differences={differences} onAlign={vi.fn()} />);

  // Two differences, two buttons. Aligning the third would mean throwing away blocks a person
  // wrote, so it is said and left alone — and a *disabled* third button would be the same trap
  // with a friendlier face.
  expect(screen.getAllByRole('button')).toHaveLength(2);
  expect(screen.getByText(words.yoursToDecide)).toBeInTheDocument();

  // And it says why, both reasons of it.
  expect(screen.getByText(new RegExp(words.reasons.blocks, 'u'))).toBeInTheDocument();
  expect(screen.getByText(new RegExp(words.reasons.structure, 'u'))).toBeInTheDocument();
});

test('nothing is drawn when the page and its template still agree', () => {
  const { container } = renderWithProviders(
    <TemplateDifferences body={page} differences={[]} onAlign={vi.fn()} />,
  );

  expect(container).toBeEmptyDOMElement();
});

test('a locked section names the template and who may change it', () => {
  renderWithProviders(
    <LockedByTemplate template={{ title: { en: 'Section page' }, department: 'WD' }} canManage={false} />,
  );

  expect(screen.getByText(new RegExp('Section page', 'u'))).toBeInTheDocument();

  // The permission by name and the department it is held on, because a permission held everywhere
  // is not what this is: templates belong to a department (design M1 §9.4).
  expect(screen.getByText(/Content\.ManageTemplates/u)).toBeInTheDocument();
  expect(screen.getByText(/WD/u)).toBeInTheDocument();
});

test('somebody who may change it is told so instead of being told who can', () => {
  renderWithProviders(
    <LockedByTemplate template={{ title: { en: 'Section page' }, department: 'WD' }} canManage />,
  );

  expect(
    screen.getByText(new RegExp(words.lockedYouMay.replace('{{department}}', 'WD'), 'u')),
  ).toBeInTheDocument();
  expect(screen.queryByText(/Content\.ManageTemplates/u)).not.toBeInTheDocument();
});
