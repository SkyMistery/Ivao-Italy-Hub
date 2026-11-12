# Gli errori detti per quello che sono, e l'approvazione legata a ciò che si è letto (#332)

**Data:** 10 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/review-lot9-web`.
**Stato:** **applica la direzione della revisione** (issue #332, lotto 9 della revisione del codice, nota
`2026-10-20-la-revisione-del-codice`). Le scelte qui sotto sono della sessione di lavoro, non di Carmine: si scrivono perché il
revisore le possa vedere e il master le porti nel piano; nessuna apre una domanda nuova.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si usano i meccanismi che ci sono (le pagine di stato, la concorrenza ottimistica,
lo schema delle liste), estesi dove non bastavano.

## Che cosa serve

La revisione ha trovato tre modi in cui la SPA diceva male un errore (punto 5): la schermata inglese del router con il messaggio
grezzo, un 500 letto come «non trovato» o come «non pubblicata», e ogni query rifatta tre volte prima di dire qualcosa (circa 7 s, e
un 401 ridisegnava la shell da anonimo a ogni tentativo). E un'approvazione non legata alla versione letta (punto 2): l'autore che
ritira, cambia e rimette pronta la pagina sotto la scheda aperta di chi approva faceva pubblicare una bozza che nessuno aveva letto.

## Le scelte

- **Una terza pagina di stato, `Failed`**, accanto a `NotFound` e `Forbidden` (design M0 §7.2: «un `NotFound` e un `Forbidden`
  tradotti»): «la pagina non si è potuta caricare», con «Riprova». Come le altre due è composta da `EmptyState` e non è un componente
  nuovo dell'elenco chiuso (`docs/UI-GUIDELINES.md` §3).
- **`RouteError` è l'errore predefinito del router** (`createHubRouter`, `defaultErrorComponent`): un `ApiError` 404 è `NotFound`, un
  403 è `Forbidden`, tutto il resto è `Failed`, e «Riprova» rilegge la rotta. La radice lo disegna nella cornice del suo
  `notFoundComponent`; le pagine pubbliche (pagina, notizia, documento) lo usano al posto di `NotFound`. Le dashboard dicono
  `Failed` su ogni errore che non è un 404.
- **Un parametro di una lista scritto a mano che la lista non accetta si legge come il suo predefinito** (`.catch` su ognuno dei
  cinque di `listSearchSchema`, sul dipartimento e su tipo e stato dei contenuti): `?page=0` è la prima pagina, `?pageSize=500` la
  dimensione predefinita. Il tetto di 100 resta. Un ordinamento che il server rifiuta resta il suo 400, detto da `RouteError`.
- **Le query si rifanno una volta sola, e mai su un 4xx** (`retryQuery`, `shared/api/retry.ts`, in `main.tsx`): un 4xx è una
  risposta, e la seconda volta è la stessa; un 500 o la rete possono essere di un momento. Una volta, e non le tre della libreria.
- **Chi approva o rimanda indietro risponde alla versione che ha letto**: il riepilogo della revisione porta la `RowVersion` della riga
  che descrive, la richiesta la rimanda, e una riga che è andata avanti nel frattempo è rifiutata con 409, come ogni scrittura con una
  versione vecchia (design M0 §3.9). Le due azioni dell'autore non la mandano.
- **La pagina pubblica di una notizia o di un documento porta i nomi delle sue collezioni** (`CollectionNames`, aggiunto alla risposta
  pubblica): il vocabolario si legge nel back-office, dove il visitatore non va. Prima il nome del dipartimento della riga, poi il
  primo in ordine, come il vocabolario di un blocco elenco; una collezione cancellata non ha nome, e si mostra la chiave.

## Alternative scartate

- **`notFound()` di TanStack al posto del 404 nel loader della pagina pubblica**: la regola ESLint `only-throw-error` lo rifiuta, e un
  `ApiError(404)` dice la stessa cosa nello stesso modo in cui la dice il server.
- **Un `MutationCache.onError` globale** per i rifiuti senza un form (punto 4): un toast per ogni mutazione avrebbe detto due volte i
  rifiuti che un form già mostra campo per campo. Lo dice chi non ha un form, con `useRefusalNotice`.
- **Lasciare tre tentativi sui 5xx**: un errore che compare dopo sette secondi sembra una pagina che non risponde.

## Che cosa resta

Nulla del punto 5. Il lotto è chiuso con questa PR.

## Da portare nel piano

- §16 (le pagine di stato del front end, se il piano le elenca): la terza, `Failed`, e `RouteError` come errore predefinito del router.
- Design M0 §7.2: «un `NotFound`, un `Forbidden` e un `Failed` tradotti», e l'errore predefinito del router.
- La revisione delle pagine (G19, changelog 0.73 e §9.3): l'approvazione e il rimando portano la versione del riepilogo letto, 409 se
  la riga è cambiata.
- `HANDOFF.md` §3, regole già attive: un parametro di lista sbagliato si legge come il predefinito; un 4xx non si richiede.
