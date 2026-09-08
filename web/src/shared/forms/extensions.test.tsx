import { queryOptions } from '@tanstack/react-query';
import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';
import { z } from 'zod';

import { createTestI18n, renderWithProviders } from '../../test/harness';
import type { MediaLibraryQuery, PickableMedia } from '../ui';

import { SchemaForm } from './SchemaForm';
import { localized, localizedObject } from './schema';

/**
 * The five things the generator learned in G2, one test each (implementation plan M1, G2), the
 * sixth, `multi`, which G11a added for `allowedBlocks` (decision note
 * `2026-09-07-scrivere-un-template.md`), and the seventh, `slugFrom`, which Carmine asked for after
 * running the demo of M1.
 *
 * They are all the same argument in seven shapes: a coordinator never writes an identifier, never
 * types an icon name, never converts a time zone in their head, never edits JSON, never has to
 * delete and re-add three cards to put one of them first, never adds five rows of a list to tick
 * five boxes, and never copies a title into an address by hand. Every one of those is a form
 * somebody would otherwise have written by hand.
 */

const LOCALES = ['en', 'it'] as const;

const DIVISION = { defaultLocale: 'en', timezone: 'Europe/Rome' };

const PICTURE: PickableMedia = {
  id: 42,
  fileName: 'hangar.png',
  contentType: 'image/png',
  alt: { en: 'An open hangar', it: 'Un hangar aperto' },
  url: '/media/42/hangar.png',
};

const library: MediaLibraryQuery = queryOptions({
  queryKey: ['extensions-test', 'library'] as readonly unknown[],
  queryFn: () => Promise.resolve({ items: [PICTURE], total: 1 }),
});

function render<TValues extends Record<string, unknown>>(
  schema: z.ZodType<TValues, TValues>,
  defaults: TValues,
  extras: {
    labels?: Record<string, unknown>;
    onSubmit?: (values: TValues) => Promise<unknown>;
    mediaLibrary?: MediaLibraryQuery;
    division?: { defaultLocale: string; timezone: string };
  } = {},
) {
  return renderWithProviders(
    <SchemaForm
      schema={schema}
      defaults={defaults}
      locales={LOCALES}
      labels="test"
      submitLabel="Save"
      onSubmit={extras.onSubmit ?? (() => Promise.resolve())}
      {...(extras.mediaLibrary === undefined ? {} : { mediaLibrary: extras.mediaLibrary })}
      {...(extras.division === undefined ? {} : { division: extras.division })}
    />,
    { i18n: createTestI18n({ test: extras.labels ?? {} }) },
  );
}

// ---- 1. a media is chosen, never typed -------------------------------------------------------

const mediaSchema = z.object({ picture: z.number().optional().meta({ media: true }) });
const mediaLabels = { fields: { picture: 'Picture' } };

test('a media field is the library, and never a number to type', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(
    mediaSchema,
    { picture: undefined },
    { labels: mediaLabels, mediaLibrary: library, division: DIVISION, onSubmit },
  );

  // Nothing to type: no number box anywhere on the form. That is the whole point of the field —
  // a free identifier produces pages pointing at files deleted years ago.
  expect(screen.queryByRole('spinbutton')).toBeNull();

  await userEvent.click(await screen.findByRole('button', { name: /hangar\.png/ }));
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));

  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ picture: 42 }));
});

test('a media field says what it is missing rather than drawing a picker that cannot work', () => {
  // The same discipline as an unknown type: the generator refuses out loud. A screen that forgot
  // to hand over the library would otherwise render an empty box and look like a bug in the data.
  expect(() => render(mediaSchema, { picture: undefined }, { labels: mediaLabels })).toThrow(/mediaLibrary/);
});

// ---- 2. an icon comes from the allow list ----------------------------------------------------

test('an icon field offers the allow list as pictures, and holds the name that was chosen', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(
    z.object({ icon: z.string().optional().meta({ icon: true }) }),
    { icon: undefined },
    {
      labels: { fields: { icon: 'Icon' }, options: { icon: { none: 'No icon' } } },
      onSubmit,
    },
  );

  const group = screen.getByRole('radiogroup', { name: 'Icon' });

  // A closed set, drawn: no text box, and every option carries its own picture rather than only
  // its name — which is why it is a grid and not a select (Atmosphere's takes a string per option).
  expect(within(group).queryByRole('textbox')).toBeNull();
  expect(within(group).getByRole('radio', { name: 'plane' })).toBeInTheDocument();
  expect(within(group).queryByRole('radio', { name: 'definitely-not-an-icon' })).toBeNull();

  await userEvent.click(within(group).getByRole('radio', { name: 'plane' }));
  expect(within(group).getByRole('radio', { name: 'plane' })).toHaveAttribute('aria-checked', 'true');

  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ icon: 'plane' }));
});

// ---- 3. a day and an instant, both held in UTC -----------------------------------------------

test('a day is a date input, and what it hands the server is an instant in UTC', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(
    z.object({ expiresAt: z.string().optional().meta({ date: true }) }),
    { expiresAt: undefined },
    { labels: { fields: { expiresAt: 'Expires' } }, division: DIVISION, onSubmit },
  );

  const input = screen.getByLabelText('Expires');
  expect(input).toHaveAttribute('type', 'date');

  await userEvent.type(input, '2026-12-31');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));

  // Midnight, and said in UTC: the column is a `DateTime`, and a date with no zone is a date that
  // means a different instant depending on who saved it.
  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ expiresAt: '2026-12-31T00:00:00Z' }));
});

test('an instant shows the time of the division under the UTC one, and stays UTC', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(
    z.object({ startsAt: z.string().optional().meta({ datetime: true }) }),
    // 18:00 UTC in January is 19:00 in Rome: a value whose two readings differ, so that a test
    // that quietly showed the browser's own zone could not pass.
    { startsAt: '2026-01-15T18:00:00Z' },
    { labels: { fields: { startsAt: 'Starts' } }, division: DIVISION, onSubmit },
  );

  const input = screen.getByLabelText('Starts');
  expect(input).toHaveAttribute('type', 'datetime-local');
  expect(input).toHaveValue('2026-01-15T18:00');

  // The input carries the UTC wall clock; underneath it the same instant where the division lives,
  // named. 18:00 UTC is 7 in the evening in Rome, so a line that quietly showed UTC twice — or the
  // browser's own zone, which in CI is UTC — cannot pass this.
  const local = screen.getByText(/Europe\/Rome/);
  expect(local).toHaveTextContent('7:00');
  expect(local).not.toHaveTextContent('6:00');

  // Typed rather than clicked, and with `fireEvent` rather than `userEvent`: a `datetime-local`
  // input is a row of segments a browser drives, and jsdom does not implement them — typing into
  // one leaves it half filled. What is under test is the conversion, not jsdom's date widget.
  fireEvent.change(input, { target: { value: '2026-02-20T07:30' } });
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));

  expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ startsAt: '2026-02-20T07:30:00Z' }));
});

test('an instant with nowhere to show local time says so rather than showing UTC alone', () => {
  expect(() =>
    render(
      z.object({ startsAt: z.string().optional().meta({ datetime: true }) }),
      { startsAt: undefined },
      { labels: { fields: { startsAt: 'Starts' } } },
    ),
  ).toThrow(/timezone/);
});

// ---- 4. a translated object is fields, not JSON ----------------------------------------------

const seoSchema = z.object({
  seo: localizedObject({
    title: z.string().optional(),
    ogImageMediaId: z.number().optional().meta({ media: true }),
  }).optional(),
});

/** What the form hands back for a translated object, named so the assertions can read it. */
type SeoValues = z.output<typeof seoSchema>;

test('a translated object is one tab per language, with real fields inside', async () => {
  const onSubmit = vi.fn((values: SeoValues) => Promise.resolve(values));
  render(
    seoSchema,
    { seo: { en: { title: '' }, it: { title: '' } } },
    {
      labels: {
        fields: { seo: 'Sharing', 'seo.title': 'Title', 'seo.ogImageMediaId': 'Picture' },
      },
      mediaLibrary: library,
      division: DIVISION,
      onSubmit,
    },
  );

  // Not a JSON box: a coordinator does not write JSON, which is the reason this kind exists.
  expect(screen.queryByRole('textbox', { name: 'Sharing' })).toBeNull();

  expect(screen.getByRole('tab', { name: /English/ })).toBeInTheDocument();
  expect(screen.getByRole('tab', { name: /Italian/ })).toBeInTheDocument();

  // And what is inside a tab is the generator again, all the way down: the picture is the media
  // picker, not a number.
  expect(await screen.findByRole('button', { name: /hangar\.png/ })).toBeInTheDocument();

  await userEvent.type(screen.getByLabelText('Title'), 'A hangar');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));

  // Written into the language on screen and nowhere else: the tabs are one value, not two fields.
  expect(onSubmit).toHaveBeenCalledTimes(1);
  const [written] = onSubmit.mock.calls[0] ?? [{}];
  expect(written?.seo?.en?.title).toBe('A hangar');
  expect(written?.seo?.it?.title).toBe('');
});

test('a language of a translated object is marked empty until something is in it', () => {
  render(
    seoSchema,
    { seo: { en: { title: 'Written' }, it: { title: '' } } },
    {
      labels: { fields: { seo: 'Sharing', 'seo.title': 'Title', 'seo.ogImageMediaId': 'Picture' } },
      mediaLibrary: library,
      division: DIVISION,
    },
  );

  // The same badge a translated string carries, asked of an object: the English holds something,
  // the Italian does not.
  expect(screen.getByRole('tab', { name: /English/ })).not.toHaveTextContent('Empty');
  expect(screen.getByRole('tab', { name: /Italian/ })).toHaveTextContent('Empty');
});

// ---- 5. a list can be reordered --------------------------------------------------------------

const listSchema = z.object({ cards: z.array(z.object({ name: z.string() })) });
const listLabels = { fields: { cards: 'Cards', 'cards.name': 'Name' } };

test('an entry of a list moves up and down, from the keyboard', async () => {
  const onSubmit = vi.fn(() => Promise.resolve());
  render(listSchema, { cards: [{ name: 'first' }, { name: 'second' }] }, { labels: listLabels, onSubmit });

  const before = screen.getAllByLabelText('Name');
  expect(before.map((input) => (input as HTMLInputElement).value)).toEqual(['first', 'second']);

  // Buttons rather than dragging, and buttons is what a keyboard reaches: `Tab` to it and press it.
  // Dragging arrives in G11 and does not replace this.
  const down = screen.getAllByRole('button', { name: 'Move down' })[0]!;
  down.focus();
  await userEvent.keyboard('{Enter}');

  const after = screen.getAllByLabelText('Name');
  expect(after.map((input) => (input as HTMLInputElement).value)).toEqual(['second', 'first']);

  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  expect(onSubmit).toHaveBeenCalledWith(
    expect.objectContaining({ cards: [{ name: 'second' }, { name: 'first' }] }),
  );
});

test('a new entry of a list arrives with fields somebody can type into', async () => {
  // ⚠️ G3 is where this started mattering: eleven of the sixteen blocks carry a list of objects,
  // and an entry appended as `{}` gives React inputs with no value — a translated field then
  // forgets what was typed into it, and the entry submits as undefined.
  const translated = z.object({ cards: z.array(z.object({ title: localized() })) });
  const onSubmit = vi.fn(() => Promise.resolve());

  render(
    translated,
    { cards: [] },
    { labels: { fields: { cards: 'Cards', 'cards.title': 'Title' } }, onSubmit },
  );

  await userEvent.click(screen.getByRole('button', { name: 'Add' }));

  // The English tab of the new entry. It has a value — the empty string — which is what makes it a
  // field React controls; appended as `{}` it would be undefined, and what is typed into it would
  // not survive the next render.
  const english = screen.getAllByRole('textbox')[0]!;
  expect(english).toHaveValue('');

  await userEvent.type(english, 'A card');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));

  expect(onSubmit).toHaveBeenCalledWith({ cards: [{ title: { en: 'A card', it: '' } }] });
});

test('the ends of a list have nowhere to go, and say so', () => {
  render(listSchema, { cards: [{ name: 'first' }, { name: 'second' }] }, { labels: listLabels });

  expect(screen.getAllByRole('button', { name: 'Move up' })[0]).toBeDisabled();
  expect(screen.getAllByRole('button', { name: 'Move down' })[1]).toBeDisabled();
});

// ---- 6. several out of a closed set is one click each ----------------------------------------

const multiSchema = z.object({
  allowed: z
    .array(z.string())
    .optional()
    .meta({
      multi: true,
      choices: [
        { value: 'text', label: 'Text' },
        { value: 'heading', label: 'Heading' },
        { value: 'gallery', label: 'Gallery' },
      ],
    }),
});

const multiLabels = { fields: { allowed: 'Blocks allowed here' } };

test('a closed set to pick several of is a checkbox each, not a list to fill in', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());

  render(multiSchema, { allowed: ['heading'] }, { labels: multiLabels, onSubmit });

  // What is already chosen is shown as chosen, which a list of selects could also do — and then
  // one click adds a second, where a list would be "add a row, open a select, find the value".
  expect(screen.getByRole('checkbox', { name: 'Heading' })).toBeChecked();
  expect(screen.getByRole('checkbox', { name: 'Text' })).not.toBeChecked();

  await user.click(screen.getByRole('checkbox', { name: 'Text' }));
  await user.click(screen.getByRole('button', { name: 'Save' }));

  // In the order the set declares, not the order they were ticked: what is stored is a set, and
  // two arrays holding the same values in a different order would read as a change nobody made.
  expect(onSubmit).toHaveBeenCalledWith({ allowed: ['text', 'heading'] });
});

test('unticking the last one leaves nothing rather than an empty box nobody meant', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());

  render(multiSchema, { allowed: ['heading'] }, { labels: multiLabels, onSubmit });

  await user.click(screen.getByRole('checkbox', { name: 'Heading' }));
  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(onSubmit).toHaveBeenCalledWith({ allowed: [] });
});

// ---- 7. an address proposes itself from the title --------------------------------------------

const slugSchema = z.object({ title: localized(), slug: z.string().meta({ slugFrom: 'title' }) });
const slugLabels = { fields: { title: 'Title', slug: 'Address' } };

/**
 * The title is drawn by `LocaleFields`, which is a tab per language and one box at a time, and the
 * box carries no label of its own — the group does. So the title is "the first text box on the
 * form", which is also the order the schema declares.
 */
function titleBox() {
  return screen.getAllByRole('textbox')[0]!;
}

function addressBox() {
  return screen.getByLabelText('Address');
}

test('a new row proposes its address from the title, accents folded', async () => {
  const user = userEvent.setup();

  render(slugSchema, { title: { en: '', it: '' }, slug: '' }, { labels: slugLabels, division: DIVISION });

  await user.type(titleBox(), 'Città di partenza!');

  // Lower case, accents folded rather than dropped, one dash per run of anything else, and no dash
  // hanging off either end. It is the shape `ContentWriteDtoValidator` holds a slug to.
  expect(addressBox()).toHaveValue('citta-di-partenza');
});

test('the proposal stops for good the moment somebody writes the address themselves', async () => {
  const user = userEvent.setup();

  render(slugSchema, { title: { en: '', it: '' }, slug: '' }, { labels: slugLabels, division: DIVISION });

  await user.type(titleBox(), 'First');
  await user.clear(addressBox());
  await user.type(addressBox(), 'chosen-by-hand');

  // The title keeps moving and the address does not follow it any more. Without this, an address
  // would rewrite itself under the person typing it.
  await user.type(titleBox(), ' and second');

  expect(addressBox()).toHaveValue('chosen-by-hand');
});

test('a row that already has an address never moves it, however its title is edited', async () => {
  const user = userEvent.setup();

  render(
    slugSchema,
    { title: { en: 'About us', it: 'Chi siamo' }, slug: 'about' },
    { labels: slugLabels, division: DIVISION },
  );

  await user.type(titleBox(), ' renamed');

  // ⚠️ The whole reason the rule is "follow what was proposed" and not "follow while empty": an
  // address outlives the page, and a published one that moved because somebody fixed a typo in its
  // title would break every link anybody had to it.
  expect(addressBox()).toHaveValue('about');
});

test('the proposal reads the default language of the division, and falls back to what is written', async () => {
  const user = userEvent.setup();

  render(slugSchema, { title: { en: '', it: '' }, slug: '' }, { labels: slugLabels, division: DIVISION });

  // Italian first: nothing is written in the language the address is published in, so a proposal
  // made from what there is beats no proposal at all.
  await user.click(screen.getByRole('tab', { name: /Italian/ }));
  await user.type(titleBox(), 'Chi siamo');
  expect(addressBox()).toHaveValue('chi-siamo');

  // And the moment the default language has something, that is what the address is made of.
  await user.click(screen.getByRole('tab', { name: /English/ }));
  await user.type(titleBox(), 'About us');
  expect(addressBox()).toHaveValue('about-us');
});

test('a field that is a path proposes one, slash and all', async () => {
  const user = userEvent.setup();

  const pathSchema = z.object({
    label: localized(),
    path: z.string().meta({ slugFrom: 'label', slugPrefix: '/' }),
  });

  render(
    pathSchema,
    { label: { en: '', it: '' }, path: '' },
    { labels: { fields: { label: 'Label', path: 'Address' } }, division: DIVISION },
  );

  // The menu writes `/chi-siamo` where a page writes `chi-siamo` (asked for while running the demo
  // of M1, part 1). Same annotation, one word more.
  await user.type(screen.getAllByRole('textbox')[0]!, 'About us');
  expect(screen.getByLabelText('Address')).toHaveValue('/about-us');
});

// ---- 8. a field that offers without demanding -------------------------------------------------

const suggestSchema = z.object({
  path: z.string().meta({
    suggestions: [
      { value: '/about', label: 'Chi siamo', group: 'Web' },
      { value: '/tours', label: 'I tour', group: 'Flight Ops' },
    ],
  }),
});

const suggestLabels = { fields: { path: 'Address' } };

test('a suggested field offers what exists, grouped, and takes what is typed anyway', async () => {
  const user = userEvent.setup();
  const onSubmit = vi.fn(() => Promise.resolve());

  render(suggestSchema, { path: '' }, { labels: suggestLabels, onSubmit });

  await user.click(screen.getByLabelText('Address'));

  // Grouped, because thirty addresses in one list is a wall: the heading is the department that
  // wrote the page.
  expect(await screen.findByText('Web')).toBeInTheDocument();
  expect(screen.getByText('Flight Ops')).toBeInTheDocument();

  // Choosing one writes the address and not the title: what a menu stores is where it leads.
  await user.click(screen.getByText('Chi siamo'));
  expect(screen.getByLabelText('Address')).toHaveValue('/about');

  // ⚠️ And it is not a select: an address of somewhere else is typed in full and kept, or a menu
  // could not link the forum.
  await user.clear(screen.getByLabelText('Address'));
  await user.type(screen.getByLabelText('Address'), 'https://forum.example.org');
  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(onSubmit).toHaveBeenCalledWith({ path: 'https://forum.example.org' });
});

test('the list narrows to what is being typed, on the address as well as on the title', async () => {
  const user = userEvent.setup();

  render(suggestSchema, { path: '' }, { labels: suggestLabels, division: DIVISION });

  await user.click(screen.getByLabelText('Address'));
  await user.type(screen.getByLabelText('Address'), 'tour');

  // Matched on the address, which is what somebody types when they half remember it. `cmdk` filters
  // on its own idea of the text, which is why the filtering here is ours.
  expect(await screen.findByText('I tour')).toBeInTheDocument();
  expect(screen.queryByText('Chi siamo')).not.toBeInTheDocument();
});

test('a closed list keeps only what it offered, and says so', async () => {
  const user = userEvent.setup();

  const closed = z.object({
    path: z.string().meta({
      suggestions: [{ value: '/about', label: 'Chi siamo', group: 'Web' }],
      suggestionsOnly: true,
    }),
  });

  render(closed, { path: '/about' }, { labels: suggestLabels, division: DIVISION });

  const field = screen.getByLabelText('Address');

  // ⚠️ The difference between offering and deciding. A menu entry leads to a page of this site, to
  // one of its screens or to a link of the library, so that every address leaving the site lives in
  // one table — and what is typed here is a way of searching that list, not a value.
  await user.clear(field);
  await user.type(field, 'https://somewhere.example');
  await user.tab();

  expect(field).toHaveValue('/about');
});

// ---- and the property none of the five may weaken --------------------------------------------

test('the generator still refuses a type it cannot draw', () => {
  // The reason nobody writes a form by hand is that skipping a field is impossible: the generator
  // throws instead of quietly leaving one out. Five new kinds must not have softened that.
  expect(() =>
    render(z.object({ when: z.date() }) as never, { when: new Date() } as never, {
      labels: { fields: { when: 'When' } },
    }),
  ).toThrow(/does not draw/);
});
