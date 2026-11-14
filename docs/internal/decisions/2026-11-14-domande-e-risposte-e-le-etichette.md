# Domande e risposte, e le etichette dei numeri: che cosa il design non diceva

**Data:** 14 novembre 2026, PR 3.13 della fase 3 (ramo `feat/questions-and-labels`; `12-design-editor-fase-3.md` §5.4 e §5.7, riga
3.13 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.4 e il §5.7 hanno già deciso (approvati da Carmine il 31 ottobre
2026, §9, risposta 2 per le domande): dicono dove sta e come si legge quello che il design vuole. **La parte dei moduli del §5.7
(§2, terzo punto) l'ha decisa Carmine** il 14 novembre 2026, sulla domanda del master (rilievo 3 della revisione): la lettera del
design, e la issue #518 per gli eventi.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design. Il §5.4 e il §5.7 dicono «nota sua: no»; questa nota c'è per i punti che
non scrivono e che il codice ha dovuto scegliere, come le note della 3.10, della 3.11 e della 3.12.

## 1. Domande e risposte

- **Le tre proprietà del §5.4**: `title` (tradotto, facoltativo), `numbered` (di default no), `initially: 'closed' | 'first' | 'all'`
  (di default `closed`, cioè com'era ogni blocco prima di questa PR). Un valore di `initially` che il blocco non conosce si legge
  `closed`. Un blocco scritto prima non ha nessuna delle tre e si disegna com'era: nessun corpo si sposta.
- **`allowMultiple` resta.** Il §5.4 dice che `all` «vale anche "più aperte insieme"», non che la casella sparisce: un corpo salvato con
  `allowMultiple: true` deve continuare a lasciare aperte più risposte (principio 4 del design, «un corpo salvato non si riscrive»).
  Quindi: con `initially: 'all'` il blocco è a più aperte qualunque cosa dica `allowMultiple`; con `closed` e `first` decide la
  casella, come prima. Lo schema lo scrive accanto alla proprietà, e lo staffer lo legge sotto la casella
  (`blocks.accordion.hints.allowMultiple`, rilievo 2 della revisione): «Più di una aperta insieme» con `all` non cambia niente sullo
  schermo, e senza la frase sembrava rotta. Il form generato non disegnava il suggerimento sotto un interruttore, come sotto ogni altro
  campo: ora lo disegna (`shared/forms/SchemaForm.tsx`, il caso `boolean`); un interruttore senza suggerimento è disegnato com'era.
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
- **I blocchi di dati dei moduli** (§5.7, secondo punto) ⚖️ — **deciso da Carmine**: un blocco di dati di un modulo che mostra numeri
  ha le stesse etichette, come dice la lettera del §5.7, ed **`events.atcCoverage` è uno di questi** (disponibilità, pezzi scoperti,
  trasferimenti, oggi dentro frasi con il plurale). **Non in questa PR**: per il modulo Eventi c'è la issue #518, assegnata a
  `dalberone`. Risposta di Carmine in chat al master il 14 novembre 2026, «Sì a tutte e cinque, come raccomandi», riportata dal master
  sulla PR: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/511#issuecomment-6883307038.
- **I blocchi di dati dei tour** (§5.7, secondo punto: «i tour […] seguono nella stessa PR se ne hanno») ⚖️ — **deciso da Carmine**:
  una **issue a parte**, come per gli eventi, la #521; i blocchi dei tour che mostrano numeri (`openIssues`, `reviewQueue`, il
  riepilogo di `myTours`, `errorCatalog`) non si toccano qui. Risposta di Carmine in chat al master il 14 novembre 2026, «Issue a
  parte per i tour», riportata dal master sulla PR: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/511#issuecomment-6883857820.
  Questa PR non tocca i blocchi dei moduli.
- Il server non cambia: legge di ogni voce solo `metric` e `figure` (`BlockProps.Entries`), e il testo dell'etichetta entra nel testo
  della ricerca come ogni stringa delle `props`.

## 3. La richiesta di un blocco di dati porta la domanda, non le parole

**Classificazione** (`CLAUDE.md` §5): **(b)**, estende il meccanismo dei blocchi di dati del nucleo (piano §16, «Data blocks»). Dentro
questa PR, sul via del master (rilievo 1 della revisione): la 3.13 è già tutta nucleo, editor e blocchi, come la 3.12 con `Dialog wide`
e `LocaleTabs`.

- **Il difetto** (rilievo 1): la richiesta dal vivo di un blocco di dati portava **tutte** le `props`, codificate, nella chiave della
  cache e nell'indirizzo (`blockDataQuery`, `web/src/blocks/data.ts`). Con la 3.13 l'etichetta sta nelle `props` di `stats` e
  `networkStats`, e il pannello applica il form a ogni tasto: scrivere «piloti e controllori» sotto un numero faceva una ventina di
  chiavi, i numeri tornavano a «caricamento» a ogni lettera e il fornitore contava ogni volta. Sul sito, due pagine con lo stesso numero
  e parole diverse non dividevano la risposta.
- **La causa si toglie, non si nasconde.** `questionOf(schema, props)`, in `data.ts`, toglie dalle `props` i campi che lo schema zod
  del blocco segna come parole (`localized`, letti con `readFields` del form generato, una volta per schema), **anche dentro le voci di
  un elenco**; tutto il resto resta dov'era, nello stesso ordine. `DataBlockView` (`ContentRenderer.tsx`) chiede con quella, dalla
  `registration.schema` che ha già. Nessuna dichiarazione nuova nella registrazione: le parole di un blocco le dice già il suo schema.
- **Un blocco senza parole chiede quello che chiedeva**: `questionOf` gli rende le sue stesse `props` (lo stesso oggetto), quindi
  l'indirizzo e la chiave della cache di una pagina pubblica senza etichette sono quelli di prima. Prima della 3.13 nessun blocco di
  dati aveva parole nelle `props` (`welcome` e `coordination` sono `Content`; i blocchi dei moduli non hanno `localized()`): oggi
  la funzione toglie qualcosa solo a `stats` e `networkStats`.
- **Nessun fornitore legge una parola**: i fornitori del server scrivono testi tradotti nelle risposte, non li leggono dalle `props`.
  Una parola che un giorno servisse alla domanda non sarebbe più una parola del blocco: sarebbe un campo non tradotto.
- **La cattura alla pubblicazione non cambia**: la fa il server, dalle `props` intere, e non le legge. Le altre richieste che chiamano
  `blockDataQuery` da sé (il calendario pubblico, le liste pubbliche, la striscia del traffico, la lista degli eventi) costruiscono le
  loro `props` nel codice, senza parole, e non cambiano. ⚠️ Le liste pubbliche (`PublicListScreen`) dividono la chiave fra la loro
  richiesta e il blocco che disegnano: resta divisa, perché `newsList` e `documentList` non hanno parole e `questionOf` rende al blocco
  le stesse `props`. Una lista che un giorno avesse parole dovrebbe chiedere anche lei con `questionOf`.
- **Scartata come rimedio**: `placeholderData: keepPreviousData` toglie lo sfarfallio ma il fornitore conta lo stesso a ogni tasto, e la
  cache resta divisa per parole.
- **I test** (`blocks/question.test.tsx`): le parole tolte anche dentro un elenco e l'ordine tenuto; un blocco senza parole con le stesse
  `props`, lo stesso indirizzo e la stessa chiave; due blocchi con parole diverse una domanda sola; e sulla pagina disegnata le **due
  metà**: tre lettere di un'etichetta non fanno partire richieste e il numero resta disegnato, un numero cambiato ne fa partire una.
  Mutazione: con le `props` intere in `DataBlockView` il test della pagina è rosso.

## 4. Le parole, le istruzioni per Claude, i test

- **Le parole**: `blocks.accordion.fields.{title,numbered,initially}`, `blocks.accordion.options.initially.*`,
  `blocks.accordion.hints.{title,items.answer,allowMultiple}`, `blocks.stats.fields.metrics.label`, `blocks.stats.hints.metrics.label`,
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
  due blocchi di numeri), due casi in `features/content/claudeInstructions.test.ts`, `shared/forms/SchemaForm.switchHint.test.tsx`
  (il suggerimento sotto un interruttore), `blocks/question.test.tsx` (la domanda senza le parole, §3), e `e2e/questions.spec.ts` (la pagina pubblicata:
  il titolo, i numeri, la prima aperta, l'elenco nella risposta, una alla volta; l'etichetta e la parola dell'hub).

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.4**: `allowMultiple` resta, e `initially: 'all'` lo rende vero; il titolo è
  un `h2` sopra le domande (`h3`); il numero fa parte della domanda.
- **§5.7**: l'etichetta è della prima voce che chiede il numero; vuota, la parola dell'hub; un blocco di dati di un modulo che mostra
  numeri ha le stesse etichette, `events.atcCoverage` con la issue #518 e i blocchi dei tour con la #521 (decise da Carmine, §2).
- **§16** (meccanismi generici): il form generato disegna il suggerimento anche sotto un interruttore.
- **§16, «Data blocks»** (caso (b), §3): una richiesta dal vivo porta solo la domanda — le `props` senza i campi `localized`, anche
  dentro gli elenchi (`questionOf`, `web/src/blocks/data.ts`) — e le parole restano fuori; un blocco senza parole chiede quello che
  chiedeva; la cattura alla pubblicazione non cambia.
- **§9.3** (il modello dei contenuti): `accordion` ha `title`, `numbered`, `initially` (`closed`, `first`, `all`; di default `closed`);
  le voci di `stats` e `networkStats` hanno `label`.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: l'impronta `rules` cambia con la 3.13.
