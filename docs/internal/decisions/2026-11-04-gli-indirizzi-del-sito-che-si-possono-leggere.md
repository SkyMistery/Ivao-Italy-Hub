# Gli indirizzi del sito che chi scrive può leggere, in una lettura sola

- **Data**: 4 novembre 2026
- **Stato**: **decisa — Carmine in chat al master, 4 novembre 2026** («Sì, (a)»). La risposta è in chat e non in un commento, perché
  la sessione è di Carmine: come il §9 del design `12-design-editor-fase-3.md`
- **Chi**: la sessione di lavoro della PR 3.0a (#449) e della pagina dello stile (#450); questa PR viene dopo tutte e due, in coda
- **Classificazione** (`CLAUDE.md` §5): **(b)**, il filtro di visibilità del sito (`VisibilityQueryFilter`) usato da una lettura
  nuova del nucleo dei contenuti; tocca il nucleo del server, quindi PR sua con questa nota

## 1. Che cosa serve

La nota `2026-10-30-una-pagina-scritta-con-claude` (§3) vuole nelle istruzioni per Claude «gli indirizzi pubblicati che lo staffer
può vedere», e il design (§4.5) fa del lettore degli indirizzi (`web/src/features/content/siteAddresses.ts`, nato nella 3.0a) la
base dei blocchi (3.5) e della fase 4. Nella 3.0a quel lettore usava la lista del menu, cioè `GET /api/content` filtrata a
`kind = Page`. La revisione della #449 (rilievo 2) ha trovato che quella lista è ristretta ai **dipartimenti di chi legge**, si ferma
a cento righe e ha solo pagine. Bastava al menu, che si scrive solo con la direzione e il web, perché quei due raggiungono tutti i
dipartimenti. Non basta a uno staffer qualunque, che non vedeva una pagina pubblica di un altro dipartimento.

Nessuna lettura che c'è dà quella lista:

- `GET /api/content` è ristretta ai dipartimenti di chi legge;
- `GET /api/content/pages` (l'albero) legge con `CrudSource.BackOffice`, quindi dà anche le bozze e non dice la visibilità;
- la ricerca vuole una parola;
- la sitemap ha solo le pagine pubbliche.

Il master ha posto la domanda a Carmine il 4 novembre 2026. Carmine ha scelto la (a): «Sì, (a)».

## 2. Che cosa si fa

- **`GET /api/content/addresses`**, dietro `Content.Edit` (chi scrive un contenuto e scarica le istruzioni). Legge
  `database.Contents` **con il filtro del sito**: la visibilità di chi chiede, e solo le righe con una versione pubblicata. Niente
  `CrudSource.BackOffice` e niente `IgnoreQueryFilters`: chi chiede riceve esattamente quello che il sito gli mostrerebbe.
- Dà **pagine, news e documenti**, non i modelli né le dashboard, con id, tipo, indirizzo (`ContentEntry.Url`: `/<percorso>`,
  `/news/<slug>`, `/documents/<slug>`), titolo, dipartimento e visibilità.
- **Senza pagine.** Le istruzioni hanno bisogno della lista intera: un indirizzo che non c'è è un indirizzo che Claude non può
  usare. Una pagina di cento righe del motore delle liste farebbe tornare il difetto della #449. Una divisione ha qualche centinaio
  di righe pubblicate; ogni riga sono un centinaio di byte (niente corpo), quindi anche mille righe stanno in una risposta piccola.
  L'ordine è tipo e indirizzo, così la lista è stabile.
- **Il browser**: `siteAddresses.ts` prende queste righe al posto della lista del menu per le istruzioni e per il pannello dello stile
  (#450). Le istruzioni tornano a dire che la lista è **completa**: «una pagina che non c'è non esiste per te». Il menu invece tiene
  la sua lettura (`menuDestinationPagesQuery`): offre anche le bozze, perché una voce di menu si scrive prima che la pagina esca, e
  cerca mentre si scrive. Le due liste passano dallo stesso costruttore, con le schermate e i link della libreria.

## 3. Che cosa tocca

- **Server, nucleo**: una lettura a mano in più nel gruppo dei contenuti, `ContentEndpoints.cs`, con il suo DTO; da contare nel
  §16.6 del piano.
- **Browser, nucleo**: `features/content/siteAddresses.ts`, `queries.ts`, `ClaudeDownload.tsx`, `ContentSettingsPanel.tsx`, il testo
  di `claudeInstructions.md` e `claudeInstructions.ts`, il client generato.
- **Test**: d'integrazione (uno staffer di un dipartimento vede le pagine pubbliche e dello staff di un altro, non le sue pagine
  «Department», non le bozze, non i modelli; news e documenti ci sono), unitari ed e2e del lettore e delle istruzioni.

## 4. Alternative considerate

| | Che cosa | Perché no |
|---|---|---|
| (b) | Restano le pagine dei dipartimenti di chi scarica, e le istruzioni lo dicono | Claude non può linkare una pagina pubblica di un altro dipartimento; Carmine ha scelto la (a) |
| — | Allargare `GET /api/content` | È la lista del back office, ristretta ai dipartimenti per costruzione: le bozze degli altri non si danno |
| — | L'albero `/api/content/pages` | Dà le bozze e non dice la visibilità; serve a dove mettere una pagina, non a dove portare un link |

## Da portare nel piano

- **§16** (meccanismi generici): il lettore degli indirizzi del sito (`siteAddresses.ts`) prende le righe pubblicate da
  `GET /api/content/addresses`, che legge con il filtro di visibilità di chi chiede: pagine, news e documenti di ogni dipartimento,
  senza pagine. Il menu tiene la sua lettura con le bozze. Lo usano le istruzioni per Claude, il pannello dello stile, poi i blocchi
  (3.5) e la fase 4.
- **§16.6**: una lettura a mano in più nella famiglia dei contenuti (`GET /api/content/addresses`).
- **§9.3**, con la nota del 30 ottobre: «gli indirizzi pubblicati che lo staffer può vedere» sono quelli di questa lettura, e le
  istruzioni li danno tutti.
