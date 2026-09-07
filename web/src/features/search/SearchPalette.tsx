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
export function SearchPalette({ bootstrap }: { bootstrap: Bootstrap }) {
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
    .flatMap((group) => group.items.map((item) => ({ ...item, group: group.title })))
    .filter((item) => matches(item.title, item.group, query));

  const go = (href: string) => {
    setOpen(false);
    setQuery('');
    // A path built from data: `RouterAnchor` is the one place that widens a string into a `to`, and
    // this is the same widening for a navigation nobody clicked.
    void navigate({ to: href as never });
  };

  return (
    <>
      {/* The way in for everybody who does not know there is a shortcut. Asked for by Carmine
          after the demo: ⌘K is invisible, and a back office whose search you have to be told about
          is a search most of the staff will never use.

          A button dressed as a box, and not an input: what is typed belongs to the palette, and a
          second box that also searched would be a second search — the thing this whole screen
          exists not to be (design M1 §7). The shortcut is written on it, so the box teaches it. */}
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
        <kbd className="border-border bg-background ml-auto rounded border px-1.5 py-0.5 text-xs max-sm:hidden">
          {t('search.palette.shortcut')}
        </kbd>
      </button>

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
function matches(title: string, group: string, query: string): boolean {
  const typed = fold(query.trim());
  if (typed === '') {
    return true;
  }

  return fold(`${group} ${title}`).includes(typed);
}
