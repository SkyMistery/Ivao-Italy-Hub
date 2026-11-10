import {
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandList,
  CommandRoot,
  Input,
  PopoverAnchor,
  PopoverContent,
  PopoverRoot,
} from '@ivao/atmosphere-react';
import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react';

import { fold } from '../search/highlight';

import type { Suggestion } from './schema';

/**
 * The `value` of `cmdk`'s root that no option carries: "nothing is lit". An empty string would not
 * do — `cmdk` reads it as "light the first option" — and no address, callsign or aircraft type holds
 * a NUL.
 */
const NOTHING_LIT = '\u0000';

/**
 * A field that **offers** without demanding: the address of a menu entry is the case it was built
 * for — the pages of the site, grouped by the department that wrote them, and an address of
 * somewhere else typed in full.
 *
 * ⚠️ It is not a select, and the difference is the whole point (asked for while running the demo of
 * M1, part 1). A select refuses everything it does not list, and a menu that could only point at a
 * page of this site could not link the forum. What is typed **is** the value; the list is a way of
 * not typing it.
 *
 * The list filters itself against what is in the box, folded the way the search folds — so "citta"
 * finds "Città" here too — and it is `shouldFilter={false}` because that filtering is ours: `cmdk`
 * would match on its own idea of the text and throw away a page whose address matches while its
 * title does not.
 *
 * ⚠️ The box drives the list from the keyboard (28 September 2026, note
 * `2026-09-28-il-suggerimento-dalla-tastiera`). It sits outside `cmdk`'s root — the root is inside
 * the popover, the box is its anchor — so `cmdk` never heard a key typed in it: the arrows did not
 * move through the options, Enter did not choose one, and Enter in the only line of a form with no
 * button sent the form. The box now hands the arrows and Enter to the root, and `cmdk` moves and
 * chooses as it does for its own input; which option is lit is held here (`value` on the root), so
 * the box can say it to a screen reader and decide what Enter means.
 */
export function Suggest({
  id,
  value,
  suggestions,
  only,
  empty,
  onChange,
  onSearch,
}: {
  id: string;
  value: string;
  suggestions: readonly Suggestion[];
  /** True when the list is the whole of what the field accepts, and not only what it offers. */
  only: boolean;
  empty: string;
  onChange: (next: string) => void;
  /** Told what is being typed, after a pause, so the screen can go and ask for more. */
  onSearch?: ((typed: string) => void) | undefined;
}) {
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLInputElement>(null);

  // ⚠️ The box and its list are **one field** (A6c, 26 September 2026). Pressing the list — an
  // option, a heading, its scrollbar — moves the focus into it, and that is not leaving the field.
  // It counted as leaving: a closed field with part of a value typed put back what it held (the
  // rule below), the list grew back to every row under the pointer, and the click landed on
  // another row, so the option pressed was never chosen. Measured in a browser; in jsdom a click
  // reaches its element whatever has moved under it. The list is allowed the focus rather than
  // denied it: cancelling the press would keep the box focused, and would also stop a scrollbar
  // from dragging — measured too.
  const list = useRef<HTMLDivElement>(null);
  const inList = (node: EventTarget | null) => node instanceof Node && list.current?.contains(node) === true;

  // ⚠️ What narrows the list is what somebody has typed **since it opened**, not what the field
  // happened to hold. Opening the address of an entry that already has one would otherwise answer
  // "nothing matches": the value is a whole address, and it matches nothing but itself.
  const [opened, setOpened] = useState(value);
  const typed = value === opened ? '' : fold(value.trim());
  const matching = suggestions.filter(
    (suggestion) =>
      typed === '' || fold(suggestion.value).includes(typed) || fold(suggestion.label).includes(typed),
  );

  // ⚠️ And the same text goes **to the caller**, after a pause, so that a list which is only ever a
  // page of rows can be a different page. Three hundred milliseconds, like the search box of a list
  // — the same pause, because it is the same gesture. Only while the box is open: choosing an entry
  // writes a whole address into the field, and asking the server about it would be a request for
  // something already chosen.
  //
  // What was asked last, and which options were on the screen when it was: while they are still the
  // same ones, the answer has not arrived.
  const asked = useRef<{ text: string; options: string } | null>(null);
  const options = suggestions.map((suggestion) => suggestion.value).join('\n');
  const optionsNow = useRef(options);
  useEffect(() => {
    optionsNow.current = options;
  }, [options]);
  const ask = useCallback(
    (text: string) => {
      asked.current = { text, options: optionsNow.current };
      onSearch?.(text);
    },
    [onSearch],
  );

  useEffect(() => {
    if (onSearch === undefined || !open) {
      return undefined;
    }

    const timer = setTimeout(() => ask(typed), 300);
    return () => clearTimeout(timer);
  }, [typed, open, onSearch, ask]);

  // Kept in the order the caller gave them, grouped by the heading each carries: a `Map` because
  // insertion order is the order the groups are drawn in, and the caller decided it.
  const groups = new Map<string, Suggestion[]>();
  for (const suggestion of matching) {
    const heading = suggestion.group ?? '';
    groups.set(heading, [...(groups.get(heading) ?? []), suggestion]);
  }

  // A closed field keeps only what was offered. What is typed is a way of searching the list, so
  // leaving the field with something nobody offered puts back what was there — and the server
  // refuses that value anyway, which is what makes this a rule.
  //
  // ⚠️ Offered is read the way the list is searched, folded (#366): `a320` typed in full is the
  // `A320` the list shows and lights, and leaving the field writes the option as it is offered. It
  // was compared exactly, and the field put back what it held under the option it had just lit.
  const held = fold(value.trim());
  const match =
    suggestions.find((suggestion) => suggestion.value === value) ??
    (held === '' ? undefined : suggestions.find((suggestion) => fold(suggestion.value) === held));
  const offered = match !== undefined;

  // ⚠️ And a field left before the answer to what was typed arrived waits for it (#366): the
  // question goes three hundred milliseconds after the last key, and the options on the screen
  // until the answer are those of the question before — the first page of every type, where `A320`
  // is not. Tab straight after typing put back a value the answer would have offered. So leaving
  // asks at once, if it was not asked yet, and the field decides when the options change; what it
  // holds meanwhile is what was typed, and the server still refuses it if nothing offers it.
  //
  // ⚠️ Not forever: an answer may be the very options already on the screen, and nothing changes
  // then. After two seconds the field decides with what it has, as it did before it waited.
  const [waiting, setWaiting] = useState<string | null>(null);
  const [gaveUp, setGaveUp] = useState(false);
  const keepWhatWasOffered = () => {
    if (!only) {
      return;
    }

    if (match !== undefined) {
      if (match.value !== value) {
        onChange(match.value);
      }

      return;
    }

    const answered =
      asked.current !== null && asked.current.text === typed && asked.current.options !== options;
    if (onSearch !== undefined && typed !== '' && !answered) {
      if (asked.current?.text !== typed) {
        ask(typed);
      }

      setWaiting(opened);
      return;
    }

    onChange(opened);
  };

  useEffect(() => {
    if (waiting === null || open) {
      return undefined;
    }

    const timer = setTimeout(() => setGaveUp(true), 2000);
    return () => clearTimeout(timer);
  }, [waiting, open]);

  useEffect(() => {
    if (waiting === null || open || (asked.current?.options === options && !gaveUp)) {
      return;
    }

    setWaiting(null);
    setGaveUp(false);
    onChange(match?.value ?? waiting);
  }, [waiting, open, options, gaveUp, match, onChange]);

  // The option lit in the list, by its `value` — what `cmdk` calls the value of its root, and what
  // it chooses on Enter. Held here rather than by `cmdk`, so that the box decides when something is
  // lit at all.
  //
  // ⚠️ **Nothing is lit until somebody searches or moves.** The list opens on focus, and a lit
  // option there would turn the Enter of somebody who only walked through the field into a choice
  // they never made. `cmdk` lights its first option by itself whenever its value is empty, so
  // "nothing" is a value no option carries.
  const [lit, setLit] = useState(NOTHING_LIT);
  const root = useRef<HTMLDivElement>(null);

  // What `cmdk` is told is lit. In a closed field that somebody is searching, the first option still
  // shown is lit unless an arrow lit another: what is typed there is a search, and Enter finishes it —
  // part of a callsign, Enter, and the one it matches is chosen. In an open field what is typed is
  // the value, and only an arrow or the pointer lights an option.
  const shown = [...groups.values()].flat();
  const lighting =
    only && typed !== '' && !shown.some((suggestion) => suggestion.value === lit)
      ? (shown[0]?.value ?? NOTHING_LIT)
      : lit;

  // What `cmdk` itself chooses on Enter: the row it has marked, and nothing else.
  const litOption = () => list.current?.querySelector('[cmdk-item][aria-selected="true"]') ?? null;

  // A key typed in the box, handed to the list as if it were typed in `cmdk`'s own input. A new event,
  // because an event is dispatched once; `cmdk` handles it before `dispatchEvent` returns.
  const toList = (key: string) =>
    root.current?.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));

  // Opening from the box lights nothing, or the first option when an arrow opened it: the arrow's
  // promise is "take me into the list".
  const show = (from: string = NOTHING_LIT) => {
    if (!open) {
      setLit(from);
    }

    setOpen(true);
  };

  // ⚠️ The box points at the list and at the lit row, so that a screen reader follows the arrows
  // while the focus stays in the box. Both ids are `cmdk`'s — it overwrites any id given to its list
  // or its rows. The list's arrives through its `ref`, which also draws the box again once the list
  // is on the page: Radix mounts a popover's content one pass after it opens. The row's is read from
  // the page and set on the box by hand, since its rows are drawn after the box: React does not own
  // that attribute, and nothing else writes it.
  const [listbox, setListbox] = useState<string>();
  const holdListbox = useCallback((node: HTMLDivElement | null) => setListbox(node?.id), []);

  useLayoutEffect(() => {
    const row = [...(list.current?.querySelectorAll('[cmdk-item]') ?? [])].find(
      (candidate) => candidate.getAttribute('data-value') === lighting,
    );

    if (open && row !== undefined) {
      box.current?.setAttribute('aria-activedescendant', row.id);
    } else {
      box.current?.removeAttribute('aria-activedescendant');
    }
  });

  return (
    <PopoverRoot
      open={open}
      onOpenChange={(next) => {
        if (next) {
          setOpened(value);
        } else if (document.activeElement !== box.current) {
          // Closing with the focus in the list or already past it — a click elsewhere after a press
          // on the list, Tab or Escape from there — is leaving the field, and the box had no blur
          // to say so.
          keepWhatWasOffered();
        }

        setOpen(next);
      }}
    >
      <PopoverAnchor asChild>
        <Input
          ref={box}
          id={id}
          value={value}
          autoComplete="off"
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls={open ? listbox : undefined}
          onChange={(event) => {
            onChange(event.target.value);
            // In an open field what is typed is the value again: a row lit before it is not a choice.
            if (open && !only) {
              setLit(NOTHING_LIT);
            }

            show();
          }}
          onFocus={(event) => {
            // Back from the list is not arriving: the search goes on from where it was. Back while the
            // field waits for an answer is coming back to the same search, and what it would put back
            // is still what it held before it.
            if (!inList(event.relatedTarget)) {
              setOpened(waiting ?? value);
              setWaiting(null);
              setGaveUp(false);
            }

            show();
          }}
          onKeyDown={(event) => {
            if (event.key === 'Escape') {
              setOpen(false);
              return;
            }

            // The arrows move through the list, and open it first when it is closed. Never the caret:
            // in a one line box they would only jump it to an end.
            if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
              event.preventDefault();

              if (!open) {
                show(shown[0]?.value);
              } else {
                // Up from nothing lit is the last option, as it is in any list; `cmdk` would stay put.
                toList(event.key === 'ArrowUp' && litOption() === null ? 'End' : event.key);
              }

              return;
            }

            if (event.key !== 'Enter' || event.nativeEvent.isComposing) {
              return;
            }

            // ⚠️ Enter chooses the lit option, and then it is **only** a choice: without
            // `preventDefault` the browser also sends the form — the only line of a form with no
            // button does exactly that (the request of a training, #144).
            if (open && litOption() !== null) {
              event.preventDefault();
              toList('Enter');
              return;
            }

            // Nothing lit. In a closed field something typed that nobody offered is a search nobody
            // finished, and sending it would send a value the server refuses: Enter opens the list
            // instead. An empty box is not a search, and neither is what the field held on arrival.
            if (only && !offered && typed !== '') {
              event.preventDefault();
              show();
              return;
            }

            // Otherwise what the box holds is a value — chosen, or typed in an open field — and Enter
            // is the form's again: it sends it, as in any line of a form. Deliberately: a field that
            // swallowed Enter would be the one line of a form where it does nothing. What it sends is the
            // option as it is offered, not as it was typed.
            if (only && match !== undefined && match.value !== value) {
              onChange(match.value);
            }

            setOpen(false);
          }}
          onBlur={(event) => {
            if (!inList(event.relatedTarget)) {
              keepWhatWasOffered();
            }
          }}
        />
      </PopoverAnchor>

      <PopoverContent
        ref={list}
        align="start"
        className="w-(--radix-popover-trigger-width) p-0"
        // The box keeps the focus: this list is read while typing, and a popover that stole it
        // would end the typing it exists to help.
        onOpenAutoFocus={(event) => event.preventDefault()}
        // ⚠️ And a click **in the box** does not count as clicking away. Without this the list
        // opened on focus and closed on the very same click, so it never appeared to a mouse —
        // while a test in jsdom passed, because jsdom does not deliver the pointer event Radix
        // dismisses on. Measured in a browser.
        onInteractOutside={(event) => {
          if (box.current?.contains(event.target as Node) === true) {
            event.preventDefault();
          }
        }}
      >
        <CommandRoot
          ref={root}
          shouldFilter={false}
          value={lighting}
          // `cmdk` says "nothing" with an empty string, which to itself means "light the first".
          onValueChange={(next) => setLit(next === '' ? NOTHING_LIT : next)}
        >
          <CommandList ref={holdListbox}>
            {matching.length === 0 ? <CommandEmpty>{empty}</CommandEmpty> : null}

            {[...groups].map(([heading, items]) => (
              <CommandGroup key={heading} {...(heading === '' ? {} : { heading })}>
                {items.map((suggestion) => (
                  <CommandItem
                    key={suggestion.value}
                    value={suggestion.value}
                    onSelect={() => {
                      onChange(suggestion.value);
                      // What was there, from now on, is the choice: the box may be entered again from
                      // the list while it is still closing, and that is not an arrival that reads it.
                      setOpened(suggestion.value);
                      setOpen(false);
                    }}
                  >
                    <span className="flex flex-col">
                      <span>{suggestion.label}</span>
                      <span className="text-muted-foreground text-xs">{suggestion.value}</span>
                    </span>
                  </CommandItem>
                ))}
              </CommandGroup>
            ))}
          </CommandList>
        </CommandRoot>
      </PopoverContent>
    </PopoverRoot>
  );
}
