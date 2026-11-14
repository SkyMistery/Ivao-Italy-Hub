# Domande e risposte, e le etichette dei numeri: che cosa il design non diceva

**Data:** 14 novembre 2026, PR 3.13 della fase 3 (ramo `feat/questions-and-labels`; `12-design-editor-fase-3.md` §5.4 e §5.7, riga
3.13 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.4 e il §5.7 hanno già deciso (approvati da Carmine il 31 ottobre
2026, §9, risposta 2 per le domande). **Nessuna si scosta dalla lettera** del design: dicono dove sta e come si legge quello che il
design vuole. Se il master ne legge una come uno scostamento, è una domanda per Carmine prima del merge.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design. Il §5.4 e il §5.7 dicono «nota sua: no»; questa nota c'è per i punti che
non scrivono e che il codice ha dovuto scegliere, come le note della 3.10, della 3.11 e della 3.12.

## 1. Domande e risposte

- **Le tre proprietà del §5.4**: `title` (tradotto, facoltativo), `numbered` (di default no), `initially: 'closed' | 'first' | 'all'`
  (di default `closed`, cioè com'era ogni blocco prima di questa PR). Un valore di `initially` che il blocco non conosce si legge
  `closed`. Un blocco scritto prima non ha nessuna delle tre e si disegna com'era: nessun corpo si sposta.
- **`allowMultiple` resta.** Il §5.4 dice che `all` «vale anche "più aperte insieme"», non che la casella sparisce: un corpo salvato con
  `allowMultiple: true` deve continuare a lasciare aperte più risposte (principio 4 del design, «un corpo salvato non si riscrive»).
  Quindi: con `initially: 'all'` il blocco è a più aperte qualunque cosa dica `allowMultiple`; con `closed` e `first` decide la
  casella, come prima. Lo schema lo scrive accanto alla proprietà.
- **Il titolo è un `h2`.** L'accordion di Atmosphere mette ogni domanda in un `h3` (Radix, `AccordionHeader`): il titolo che sta sopra
  le domande è il livello sopra, lo stesso a cui comincia un titolo che lo staffer aggiungerebbe a mano con il blocco `heading`. In
  stampa le domande restano `h3`, come prima. Un titolo vuoto nella lingua sullo schermo è nessun titolo. Il suggerimento sotto il campo
  dice di lasciarlo vuoto quando sopra il blocco c'è già un titolo.
- **Il numero fa parte delle parole della domanda** («1. Quando?»): un lettore di schermo lo dice con la domanda e la carta lo stampa,
  senza un elenco numerato che l'accordion di Atmosphere non sa essere.
- **Come si apre la pagina si legge una volta**, quando l'accordion si monta (`defaultValue` di Radix): l'accordion ha una `key` fatta
  di `initially`, così nell'editor una scelta cambiata si vede subito, senza riaprire la pagina. Con `all` la chiave porta anche il
  numero delle domande, perché una domanda aggiunta nasca aperta come le altre.
- **«Più risposte per domanda»** (§9, risposta 2): la risposta resta un testo. Il suggerimento sotto il campo della risposta dice che
  può avere più paragrafi e un elenco, e mai blocchi; l'e2e prova che un elenco nella risposta si disegna.
- In stampa tutto resta aperto, come prima.

## 2. Le etichette dei numeri

- Ogni voce di `stats` (`metrics[]`) e di `networkStats` (`figures[]`) ha `label`, tradotta e facoltativa, come vuole il §5.7.
- **Quale voce dà l'etichetta a un numero**: il server risponde ogni numero una volta sola, nell'ordine chiesto (`Distinct`, in
  `CoreDataBlockProviders`), quindi l'etichetta è quella della **prima voce** che chiede quel numero. Una seconda voce dello stesso numero
  non disegna niente oggi e non disegna niente adesso.
- **Vuota è nessuna etichetta**: un'etichetta vuota (o di soli spazi) nella lingua sullo schermo lascia la parola dell'hub. Un'etichetta
  vuota in tutte le lingue non arriva nemmeno al server: il form generato toglie i campi facoltativi che nessuno ha scritto, anche
  dentro le voci di un elenco (`shared/forms/schema.ts`, `writtenValues`). ⚠️ **Un'etichetta scritta in una lingua sola ferma la
  pubblicazione**, come ogni testo tradotto facoltativo (il titolo di un callout): la pubblicazione legge la lingua vuota come una
  traduzione che manca (`BlockDocumentWalker.MissingLocales`).
- **I blocchi di dati dei moduli** (§5.7, secondo punto): nessuno ha una lista di numeri con la loro parola sotto, come `stats`. I
  numeri che mostrano stanno dentro frasi con il plurale, che un'etichetta dello staffer romperebbe: nei tour `openIssues` («2
  segnalazioni aperte»), `reviewQueue`, il riepilogo di `myTours`, `errorCatalog`; negli eventi `atcCoverage` e `staffQueue`; nella
  formazione `approvalQueue`. `tourCards` non mostra numeri. Quindi nei tour non c'è niente da fare in questa PR, e **la issue per `dalberone` non è stata aperta**: non c'è un
  blocco che la chieda. Se il master o Carmine leggono il §5.7 diversamente, la issue si apre dopo.
- Il server non cambia: legge di ogni voce solo `metric` e `figure` (`BlockProps.Entries`), e il testo dell'etichetta entra nel testo
  della ricerca come ogni stringa delle `props`.

## 3. Le parole, le istruzioni per Claude, i test

- **Le parole**: `blocks.accordion.fields.{title,numbered,initially}`, `blocks.accordion.options.initially.*`,
  `blocks.accordion.hints.{title,items.answer}`, `blocks.stats.fields.metrics.label`, `blocks.stats.hints.metrics.label`,
  `blocks.networkStats.fields.figures.label`, `blocks.networkStats.hints.figures.label`; cambia `blocks.accordion.about`. In italiano
  «Piu' di una aperta insieme» diventa «Più di una aperta insieme».
- **La galleria** (`/staff/admin/ui-kit`) mostra le domande con il titolo, i numeri e la prima risposta aperta, e i numeri con
  un'etichetta scritta accanto a due dell'hub: gli esempi della registrazione (`blocks/core.ts`).
- **La parte fissa delle istruzioni** dice le domande (una risposta è un testo, mai due voci per una domanda; i tre modi di aprirsi; un
  blocco della bozza senza `initially` resta chiuso) e le etichette (le scrive solo chi lo chiede, e allora in ogni lingua). ⚠️
  **L'impronta `rules` cambia**, perché cambiano gli schemi: **da dire nelle note di rilascio**, un file per Claude scaricato prima di
  questa versione è rifiutato con `content.import.rulesChanged`.
- **I test**: `blocks/questions.test.tsx` (il blocco di prima com'era, titolo e numeri, `first`, `all` che lascia aperte le altre anche
  con `allowMultiple: false`, la scelta cambiata che si vede subito, il valore sconosciuto, la stampa, le etichette scritte e vuote dei
  due blocchi di numeri), due casi in `features/content/claudeInstructions.test.ts`, e `e2e/questions.spec.ts` (la pagina pubblicata:
  il titolo, i numeri, la prima aperta, l'elenco nella risposta, una alla volta; l'etichetta e la parola dell'hub).

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.4**: `allowMultiple` resta, e `initially: 'all'` lo rende vero; il titolo è
  un `h2` sopra le domande (`h3`); il numero fa parte della domanda.
- **§5.7**: l'etichetta è della prima voce che chiede il numero; vuota, la parola dell'hub; nessun blocco di dati dei moduli ha una lista
  di numeri con la loro parola, quindi niente nei tour e nessuna issue per gli eventi.
- **§9.3** (il modello dei contenuti): `accordion` ha `title`, `numbered`, `initially` (`closed`, `first`, `all`; di default `closed`);
  le voci di `stats` e `networkStats` hanno `label`.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: l'impronta `rules` cambia con la 3.13.
