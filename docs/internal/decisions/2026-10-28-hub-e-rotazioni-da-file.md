# Hub e rotazioni da file

**Data:** 28 ottobre 2026, fase di test, fase 6 della nota `2026-10-22-il-piano-dopo-la-prova-dello-staff` (§4.G, voce p).
**Stato:** **decisa.** Che hub e rotazioni si importino da file l'ha deciso Carmine approvando la nota del 22 ottobre (§7.6); le due
domande del §4 **le ha decise Carmine in chat alla sessione di lavoro, 29 ottobre 2026**, con le raccomandazioni: «Yes, both» — (a)
una tabella sola con le colonne `Hub` e `Rotation`, (a) «unisci» e «sostituisci» come le leg.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione nuova del modulo (l'import di T8 esteso ai tour `Hub`). Non tocca il
nucleo: legge il file nel browser e scrive con le regole che hub, rotazioni e leg hanno già.

## 1. Il problema

Un tour `Hub` oggi si scrive a mano: gli hub uno per uno (scheda «Hub»), le rotazioni una per una, poi ogni leg nella griglia con la
sua rotazione. L'import delle leg (T8, design M2 §8.4) **esclude** i tour `Hub`: «le leg di un tour hub appartengono a rotazioni che un
file non sa nominare» (Carmine, 22 settembre 2026, scritto in `LegGrid`). Lo staff chiede di poterlo fare da file, come le leg.

## 2. La proposta: il file delle leg con due colonne in più

Lo stesso file (XLSX, XLS, ODS, CSV, letto nel browser come le leg; le stesse colonne, trovate per nome) con **due colonne in più**:

| Colonna | Che cosa dice |
|---|---|
| `Hub` | L'ICAO dell'hub da cui parte e a cui torna la rotazione della riga |
| `Rotation` | Un'etichetta qualsiasi (`1`, `A`, `Sud`) che tiene insieme le righe della stessa rotazione dentro il suo hub |

- **Una riga è una leg**, nell'ordine di volo. Le righe di una rotazione sono **consecutive**.
- **Gli hub** sono quelli che il file nomina, nell'ordine in cui compaiono; **le rotazioni** nell'ordine in cui compaiono nel loro hub.
- **La dimensione** di una rotazione è il numero delle sue righe: 2, 4 o 6, se no è rifiutata sulla prima riga. Deve partire
  dall'hub e tornarci: rifiutata sulla riga, adesso e non solo quando si segna «pronto» (il file si pulisce, non si indovina).
- **Il collegamento fra due hub** è una riga con `Hub` vuoto e partenza e arrivo che sono due hub del file (`kind = HubConnection`).
- Il resto è quello delle leg: anteprima con l'impronta, poi «applica»; callsign, numeri di volo, tipi di aereo, rilascio per riga;
  ogni rifiuto sulla riga del file.

## 3. Le alternative

- **Un foglio per hub** (il nome del foglio è l'hub): più vicino a un quaderno fatto a mano, ma un file con più fogli da leggere
  insieme, e i collegamenti fra hub non hanno un foglio loro. Scartata, salvo che il quaderno FOD dei tour hub sia già fatto così.
- **Solo gli hub da file** e le rotazioni a mano: toglie poco lavoro, perché il lavoro sono le rotazioni.

## 4. Le domande per Carmine ⚖️

1. **Il formato**: (a) una tabella sola con le colonne `Hub` e `Rotation` — **raccomandata**; (b) un foglio per hub.
2. **Un secondo import sullo stesso tour**: (a) come le leg, «unisci» e «sostituisci» (ADR-051): una rotazione si riconosce dal suo hub e
   dalla sequenza delle sue tratte; «sostituisci» elimina quello che il file non nomina e non ha rapporti, e **ritira intera** la
   rotazione che ne ha (§1.4.1) — **raccomandata**, perché sulla prova un tour si corregge e si reimporta; (b) solo il primo import,
   su un tour `Hub` ancora senza hub, e le correzioni a mano.

## 5. Com'è fatto

- **Il server** (`Legs/HubImport.cs`, `HubImportPlan`, accanto a `LegImportPlan`): si confronta per **unità** — una rotazione intera, o
  un collegamento — invece che per leg. La stessa rotazione è lo stesso hub con le stesse tratte nello stesso ordine; due uguali si
  appaiano nel loro ordine, come le leg. Quello che il file non contiene resta dopo l'unità che lo precedeva («unisci»), oppure si
  elimina se non ha report e si **ritira intero** se ne ha («sostituisci»).
- **L'ordine dopo**: le leg nell'ordine del file; gli hub nell'ordine in cui le leg li nominano la prima volta; le rotazioni di un hub
  nell'ordine delle loro leg. Su «sostituisci» un hub che il file non nomina — né come hub di una rotazione né come capo di un
  collegamento — si elimina; uno che resta con rotazioni ritirate resta.
- **Una transazione sola**: prima gli hub nuovi, poi le rotazioni nuove (una leg nuova va in una rotazione che ha un identificativo),
  poi le leg; il controllo di «pronto» su tutta la forma com'è lasciata; un rifiuto disfa tutto. L'impronta dell'anteprima copre leg,
  hub e rotazioni.
- **Il file** si controlla prima, riga per riga: hub senza rotazione o rotazione senza hub, righe di una rotazione non consecutive
  (`importRotationSplit`), dimensione, partenza e ritorno all'hub, collegamento fra due hub del file o del tour. Un tour che non è
  Hub rifiuta la riga che nomina un hub o una rotazione (`legHubOnly`).
- **Il browser** legge le colonne `Hub` e `Rotation` quando ci sono; il modello scaricabile di un tour Hub le ha; l'anteprima dice per
  ogni leg hub e rotazione, e gli hub aggiunti o eliminati. Il tasto «Importa da un file» c'è anche sui tour Hub.

## Da portare nel piano

- **Design M2 §8.4** (l'import): anche i tour `Hub`, con le colonne `Hub` e `Rotation` e le regole del §2.
- **Design M2 §1.3**: hub e rotazioni nascono anche dall'import.
