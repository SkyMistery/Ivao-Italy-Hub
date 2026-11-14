# Un CSV è il testo scritto: il lettore dei fogli non deduce numeri né date

**Data:** 14 novembre 2026 (ramo `fix/sheet-csv-raw`; issue #334, punto 3, lotto 10 della revisione del codice).
**Stato:** **decisa dal master** il 14 novembre 2026, in chat con la sessione di lavoro: la proposta `raw: true` per un testo, non un
campo nuovo di `Workbook`; due PR (questa del nucleo, poi quella del modulo dei tour in coda). Nessuna domanda per Carmine: è la
correzione di un difetto dentro quello che il §5.1 di `12-design-editor-fase-3.md` e il §8.4 di `05-design-m2.md` hanno già deciso.
**Classificazione** (`CLAUDE.md` §5): **(b)**, si estende il lettore dei fogli del nucleo (`web/src/shared/files/sheet.ts`); per la
regola 6 di `CLAUDE.md` §0 è una PR a sé, prima di quella del modulo che la usa.

## 1. Il difetto

`readWorkbook` passava a SheetJS il testo di un CSV senza `raw`, e SheetJS **deduce** un numero o una data da ogni cella di un CSV.
La lettura dei valori (quella senza `formatted`, che legge solo l'import delle leg dei tour) riceveva così un numero di giorni dove il
file aveva scritto un testo, e il modulo non poteva più rifiutare una data che non è ISO: il §8.4 vuole che il file si pulisca, non
che si indovini. Misurato con SheetJS 0.20.3, il risultato è lo stesso in UTC, Europe/Rome e America/New_York:

| testo nella colonna `release` del CSV | `releaseAt` prima | atteso |
|---|---|---|
| `01/10/2026` (il primo ottobre, all'italiana) | `2026-01-10T00:00Z`, **il 10 gennaio, in silenzio** | rifiutata sulla riga |
| `01/10/2026 18:00` | `2026-01-10T18:00Z` | rifiutata sulla riga |
| `10/1/2026` | `2026-10-01T00:00Z` | rifiutata sulla riga |
| `18:00` | `1899-12-30T18:00Z` | rifiutata sulla riga |
| `Oct 1 2026 18:00`, `1 October 2026`, `2026-10-01T18:00:00+02:00` | rifiutata | rifiutata (già giusto) |
| `2026-10-01T18:00:00Z`, `2026-10-01T18:00Z`, `2026-10-01 18:00`, `2026-10-01T18:00:00`, `2026-10-01` | giuste | giuste |

La issue descriveva un altro sintomo, un orario con `Z` spostato del fuso del browser, e **non si riproduce** con SheetJS 0.20.3. In
`fuzzydate` una data ISO con `Z` si legge con `new Date(s)`, che è UTC, e `datenum` conta da `Date.UTC(1899, 11, 30)`. La causa però è
la stessa (la deduzione delle date di un CSV), e la cura di questa nota toglie anche quella.

## 2. La scelta

`XLSX.read(…, { raw: isText })`: quando il lettore ha decodificato il file come testo (`textOf`, PR 3.12), SheetJS non deduce niente, e
ogni cella del CSV è la stringa scritta (`cell.t = 's'`), in tutte e due le letture. Un file di un foglio (XLSX, XLS, ODS, UTF-16)
resta letto come prima, con i suoi valori. Il modulo dei tour, che legge i valori, riceve il testo e applica la regola che ha già per
il testo (`releaseOf`: ISO in UTC, o un problema sulla riga).

**La lettura `formatted`** (quella della tabella, `TableTools`) di un CSV dava già il testo scritto, perché SheetJS mette `cell.w = s` a
ogni cella di un CSV prima di dedurre. Restano uguali, provati uno per uno in una tabella di casi di `sheet.test.ts`, che passa con e
senza la modifica: `007`, `1,5`, `1.5`, `250`, `1e3`, `50%`, `€ 5`, `1.234,5`, `-`, `01/10/2026`, `18:00`, `2026-10-01T18:00:00Z`,
`TRUE`, `FALSE`, `=1+1`, `="007"`, `" x "` (letto ` x `) e `"say ""hi"""` (letto `say "hi"`).

**Un cambiamento di comportamento della lettura `formatted`, limitato agli errori di Excel scritti nel CSV** (`#N/A`, `#DIV/0!`,
`#REF!`): prima diventavano una cella **vuota** (SheetJS li teneva come errori e la lettura formattata li saltava), ora sono il loro
testo. Accettato dal master: il file ha scritto `#N/A` e un foglio lo mostra così. In più, una cella vuota della tabella ferma la
pubblicazione (nota `2026-11-14-la-tabella`, ⚠️ «Una cella vuota ferma la pubblicazione»), quindi un errore di Excel nel CSV bloccava la
pagina senza dire perché. Nessun caso speciale per rimetterli a `null`; un test lo fissa.

**La lettura grezza sbagliava anche i numeri europei**: `1,5` diventava 15 e `1.234,5` diventava 1,2345. Ora restano testo, come
tutto il resto di un CSV.

## 3. Alternative scartate

- **`Workbook.text: boolean`**, e il modulo sceglie la lettura `formatted` per un CSV: un campo in più nel contratto del lettore per
  una domanda sola, e ogni lettore futuro dei valori dovrebbe ricordarsi di farla.
- **Riconoscere il CSV dal nome nel modulo** (`.csv`): una seconda regola accanto a `textOf` del nucleo, che riconosce il testo dai
  byte, e le due possono non essere d'accordo.
- **`dateNF` o `cellDates`**: dicono a SheetJS in che ordine dedurre, ma la deduzione resta (e il commento del lettore dice già
  perché `dateNF` non gli si passa).

## Da portare nel piano

- **`12-design-editor-fase-3.md` §5.1, punto (5)**: accanto a «il testo di un CSV lo decodifica il lettore», «e le sue celle sono il
  testo scritto: niente numeri né date dedotti; un errore di Excel scritto nel CSV (`#N/A`) è il suo testo, non una cella vuota».
- **`05-design-m2.md` §8.4** (import delle leg): un CSV si legge come testo, e una data che non è ISO si rifiuta sulla sua riga anche
  in un CSV.
- **Changelog**: una riga per questa nota e per la PR del modulo dei tour che la segue (#334 punto 3).
