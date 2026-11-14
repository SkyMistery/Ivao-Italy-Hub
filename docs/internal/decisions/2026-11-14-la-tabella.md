# La tabella: che cosa il design non diceva

**Data:** 14 novembre 2026, PR 3.12 della fase 3 (ramo `feat/table`; `12-design-editor-fase-3.md` §5.1, riga 3.12 del §7).
**Stato:** scelte della sessione di lavoro, dentro quello che il §5.1 ha già deciso (approvato da Carmine il 31 ottobre 2026, §9,
risposta 1). **Una si scosta dalla lettera del §5.1** — il nome della proprietà delle righe (§1) — e l'ha decisa Carmine, in chat al
master il 14 novembre 2026. Le altre sono della sessione: dicono dove sta e come si legge quello che il §5.1 vuole.
**Classificazione** (`CLAUDE.md` §5): (c), decisa dal design. Il §5.1 dice «nota sua: no»; questa nota c'è per i punti che il §5.1 non
scrive e che il codice ha dovuto scegliere, come le note della 3.10 e della 3.11.

## 1. `rowStyle`, non `rows` ⚖️

Il §5.1 scrive `rows: 'lines' | 'striped' | 'plain'`. Ma **`rows` è già la lista delle righe** della tabella, in `tableSchema`
(`web/src/blocks/schemas.ts:210` su `main`, `rows: z.array(z.object({ cells: … }))`): la stessa chiave non può essere l'elenco delle
righe e il loro stile. La proprietà si chiama **`rowStyle: 'lines' | 'striped' | 'plain'`**, con default `lines`, quello che si vede
oggi. Una tabella scritta prima non ha `rowStyle` e si disegna com'era: nessun corpo si sposta.

Scostamento dalla lettera, quindi una domanda per Carmine, mandata al master dalla sessione di lavoro il 14 novembre 2026 con la
raccomandazione `rowStyle`. **Decisa da Carmine**, in chat al master il 14 novembre 2026, con le sue parole: «Sì, rowStyle». Il master
la riporta come commento sulla PR: _link al commento da aggiungere qui_.

## 2. I tre stili

- **`lines`**: la linea sotto ogni riga che Atmosphere ha sempre disegnato (e che toglie all'ultima).
- **`striped`**: niente linee, e una fascia su una riga sì e una no. La fascia è **il colore del testo al 5 %** (`bg-current/5`): si legge
  su ognuno degli otto sfondi e nei due temi senza misurarla contro ognuno, e non è un colore libero (`docs/UI-GUIDELINES.md`, «no free
  colour»): è il colore che il testo ha già.
- **`plain`**: niente linee e niente fasce, solo lo spazio fra le righe.
- **L'intestazione tiene la sua linea** in tutti e tre: non è una delle righe.

## 3. La griglia: un `Dialog` largo, una copia, «Applica»

- **`BlockRegistration.editedAsGrid`** è la dichiarazione dell'eccezione (la seconda accanto a `carriesSource`, come vuole il §5.1). Nomina
  una forma, non un desiderio: le `props` sono `columns[] { label, align }` e `rows[] { cells[] { text } }`. Oggi la dichiara solo
  `table`.
- **Il `Dialog` del nucleo prende una terza misura, `wide`**: fino a 64 rem, mai più largo della finestra meno un margine. Il §5.1 dice
  «un `Dialog` largo», e il `Dialog` aveva solo quella di un form e `screen` (3.10). L'elenco chiuso resta di ventisei.
- **Le schede delle lingue** sono `LocaleTabs`, lo stesso pezzo dei campi tradotti del form (ora esportato da `shared/forms`), con il
  segno «Vuoto» sulla lingua dove manca una cella o un'intestazione. Si apre sulla lingua sullo schermo.
- **Una casella per cella e per intestazione**; sopra ogni colonna il cestino che la toglie, a destra di ogni riga quello che la toglie;
  sotto, «Aggiungi una riga» e «Aggiungi una colonna». L'allineamento delle colonne resta nel form generato.
- **La griglia lavora su una copia** e scrive una volta sola, con «Applica»: chiusa con la «X», Esc o «Annulla» non cambia niente, e
  riaperta riparte da quello che il blocco ha adesso (un Annulla dell'editor, un incolla, il form).
- **Una griglia è un rettangolo**: quante colonne ha la più larga fra l'intestazione e le righe. Una riga con una cella oltre l'ultima
  colonna (rimasta da una colonna tolta nel form, che la pagina disegna lo stesso) la griglia la mostra con una colonna sua, invece di
  nasconderla; «Applica» scrive il rettangolo.

## 4. Incolla, importa, crea: sostituiscono, e Annulla li riprende

- **Sostituiscono la tabella** (colonne e righe; la didascalia e lo stile restano), senza chiedere conferma: Annulla la rimette com'era,
  e la riga sotto i pulsanti lo dice («Ora la tabella ha 2 colonne e 3 righe.»).
- **Ognuno è un passo di Annulla a sé.** `onApplyProps` riunisce in un passo solo le scritture del form che si susseguono (la stessa
  chiave `props:<id>` di `useBodyHistory`): così un incolla dopo aver scritto la didascalia, o la griglia dopo un incolla, finivano nello
  stesso passo, e un Annulla toglieva tutti e due. `onApplyProps` prende ora un'opzione `{ step: true }` che chiude la corsa
  (`BodyEditor`). Le scritture restano quelle di `onApplyProps`, come vuole il §5.1; cambia solo come le ricorda la storia. L'e2e lo
  prova (griglia, Annulla, l'incolla resta).
- **«La prima riga è l'intestazione»** è una casella sola per incolla e importa, spuntata all'inizio. Senza, ogni riga è una riga e le
  intestazioni restano da scrivere.
- **Che cosa si scarta**: le righe vuote e le colonne vuote in fondo a destra (una selezione di un foglio ne prende spesso una in più);
  un vuoto dentro una riga resta una cella vuota. **Un a capo dentro una cella diventa uno spazio**: la pagina lo disegna come uno
  spazio comunque, e una casella della griglia è di una riga.
- **I limiti**: 20 colonne e 200 righe. Non un limite del blocco (il corpo ha il suo, in byte, sul server) ma della griglia, che disegna
  una casella per cella in ogni lingua. Un foglio più grande è rifiutato con le sue misure, non tagliato. «Crea N × M» ha gli stessi.
- **Gli appunti**: `navigator.clipboard.readText()`, cioè il permesso del browser. Se il browser non li dà (rifiutato, o la pagina non
  è in HTTPS) la riga lo dice e suggerisce il file.
- **Il file**: CSV, XLSX, XLS, ODS. Si legge il **primo foglio che ha qualcosa** (una cartella di lavoro si apre spesso su un foglio
  vuoto o di riepilogo); non c'è la scelta del foglio come nell'import delle tratte. Le celle si leggono **come il foglio le mostra**
  (`18:00`, `50%`, la data come è scritta), non come valore (una frazione di giorno, 0,5): una tabella copia quello che si vede.
- **La lingua**: quella sullo schermo, cioè la lingua in cui l'editor mostra la pagina (`usePreviewLocale`), la stessa su cui si
  aprono le schede dei campi tradotti; le altre restano da scrivere (§5.1).

## 5. Il form generato sotto: niente righe, e si ridisegna da quello che gli strumenti scrivono

- Il form generato porta `rows` **senza disegnarlo** (`withoutDrawing`, come la larghezza dell'immagine della 3.10): le righe sono della
  griglia. Restano didascalia, colonne (intestazione e allineamento) e `rowStyle`.
- ⚠️ Il form dal vivo prende i suoi valori **una volta, quando si monta**, e a ogni tasto riscrive tutto quello che ha. Dopo un incolla
  avrebbe riscritto alla prima lettera della didascalia la tabella di prima. Quindi `BlockProperties` lo ridisegna (una `key`) a ogni
  scrittura degli strumenti **e** quando le righe del blocco cambiano da fuori (un Annulla): niente di quello che si scrive nel form
  cambia le righe, quindi non si ridisegna mai sotto le dita. Un test lo prova, e senza la `key` fallisce.

## 6. Il lettore dei fogli sale nel nucleo

`web/src/shared/files/sheet.ts`: `readWorkbook` (un file → i nomi dei fogli e le righe di celle di ognuno, `formatted` per il testo che
il foglio mostra, `null` se SheetJS non lo legge), `cellText`, e `rowsOfPastedText` (il testo degli appunti di un foglio: una riga per
a capo, una cella per tabulazione, le celle fra virgolette con le virgolette raddoppiate come le scrive Excel, il CRLF di Windows dentro
una cella letto come a capo). `legFile.ts` dei tour legge da lì; il resto di `legFile` (le colonne delle tratte, le date, i codici) non
si muove e i suoi test passano come prima. La libreria resta caricata solo quando si legge un file.

## 7. Le istruzioni per Claude

- **La parte fissa** dice la tabella: le colonne con `label` e `align`, una cella per colonna nello stesso ordine, ogni intestazione e
  ogni cella in ogni lingua (una cella senza niente da dire prende un trattino, §8), i tre stili di `rowStyle`, e che una tabella della
  bozza senza `rowStyle` ha le linee. Un test controlla che nomini ogni valore di `TABLE_ROW_STYLES`.
- ⚠️ **L'impronta `rules` cambia**, perché cambia lo schema della tabella. **Da dire nelle note di rilascio**: un file per Claude
  scaricato prima di questa versione è rifiutato con `content.import.rulesChanged`.

## 8. Che cosa non cambia, e una cosa da sapere

- ⚠️ **Una cella vuota ferma la pubblicazione**, come prima di questa PR: il testo di una cella è tradotto e obbligatorio, e la
  pubblicazione legge un testo tradotto vuoto come una traduzione che manca (`BlockDocumentWalker.MissingLocales`). Un foglio incollato
  con delle celle vuote si pubblica dopo averle riempite (un trattino basta). Renderla facoltativa toccherebbe che cosa la pubblicazione
  accetta, fuori dal §5.1: se serve, è una domanda a sé.
- Il server non cambia: le proprietà nuove sono nelle `props`, che non legge.

## 9. Le parole e i test

Entrano `blocks.table.fields.rowStyle`, `blocks.table.options.rowStyle.*`, `blocks.table.tools.*` e `blocks.table.grid.*`; cambia
`blocks.table.about`. Test nuovi: `shared/files/sheet.test.ts`, `features/content/tableGrid.test.ts`,
`features/content/TableTools.test.tsx` (incolla, file, N × M, griglia, il form ridisegnato, gli stili disegnati) e `e2e/table.spec.ts`
(gli appunti veri di Chromium, la griglia larga, Annulla a passi, i tre stili misurati sul sito).

## Da portare nel piano

- **Il design dell'editor, `12-design-editor-fase-3.md` §5.1**: `rows: 'lines' | 'striped' | 'plain'` diventa
  `rowStyle: 'lines' | 'striped' | 'plain'`, default `lines` (§1, decisa da Carmine il 14 novembre 2026, «Sì, rowStyle»); la griglia
  su una copia con «Applica»; incolla, importa e crea sostituiscono e sono un passo di Annulla ciascuno; i limiti di 20 × 200; le celle
  del file come il foglio le mostra; l'a capo in una cella diventa uno spazio.
- **§9.3** (il modello dei contenuti): la tabella ha `rowStyle` (`lines`, `striped`, `plain`; di default `lines`).
- **§16** (meccanismi generici): la griglia della tabella come seconda eccezione dichiarata del form generato
  (`BlockRegistration.editedAsGrid`), accanto al codice dell'interattivo; il lettore dei fogli del nucleo (`shared/files/sheet.ts`),
  usato dalla tabella e dall'import delle tratte; `onApplyProps` con `{ step: true }` per una scrittura che è un passo di Annulla a sé.
- **§8.3** (i componenti): il `Dialog` del nucleo ha la misura `wide`; `LocaleTabs` è esportato da `shared/forms`; l'elenco resta di
  ventisei.
- **`docs/UI-GUIDELINES.md`**, se il master lo ritiene: la fascia di `striped` è il colore del testo al 5 %, non un colore libero.
- **La nota `2026-10-30-una-pagina-scritta-con-claude`** e il §3 del design: l'impronta `rules` cambia con la 3.12.
