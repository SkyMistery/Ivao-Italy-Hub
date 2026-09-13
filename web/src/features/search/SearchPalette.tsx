import {
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandRoot,
  DialogContent,
  DialogRoot,
  DialogTitle,
} from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { Search } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { staffDestinations } from '../../app/layouts/staffDestinations';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { fold } from '../../shared/search/highlight';
import { linkTarget } from '../../shared/ui/linkTarget';

import { searchQuery } from './queries';

/**
 * ⌘K for the staff: the rows of the site and the screens of the back office, in one box
 * (design M1 §7).
 *
 * It is Atmosphere's `Command`, assembled from its parts rather than through its `CommandDialog`,
 * and that is not a preference. ⚠️ `cmdk` filters the items it is given against what has been
 * typed, and `CommandDialog` passes its own props to the **dialog** and not to the command, so
 * `shouldFilter` cannot be reached through it — measured in its types and in its bundle, not
 * assumed. Our rows arrive already narrowed by the server, and a hit matched on the body of a page
 * rather than on its title would be thrown away on the way in: exactly the result a snippet exists
 * to explain. So the filtering is off and the server decides, which is the whole point of having a
 * FULLTEXT index.
 *
 * The screens, on the other hand, are a list this browser holds, so those **are** filtered here —
 * by the same box, without a round trip.
 */
export function SearchPalette({
  bootstrap,
  compact = false,
}: {
  bootstrap: Bootstrap;
  /**
   * An icon instead of the box, for the collapsed strip of the sidebar the search lives in since
   * 11 September 2026. The palette behind it is the same one either way, and so is the shortcut.
   */
  compact?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();

  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');

  // The one shortcut. `metaKey` is the command key, `ctrlKey` is its Windows and Linux twin, and
  // the browser's own "open a bookmark" is prevented only when the palette actually opens.
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
        event.preventDefault();
        setOpen((wasOpen) => !wasOpen);
      }
    };

    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, []);

  const answer = useQuery(searchQuery(query, 1, i18n.language));

  const hits = answer.data?.results.items ?? [];
  const screens = staffDestinations(bootstrap, t)
    .flatMap((group) => group.items.map((item) => ({ ...item, group: group.title, code: group.code ?? '' })))
    .filter((item) => matches(item.title, `${item.code} ${item.group}`, query));

  const go = (href: string) => {
    setOpen(false);
    setQuery('');
    // A path built from data: `RouterAnchor` is the one place that widens a string into a `to`, and
    // this is the same widening for a navigation nobody clicked.
    void navigate(linkTarget(href) as never);
  };

  return (
    <>
      {/* The way in for everybody who does not know there is a shortcut. Asked for by Carmine
          after the demo: ⌘K is invisible, and a back office whose search you have to be told about
          is a search most of the staff will never use.

          A button dressed as a box, and not an input: what is typed belongs to the palette, and a
          second box that also searched would be a second search — the thing this whole screen
          exists not to be (design M1 §7). The shortcut is written on it, so the box teaches it. */}
      {compact ? (
        // The collapsed strip has room for a square and no more. Named, because a button that is
        // only an icon says what it is to a screen reader through its label and to everybody else
        // through its tooltip.
        <button
          type="button"
          onClick={() => setOpen(true)}
          aria-label={t('search.palette.placeholder')}
          title={`${t('search.palette.placeholder')} (${t('search.palette.shortcut')})`}
          className="text-fuselage-400 hover:bg-fuselage-100 hover:text-fuselage-600 dark:hover:bg-fuselage-800 dark:hover:text-fuselage-200 flex size-9 items-center justify-center rounded-md transition-colors"
        >
          <Search aria-hidden className="size-4" />
        </button>
      ) : (
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="border-border bg-muted/40 text-muted-foreground hover:bg-muted focus-visible:ring-fuselage-700 flex w-full items-center gap-2 rounded-md border px-3 py-2 text-left text-sm transition-colors focus-visible:ring-1 focus-visible:outline-hidden"
        >
          <Search aria-hidden className="size-4 shrink-0" />
          <span className="truncate">{t('search.palette.placeholder')}</span>
          {/* ⚠️ `max-sm:hidden` and not `hidden sm:block`, which does nothing in this application:
            Atmosphere's stylesheet is imported after Tailwind's utilities and declares `.hidden`
            again, so the plain class wins over the one inside the `sm` media query and the element
            never comes back. Measured here, in the built bundle, after wondering where this had
            gone. */}
          <kbd className="border-border bg-background ml-auto rounded border px-1.5 py-0.5 text-xs whitespace-nowrap max-sm:hidden">
            {t('search.palette.shortcut')}
          </kbd>
        </button>
      )}

      {/* ⚠️ The three pieces `CommandDialogRoot` puts together — a dialog, its content, and a
          command — written out here for one reason: it forwards its own props to the **dialog**,
          so `shouldFilter` never reaches the command through it, and it already wraps one, so
          nesting a second inside it is a command inside a command. Read in its bundle, not
          assumed. Same components, same classes, one prop more. */}
      <DialogRoot open={open} onOpenChange={setOpen}>
        <DialogContent className="overflow-hidden p-0 shadow-lg">
          {/* Radix wants a dialog to have a name, and a screen reader wants one more than Radix
            does. It is not drawn, because the box below says the same thing to everybody else. */}
          <DialogTitle className="sr-only">{t('search.palette.placeholder')}</DialogTitle>

          <CommandRoot shouldFilter={false} label={t('search.palette.placeholder')}>
            <CommandInput
              value={query}
              onValueChange={setQuery}
              placeholder={t('search.palette.placeholder')}
            />

            <CommandList>
              {hits.length === 0 && screens.length === 0 ? (
                <CommandEmpty>{t('search.palette.empty')}</CommandEmpty>
              ) : null}

              {screens.length === 0 ? null : (
                <CommandGroup heading={t('search.palette.screens')}>
                  {screens.map((screen) => (
                    <CommandItem key={screen.href} value={screen.href} onSelect={() => go(screen.href)}>
                      {screen.group} — {screen.title}
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}

              {hits.length === 0 ? null : (
                <CommandGroup heading={t('search.palette.results')}>
                  {hits.map((hit) => (
                    <CommandItem
                      key={`${hit.sourceModule}:${hit.sourceId}`}
                      value={hit.url}
                      onSelect={() => go(hit.url)}
                    >
                      {hit.title}
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}
            </CommandList>
          </CommandRoot>
        </DialogContent>
      </DialogRoot>
    </>
  );
}

/**
 * Whether a screen is worth offering for what has been typed. Folded the same way the highlighting
 * folds, so "citta" finds "Città" here too — and an empty box offers everything, because a palette
 * that opens empty is a palette that has to be searched before it is useful.
 */
/**
 * Whether a screen answers what was typed: **every word** of it, in any order, somewhere in the
 * department and the name of the screen.
 *
 * ⚠️ Word by word and not as one phrase since 11 September 2026. When the department's heading
 * became its name, what is searched became "ED Events Links", and the phrase "ED links" is not in
 * it — the words are not next to each other any more. A test asked the question, and the answer was
 * that renaming a heading had quietly broken the way people have always searched. It is also simply
 * the better rule: "links events" finds the same screen as "events links".
 */
function matches(title: string, group: string, query: string): boolean {
  const words = fold(query.trim())
    .split(/\s+/)
    .filter((word) => word !== '');
  if (words.length === 0) {
    return true;
  }

  const haystack = fold(`${group} ${title}`);
  return words.every((word) => haystack.includes(word));
}
