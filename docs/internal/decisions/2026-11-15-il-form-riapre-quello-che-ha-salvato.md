# Il form riapre quello che ha salvato

**Data:** 15 novembre 2026, ramo `fix/live-form-saved-entries`, in coda dopo la #529 (tocca gli stessi file: `shared/forms/schema.ts`
e `shared/forms/SchemaForm.tsx`).
**Stato:** **la PR a parte l'ha decisa Carmine**, in chat al master il 15 novembre 2026, «Fix a parte dopo la #529», sulla
raccomandazione della sessione della PR 3.14 (#523). Il modo della correzione (§2) è scelta della sessione.
**Classificazione** (`CLAUDE.md` §5): **(b)**, un difetto del form generato, meccanismo del nucleo (piano §16, «A back office list or
form»), corretto dentro il meccanismo. È una modifica del nucleo: una PR sua e questa nota (`CLAUDE.md` §0, regola 6).

## 1. Il difetto

- Il salvataggio toglie i campi facoltativi che nessuno ha scritto (`writtenValues`, dalla G3 di M1, 6 settembre 2026): un testo
  tradotto vuoto in tutte le lingue non arriva al server, **anche dentro le voci di un elenco** (`pruneEntry`). È voluto: la
  pubblicazione leggerebbe `{ "it": "", "en": "" }` come una traduzione che manca.
- Riaperto nel form, quel campo tornava `undefined`. Un campo tradotto (`localized()`, un record di stringhe) lo rifiuta, quindi
  **tutto il form era invalido**: un form vivo (il pannello dei blocchi) non applicava più niente, senza un errore sullo schermo né in
  console (`safeParse` fallito in silenzio, `useLiveValues`); un form con il pulsante non mandava niente.
- Al primo livello del corpo di un blocco non si vedeva, perché il pannello stende i valori salvati sopra quelli di un blocco nuovo
  (`withDefaults` in `BlockProperties`); nelle voci di un elenco nessuno lo faceva. Ogni blocco con un testo tradotto facoltativo
  nelle voci: la nota di una postazione di `frequencyTable`, l'etichetta di un numero di `stats` e `networkStats` (PR 3.13), e gli
  altri come loro. Trovato provando a schermo la PR 3.14: una tabella delle frequenze salvata con una postazione senza nota, riaperta,
  non riceveva più né la scelta di una postazione né una frequenza scritta a mano (Undo spento); riprodotto con lo schema di `main`.

## 2. La correzione

- **`withUnwrittenBlanks(schema, values, locales)`** (`shared/forms/schema.ts`), che `SchemaForm` usa sui `defaults` prima di
  `useForm` (e per `useNewerRow`, che riprende la riga con gli stessi valori): ogni campo che manca è rimesso al suo valore vuoto,
  **a ogni profondità** — le voci di un elenco, i campi di un oggetto, un oggetto facoltativo che manca tutto.
- **Solo quello che il salvataggio toglie**: un campo **facoltativo** (rilievo 1 della revisione,
  https://github.com/SkyMistery/Ivao-Italy-Hub/pull/530#issuecomment-6901470264), e solo a un vuoto che il salvataggio toglierebbe di
  nuovo (`unwritten`): un testo vuoto, un campo tradotto vuoto in ogni lingua, un elenco vuoto. Un campo obbligatorio che manca manca
  per un'altra ragione (un corpo scritto prima che lo schema lo avesse, o a mano), e il form lo dice come ha sempre fatto. Un numero,
  un interruttore, una scelta non si inventano: `0` è un valore, e un default dichiarato lo applica lo schema.
- **Un oggetto facoltativo che manca si crea solo se tutti i suoi campi sono facoltativi** (rilievo 2): uno con un campo obbligatorio
  che nessuno può rimettere vuoto (una scelta, un numero) passerebbe da assente, che è valido, a un oggetto senza quel campo, che non lo
  è — il difetto spostato.
- Calcolato una volta per valore ricevuto (`useMemo`, rilievo 3), non a ogni battuta. Così **aprire un valore e salvarlo senza toccarlo scrive quello che c'era**: il test lo controlla
  con `writtenValues` sul valore applicato.
- **Nel form, non nel pannello dei blocchi**: il difetto è del form generato, e ogni schermata che riapre un valore salvato con un
  campo tradotto facoltativo dentro un elenco o un oggetto lo aveva. `withDefaults` del pannello resta: dà a un blocco i default
  dichiarati dello schema, che sono un'altra cosa.
- Scartate: (a) smettere di togliere i campi dalle voci — il server riceverebbe di nuovo traduzioni vuote, che la pubblicazione legge
  come buchi; (b) un `localized()` che accetta `undefined` — sposterebbe il difetto nel server e in ogni lettore delle `props`; (c) la
  correzione nel solo `BlockProperties` — le schermate con un form a pulsante restavano rotte.

## 3. Che cosa cambia per chi usa l'hub

- Un blocco riaperto si modifica di nuovo: le tabelle delle frequenze, i numeri con le etichette, ogni elenco con un testo tradotto
  facoltativo. **Nessun corpo si riscrive** (principio 4 del design della fase 3): quello che è salvato resta com'è finché qualcuno
  non scrive davvero.
- Il server non cambia.

## Da portare nel piano

- **§16** (meccanismi generici, «A back office list or form»): il form generato riapre un valore salvato rimettendo vuoti i campi che
  il salvataggio ha tolto perché non scritti, a ogni profondità, e solo quelli; aprire e salvare senza toccare non cambia il valore.
- **§13**: la correzione, in coda dopo la #529; la PR 3.14 (#523) ne ha bisogno su una tabella già salvata, e lo dice nel suo corpo.
