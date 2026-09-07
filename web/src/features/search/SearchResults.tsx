import { Alert, Badge, H1, Input, Label, Lead } from '@ivao/atmosphere-react';
import { Info } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../app/layouts/RouterAnchor';
import { highlight } from '../../shared/search/highlight';

import type { SearchHit, SearchResponse } from './queries';

/**
 * What a search looks like: the box, the hits, and — when there is one — the sentence that explains
 * an empty answer (design M1 §7).
 *
 * The screen draws and decides nothing else. Which rows come back, in what order, and which of them
 * this reader may see are all the server's, through the same query filter that narrows the pages
 * themselves: a search cannot show what a page would not.
 */
export function SearchResults({
  query,
  onQueryChange,
  answer,
  pending,
}: {
  query: string;
  onQueryChange: (value: string) => void;
  answer: SearchResponse | undefined;
  pending: boolean;
}) {
  const { t } = useTranslation();

  const hits = answer?.results.items ?? [];
  const total = answer?.results.total ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-3">
        <H1>{t('search.title')}</H1>

        <div className="flex flex-col gap-1">
          <Label htmlFor="search-query">{t('search.label')}</Label>
          <Input
            id="search-query"
            type="search"
            value={query}
            placeholder={t('search.placeholder')}
            onChange={(event) => onQueryChange(event.target.value)}
          />
        </div>
      </header>

      {/* The one thing the code cannot fix, so the one thing it says out loud: a query made only of
          words shorter than the index holds finds nothing however much is published. The key comes
          from the server; the sentence is in the language files, like every other sentence. */}
      {answer?.notice == null ? null : (
        <Alert variant="default" Icon={Info} title={t('search.title')} description={t(answer.notice)} />
      )}

      {query.trim() === '' ? (
        <Lead>{t('search.empty')}</Lead>
      ) : pending ? (
        <Lead>{t('search.pending')}</Lead>
      ) : total === 0 ? (
        <Lead>{t('search.nothing', { query })}</Lead>
      ) : (
        <>
          <p className="text-muted-foreground text-sm">{t('search.count', { count: total })}</p>

          <ul className="flex flex-col gap-4">
            {hits.map((hit) => (
              <li key={`${hit.sourceModule}:${hit.sourceId}`}>
                <Hit hit={hit} query={query} />
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

/** One result: what it is, what it is called, and why it is a result. */
function Hit({ hit, query }: { hit: SearchHit; query: string }) {
  const { t } = useTranslation();

  return (
    <article className="flex flex-col gap-1">
      <div className="flex flex-wrap items-baseline gap-2">
        <RouterAnchor href={hit.url} className="text-primary font-medium underline-offset-2 hover:underline">
          <Marked text={hit.title} query={query} />
        </RouterAnchor>
        {/* What kind of row it is. `defaultValue` is the kind itself, so a module that projects a
            kind this hub has no word for shows the key rather than nothing — the same choice the
            category of a news item makes. */}
        {/* `Badge` takes its words as `text` and not as children — measured, like every other
            contract of Atmosphere this repository has had to read before using (handoff §13). */}
        <Badge variant="flat" text={t(`search.kinds.${hit.kind}`, { defaultValue: hit.kind })} />
      </div>

      {hit.snippet === '' ? null : (
        <p className="text-muted-foreground text-sm">
          <Marked text={hit.snippet} query={query} />
        </p>
      )}
    </article>
  );
}

/**
 * The text with the words somebody searched for marked.
 *
 * ⚠️ `<mark>` and not a colour: what this means is "this is why you are looking at this row", and
 * that is a meaning a screen reader has to be told too. The server sends text and never markup —
 * only the browser knows the language on screen — so the cutting happens here (design M1 §7).
 */
function Marked({ text, query }: { text: string; query: string }) {
  return (
    <>
      {/* Keyed by position, which is what these are: the pieces of one string, in order, rebuilt
          from scratch whenever the text or the query changes. There is no identity to preserve. */}
      {highlight(text, query).map((part, index) =>
        part.match ? (
          <mark key={index} className="bg-accent/40 text-foreground rounded-sm px-0.5">
            {part.text}
          </mark>
        ) : (
          <span key={index}>{part.text}</span>
        ),
      )}
    </>
  );
}
