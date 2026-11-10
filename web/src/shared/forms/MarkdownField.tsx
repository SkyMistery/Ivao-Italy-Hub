import { Button, Label, PopoverContent, PopoverRoot, PopoverTrigger, Textarea } from '@ivao/atmosphere-react';
import {
  Bold,
  IndentDecrease,
  IndentIncrease,
  Italic,
  Link as LinkIcon,
  List,
  ListOrdered,
  Palette,
  Strikethrough,
} from 'lucide-react';
import { useRef, useState, type KeyboardEvent, type ReactNode, type RefObject } from 'react';
import { useFormContext } from 'react-hook-form';
import { useTranslation } from 'react-i18next';

import { TEXT_COLOURS, textColourClass } from '../ui/textColours';

import {
  linkAddressAt,
  moveListLines,
  toggleList,
  toggleMark,
  writeColour,
  writeLink,
  type InlineMark,
  type TextState,
} from './markdownEdits';
import type { Suggestion } from './schema';
import { Suggest } from './Suggest';

const MARK_KEYS: Readonly<Record<string, InlineMark>> = { b: '**', i: '*' };

/**
 * ⚠️ A key pressed in one of the bar's two lists belongs to the list. The editor listens on the document for what acts on the
 * block picked, outside a field (`useSelectionShortcuts`), and a colour is a button: Escape closed the list and let go of the
 * block too, and Delete would have removed it. Radix hears Escape first, on the way down, so the list still closes.
 */
const keepKeysInside = (event: KeyboardEvent) => event.stopPropagation();

/**
 * Where the focus goes when one of the two lists closes: back in the box, unless it has already gone somewhere. ⚠️ The list
 * finishes closing after its animation, so going from the colours straight to the link, the box took the focus from the link
 * that had just opened, and that closed it.
 */
function backToTheBox(event: Event, box: HTMLTextAreaElement | null) {
  event.preventDefault();

  if (document.activeElement === null || document.activeElement === document.body) {
    box?.focus();
  }
}

/**
 * The box of a text the renderer draws with `MarkdownContent`, with the bar that writes the marks (design of the editor, phase
 * 3, §4.6). What is typed stays what is stored: the bar only writes Markdown around the selection, and whoever writes keeps
 * seeing the signs. A piece of the generated form and not a component of the closed list, drawn only by `LocaleFields`.
 */
export function MarkdownField({
  name,
  addresses,
}: {
  name: string;
  /** What the link offers (`suggestAddresses`); without it an address is written in full. */
  addresses: readonly Suggestion[] | undefined;
}) {
  const { register, setValue } = useFormContext();
  const box = useRef<HTMLTextAreaElement | null>(null);
  const { ref, ...field } = register(name);

  const selection = (): TextState | null =>
    box.current === null
      ? null
      : {
          value: box.current.value,
          start: box.current.selectionStart,
          end: box.current.selectionEnd,
        };

  const apply = (edit: (state: TextState) => TextState) => {
    const state = selection();
    if (state === null) {
      return;
    }

    const next = edit(state);
    setValue(name, next.value, { shouldDirty: true, shouldValidate: true });
    box.current?.focus();
    box.current?.setSelectionRange(next.start, next.end);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    const mark = MARK_KEYS[event.key.toLowerCase()];
    if ((event.ctrlKey || event.metaKey) && !event.shiftKey && !event.altKey && mark !== undefined) {
      event.preventDefault();
      apply((state) => toggleMark(state, mark));
    }
  };

  return (
    <div className="flex flex-col gap-1">
      <FormatBar box={box} apply={apply} selection={selection} addresses={addresses ?? []} />
      <Textarea
        id={name}
        rows={6}
        {...field}
        ref={(element) => {
          ref(element);
          box.current = element;
        }}
        onKeyDown={onKeyDown}
      />
    </div>
  );
}

function FormatBar({
  box,
  apply,
  selection,
  addresses,
}: {
  box: RefObject<HTMLTextAreaElement | null>;
  apply: (edit: (state: TextState) => TextState) => void;
  selection: () => TextState | null;
  addresses: readonly Suggestion[];
}) {
  const { t } = useTranslation();

  const button = (key: string, icon: ReactNode, edit: (state: TextState) => TextState) => (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className="size-8"
      aria-label={t(`form.markdown.${key}`)}
      title={t(`form.markdown.${key}`)}
      onClick={() => apply(edit)}
    >
      {icon}
    </Button>
  );

  return (
    <div role="toolbar" aria-label={t('form.markdown.bar')} className="flex flex-wrap items-center gap-0.5">
      {button('bold', <Bold aria-hidden className="size-4" />, (state) => toggleMark(state, '**'))}
      {button('italic', <Italic aria-hidden className="size-4" />, (state) => toggleMark(state, '*'))}
      {button('strike', <Strikethrough aria-hidden className="size-4" />, (state) => toggleMark(state, '~~'))}
      {button('bullets', <List aria-hidden className="size-4" />, (state) => toggleList(state, 'bullets'))}
      {button('numbers', <ListOrdered aria-hidden className="size-4" />, (state) =>
        toggleList(state, 'numbers'),
      )}
      {button('indent', <IndentIncrease aria-hidden className="size-4" />, (state) =>
        moveListLines(state, 'in'),
      )}
      {button('outdent', <IndentDecrease aria-hidden className="size-4" />, (state) =>
        moveListLines(state, 'out'),
      )}
      <LinkChoice box={box} apply={apply} selection={selection} addresses={addresses} />
      <ColourChoice box={box} apply={apply} />
    </div>
  );
}

/**
 * The address of a link: a page of the site from the list, or one of somewhere else written in full, with the same field a
 * block's button has (§4.5).
 */
function LinkChoice({
  box,
  apply,
  selection,
  addresses,
}: {
  box: RefObject<HTMLTextAreaElement | null>;
  apply: (edit: (state: TextState) => TextState) => void;
  selection: () => TextState | null;
  addresses: readonly Suggestion[];
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [address, setAddress] = useState('');

  const write = () => {
    const label = addresses.find((suggestion) => suggestion.value === address.trim())?.label;
    apply((state) => writeLink(state, address, label));
    setOpen(false);
  };

  return (
    <PopoverRoot
      open={open}
      onOpenChange={(next) => {
        if (next) {
          const state = selection();
          setAddress(state === null ? '' : linkAddressAt(state));
        }
        setOpen(next);
      }}
    >
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8"
          aria-label={t('form.markdown.link')}
          title={t('form.markdown.link')}
        >
          <LinkIcon aria-hidden className="size-4" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="flex w-[28rem] max-w-[90vw] flex-col gap-2"
        onCloseAutoFocus={(event) => backToTheBox(event, box.current)}
        onKeyDown={(event) => {
          keepKeysInside(event);

          // Enter that chose an option of the list is taken: this is the Enter of an address written in full.
          if (event.key === 'Enter' && !event.defaultPrevented && !event.nativeEvent.isComposing) {
            event.preventDefault();
            write();
          }
        }}
      >
        <Label htmlFor="markdown-link-address">{t('form.markdown.linkAddress')}</Label>
        {/* Beside the box, because the list of the addresses opens under it and would cover a button below. */}
        <div className="flex items-center gap-2">
          <div className="min-w-0 flex-1">
            <Suggest
              id="markdown-link-address"
              value={address}
              suggestions={addresses}
              only={false}
              empty={t('form.suggest.empty')}
              onChange={setAddress}
            />
          </div>
          <Button type="button" size="sm" className="shrink-0" onClick={write}>
            {t('form.markdown.linkApply')}
          </Button>
        </div>
      </PopoverContent>
    </PopoverRoot>
  );
}

/** The closed set of colours, each drawn in itself; there is no other way of writing one but by hand. */
function ColourChoice({
  box,
  apply,
}: {
  box: RefObject<HTMLTextAreaElement | null>;
  apply: (edit: (state: TextState) => TextState) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <PopoverRoot open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="size-8"
          aria-label={t('form.markdown.colour')}
          title={t('form.markdown.colour')}
        >
          <Palette aria-hidden className="size-4" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        className="flex w-48 flex-col gap-1 p-2"
        onKeyDown={keepKeysInside}
        onCloseAutoFocus={(event) => backToTheBox(event, box.current)}
      >
        {TEXT_COLOURS.map((colour) => (
          <Button
            key={colour}
            type="button"
            variant="ghost"
            size="sm"
            className="justify-start"
            onClick={() => {
              apply((state) => writeColour(state, colour));
              setOpen(false);
            }}
          >
            <span className={`${textColourClass(colour)} font-semibold`}>
              {t(`form.markdown.colours.${colour}`)}
            </span>
          </Button>
        ))}
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="justify-start"
          onClick={() => {
            apply((state) => writeColour(state, null));
            setOpen(false);
          }}
        >
          {t('form.markdown.colours.none')}
        </Button>
      </PopoverContent>
    </PopoverRoot>
  );
}
