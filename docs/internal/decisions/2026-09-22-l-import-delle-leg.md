# L'import delle leg: SheetJS nel browser, la coppia e l'ordine, niente tour Hub

**Data:** 22 settembre 2026 — fase T8 di M2
**Stato:** **decisa** (Carmine, 22 settembre 2026, quattro domande in apertura: la libreria che `06` §T8 chiedeva di scegliere, e tre
nate leggendo il design contro il codice di T7a e T7b. Il resto sono scelte di forma, scritte qui perché si vedono nel file che il FOD
compila).
**Regola applicata:** `CLAUDE.md` §5: **(a)** per la libreria (una dipendenza, scelta come `06` §T8 chiedeva); **(c)** per come si
riconosce una leg, i tour `Hub` e le leg ritirate; **(b)** per tutto il resto: l'import passa dallo stesso `LegBook.ApplyAsync` di una
leg scritta a mano e dallo stesso `SaveAsync`, e i due verbi nuovi stanno dentro l'eccezione dichiarata dell'editor delle leg (design
§8.4, estensione n.6).

## 1. Che cosa serviva

Il design §8.4 e ADR-051 di Toursystem fissano il **che cosa**: un file XLSX o CSV letto nel browser (risposta 24), le differenze
calcolate dal server senza scrivere, «fondi» che non tocca le leg assenti e «sostituisci» che elimina le assenti senza PIREP e ritira
quelle con PIREP, con un motivo. Aprendo T8 restavano aperte quattro cose:

1. **La libreria**, con licenza compatibile con Apache-2.0 e nessun codice valutato a runtime (per la CSP, `script-src 'self'` senza
   eccezioni).
2. **Come si riconosce la stessa leg.** `06` §T8 dice «da partenza, arrivo e numero», ma nell'hub il numero **lo tiene il server** e
   cambia a ogni inserimento (T7a): una leg inserita a metà del file sposterebbe il numero di tutte quelle dopo, e il confronto le
   vedrebbe tutte cambiate, comprese quelle con PIREP.
3. **I tour `Hub`**: una leg sta in una rotazione (T7b), e le rotazioni sono righe che devono esistere prima delle leg.
4. **Una leg ritirata che il file nomina di nuovo**: ADR-051 la rimette nel tour; il design dell'hub non lo diceva.

## 2. Le decisioni

1. **SheetJS 0.20.3** (Carmine), Apache-2.0 come l'hub: legge XLSX, XLS, ODS e CSV (anche con il `;` che un foglio di calcolo scrive
   in mezza Europa) e scrive il modello. Si carica **solo quando si apre un file** (un chunk suo, 500 KB, 163 KB compressi). ⚠️ Su npm
   c'è solo la 0.18.5, con due CVE proprio nella lettura (prototype pollution, CVE-2023-30533; ReDoS, CVE-2024-22363): la versione
   corretta **si installa dal tarball del CDN di SheetJS**, fissato con l'hash nel lockfile. Il prezzo: **Dependabot non vede gli
   aggiornamenti**, e chi aggiorna lo fa a mano (`pnpm add https://cdn.sheetjs.com/xlsx-<versione>/xlsx-<versione>.tgz`).
   *Scartate, misurate il 22 settembre*: **read-excel-file 9 + Papa Parse** (MIT, 63 + 20 KB) — read-excel-file crea Web Worker da
   `blob:` con codice composto a runtime, anche dall'ingresso `universal` sui file grandi (attraverso `fflate`), e la CSP lo blocca:
   servirebbe `worker-src blob:`; **fflate + un lettore nostro + Papa Parse** — meno dipendenze, ma le stranezze di Excel (date seriali,
   stringhe condivise, zeri iniziali) a carico nostro.
2. **La stessa leg è la stessa coppia partenza→arrivo** (Carmine); se la coppia è nel tour più volte, le righe del file e le leg si
   abbinano **nell'ordine**. **Nessuna colonna di numeri**: l'ordine delle righe è l'ordine del tour. Una riga che non trova una leg è
   una leg nuova, al suo posto; in «fondi» le leg assenti restano **dopo la leg che le precedeva**. Il «numero» di `06` §T8 è quindi il
   posto fra le leg della stessa coppia. *Scartata*: una colonna «numero» come nel modello di Toursystem (il difetto del punto 1.2).
3. **L'import non vale per i tour `Hub`** in T8 (Carmine): il server lo rifiuta (`importNotOnHub`) e l'editor non offre il pulsante.
   Le leg di un tour `Hub` si scrivono nella tabella. Si riapre se il FOD lo chiede. *Scartate*: una colonna «rotazione» che nomina una
   rotazione già creata (hub più numero) e una «collegamento sì/no» — più codice e più errori possibili nel file, per un tour di poche
   rotazioni.
4. **Una leg ritirata che il file nomina torna nel tour** (Carmine), come in ADR-051: il ritiro è reversibile anche da import, con il
   motivo dell'import, e non in un tour in chiusura o chiuso, come il ripristino a mano (§1.4.1). *Scartata*: la ritirata resta com'è e
   la riga diventa una leg nuova (due leg uguali nel tour, una ritirata).

## 3. La forma (di Claude, da confermare nella revisione della PR)

- **Le colonne**: `departure`, `arrival` (obbligatorie), `callsign`, `flightNumber`, `aircraft`, `release`, nella prima riga, in
  qualunque ordine e maiuscolo; valgono anche i nomi del payload (`departureIcao`, `releaseAt`…). Una colonna sconosciuta si ignora,
  una riga vuota si salta. I nomi sono **inglesi e fissi**: il file è un formato, non un testo tradotto, e un fork lo riceve uguale.
- **Gli aerei**: il file porta **solo tipi ICAO**. I gruppi di una leg (`groupIds`) il file non li nomina, e una leg che il file
  riconosce **li tiene**; una leg nuova non ne ha.
- **Il rilascio** è in UTC, come ogni istante dell'hub: una cella data di Excel si legge con l'ora che mostra, un testo ISO
  (`2026-10-01 18:00`) pure; `01/10/2026` è rifiutato sulla riga, perché è ambiguo fra divisioni.
- **Il modello** scaricabile è solo l'intestazione, con la colonna del numero di volo in formato testo (uno zero iniziale sopravvive):
  una riga d'esempio nominerebbe gli aeroporti di qualcuno.
- **Due verbi** in più nell'eccezione dell'editor delle leg: `POST …/legs/import/preview` (le differenze, scritte da nessuna parte) e
  `POST …/legs/import` (le applica in un solo salvataggio, e risponde con la griglia come ogni scrittura). Ogni riga passa per
  `LegWriteDtoValidator` e `LegBook.ApplyAsync`, cioè le regole di una leg scritta a mano; i rifiuti stanno sotto `rows[n].campo`, e
  l'editor li dice sulla **riga del file**.
- **L'impronta**: l'anteprima risponde con un'impronta delle leg (identificativo e versione di ognuna); l'import la riporta, e se le
  leg sono cambiate nel frattempo è un **409**: si applica solo ciò che qualcuno ha guardato.
- **Un motivo per l'import**, chiesto solo quando serve: ritira, ripristina, o cambia una leg con PIREP. Finisce nel motivo del ritiro
  o nel `change_reason` della leg, cioè nell'audit.
- **«Pronto»**: l'anteprima non lo controlla; l'import di un tour pronto passa gli stessi controlli di ogni scrittura, e se il tour
  smetterebbe di esserlo non scrive niente e lo dice.
- **Mille righe** al massimo per richiesta: un tour della divisione ne ha decine.

## 4. Che cosa si tocca

`Legs/LegImport.cs` (il confronto, puro, con i test unitari), due verbi in `Legs/LegEndpoints.cs`; nel frontend
`screens/legFile.ts` (il file letto e il modello), `screens/LegImport.tsx` (il pannello dentro l'editor delle leg), un caso in più di
`useLegChange`. Nessuna migrazione, nessun componente nuovo nell'elenco chiuso (il pannello è parte di `LegGrid`, l'eccezione
dichiarata), nessun meccanismo nuovo. Corretti il design §8.4 e `06` §T8.
