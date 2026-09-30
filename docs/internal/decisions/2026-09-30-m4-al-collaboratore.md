# M4 al collaboratore: `dalberone` scrive tutti gli eventi, fasi del nucleo comprese

**Data:** 30 settembre 2026, dopo il merge di E0 (#184) e prima di E1
**Stato:** **decisa** (Carmine, 30 settembre 2026, **in chat** al master: nessun commento su GitHub da citare, come per la nota
`2026-09-25-le-fasi-in-coda`). Chiude la domanda che il piano 1.25 lasciava aperta («chi scrive M4 non è ancora deciso»).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: nessun meccanismo del prodotto cambia, ma cambia chi scrive che cosa sul
repository, fissato dalle note `2026-09-24-un-secondo-sviluppatore` e `2026-09-26-la-sessione-master` e da `CLAUDE.md` §0.

## 1. La domanda

Il design di M4 (`09-design-m4.md`, #180) e la fase E0 (#184) sono uniti; `10-piano-implementazione-m4.md` divide M4 in fasi del
modulo (E2–E17) e sette fasi del nucleo (E1, E10a–E10e, E15a). Le regole di tutte le fasi di `10` erano scritte per le sessioni di
lavoro di Carmine. Il piano 1.25 lasciava aperte due strade: `dalberone` su tutta M4, oppure `dalberone` sul modulo da E2 e le fasi
del nucleo alle sessioni di lavoro di Carmine.

## 2. La risposta di Carmine

**`dalberone` scrive tutta M4**: il modulo **e** le fasi del nucleo (E1, E10a–E10e, E15a). Ogni fase del nucleo resta **una PR a
sé con una nota nuova** (`CLAUDE.md` §0 regola 6, §5 caso (b)), prima della fase del modulo che la usa. Il master rivede ogni PR per
intero e unisce sul via di Carmine, come per M3.

**Perché** (Carmine): le sessioni di `dalberone` lavorano a uno sforzo più alto di quelle del maintainer, e questo compensa la
conoscenza che gli manca; quella conoscenza il master la consegna **per iscritto**, in `HANDOFF-M4.md` («Per chi prende M4»), in
`CONTRIBUTING.md` e in `CLAUDE.md`.

## 3. L'alternativa scartata

| Alternativa | Perché no |
|---|---|
| Il nucleo alle sessioni di lavoro di Carmine, il modulo a `dalberone` da E2 | Era l'alternativa di Carmine stessa, considerata e messa da parte da lui per la ragione del §2. Quello che segue è del master, non di Carmine: due autori su una catena di fasi che dipendono l'una dall'altra (E10a–E10e prima di E11–E14) vogliono dire attese incrociate e due modi di leggere lo stesso design. Con un autore solo la fase del nucleo la scrive chi poi la usa nel modulo. Il rischio che una sessione di Carmine avrebbe evitato — non sapere che cosa il nucleo ha già e dove — si copre con la sezione nuova di `HANDOFF-M4.md` e con la revisione del master, che legge per intero ogni PR del nucleo. |

## 4. Che cosa cambia

- **`.github/scripts/core-guard.sh`**: `OWN` riconosce anche gli eventi (`[Tt]raining|/[Ee]vents`): un test o una spec che porta il
  nome del modulo all'inizio del nome del file (`EventsSkeletonTests.cs`, `web/e2e/events-*.spec.ts`,
  `web/e2e/full/events-*.spec.ts`) è lavoro suo anche quando lo modifica. **Non** lo è il modulo di prova condiviso del nucleo
  (`tests/IvaoHub.IntegrationTests/SampleEvents.cs`, le migrazioni `…_AddSampleEvents`), che resta nucleo. Le cartelle del modulo
  (`src/IvaoHub.Modules.Events/`, `web/src/modules/events/`, `locales/<lang>/events.json`) non erano nucleo già prima. Da qui
  `core-guard` giudica le PR di M4: prima, aperte come `SkyMistery`, non le giudicava (`10`, «Regole di tutte le fasi»).
- **`CLAUDE.md`**: §0, riga «Contributor» (Training e Events, fasi del nucleo di M4 comprese), regola 4 (il prefisso del branch della
  milestone, `m4/e<N>-<slug>`), regola 5 (il documento di design del modulo; per M4 design ed E0 già approvati), l'intestazione
  (`HANDOFF-M4.md`), §9 (l'handoff e il piano d'implementazione del modulo).
- **`CONTRIBUTING.md`**: i nomi dei branch e dei documenti del modulo in «Working a phase» e «Phases in a queue»; sotto «Tests» i VID
  `761001–761099` e gli slug `evt-test-` degli eventi (liberi il 30 settembre, verificato con un grep; l'estensione n.8 del design li
  voleva scritti prima di E2) e la trappola dei test dei contatti (MD esatto, `IT-EC` con un indirizzo).
- **`.github/PULL_REQUEST_TEMPLATE.md`**: l'esempio di «Phase and design» e la voce dell'handoff senza nominare solo M3.
- **`docs/internal/HANDOFF-M4.md`**: la sezione «Per chi prende M4 (`dalberone`)».
- **`10-piano-implementazione-m4.md`** («Regole di tutte le fasi»: la voce «Chi scrive» e l'avviso su `core-guard`), corretto dal
  master nella stessa PR; da qui il documento è di `dalberone`.

## 5. Da portare nel piano

Portato **in questa stessa PR** (piano 1.25, #192), perché la domanda era aperta proprio lì:

- **Intestazione, riga «Stato»**: M4 la scrive `dalberone`, tutta.
- **§13, riga M4**: chi la scrive — `dalberone`, fasi del nucleo comprese, ognuna una PR a sé con la sua nota.
- **Changelog 1.25**: una voce per questa nota.
- **`HANDOFF.md`**: «Chi scrive M4» deciso; il prossimo passo resta E1, ora di `dalberone`.
