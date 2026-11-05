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
      />
    </div>
  );
}
