import { Input } from '@ivao/atmosphere-react';
import { Search } from 'lucide-react';
import { useEffect, useState } from 'react';

/**
 * The search box waits for the typing to stop. Every keystroke is a round trip to the database
 * otherwise, and a coordinator typing a title would make a dozen of them.
 *
 * In a file of its own because two things search a list of the server: `DataList` and the grid of
 * `MediaPicker`. Like `ListFilter`, it is a piece of the lists and not one more entry of the closed
 * list of components.
 *
 * `initial` is what is being searched for now, and the box may change it from outside: the back button
 * brings back the term the address carries. It follows only when it is at rest, when what it shows is
 * what was being searched for. A box that shows anything else has somebody's typing in it, and the term
 * that has just arrived is most likely the one they sent a moment ago (`tow`, while they have written
 * `towe`): that typing is not thrown away, it goes on to its own search.
 *
 * ⚠️ So no `key` on the search term, which is how the box used to follow the address: a new box at every
 * search takes the focus away from whoever is typing in it. To empty a box whatever it holds, as
 * `MediaPicker` does after an upload, a `key` that changes only then is still the way.
 */
export function SearchBox({
  initial,
  placeholder,
  onCommit,
  className = 'max-w-xs flex-1',
}: {
  initial: string;
  placeholder: string;
  onCommit: (value: string) => void;
  className?: string;
}) {
  const [draft, setDraft] = useState(initial);

  // Adjusted while rendering rather than in an effect, like the editable cells of `DataList`.
  const [seen, setSeen] = useState(initial);
  if (seen !== initial) {
    setSeen(initial);
    if (draft === seen) {
      setDraft(initial);
    }
  }

  useEffect(() => {
    if (draft === initial) {
      return undefined;
    }

    const timer = setTimeout(() => onCommit(draft), 300);
    return () => clearTimeout(timer);
  }, [draft, initial, onCommit]);

  return (
    <div className={`relative ${className}`}>
      <Search aria-hidden className="text-muted-foreground absolute top-2.5 left-2 size-4" />
      <Input
        type="search"
        className="pl-8"
        aria-label={placeholder}
        placeholder={placeholder}
        value={draft}
        onChange={(event) => setDraft(event.target.value)}
        // Enter is "search now", and only that. The box may be drawn inside a form (`MediaPicker` in
        // a field of `SchemaForm`), where the browser would take it as submitting the whole form.
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            if (draft !== initial) {
              onCommit(draft);
            }
          }
        }}
      />
    </div>
  );
}
