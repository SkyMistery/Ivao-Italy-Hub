# Un secondo sviluppatore: Training con un altro Claude Code, e solo Carmine fa il merge

**Data:** 24 settembre 2026, dopo T19b (PR #111) e prima di T20
**Stato:** **decisa** (Carmine, 24 settembre 2026, quattro risposte in chat). Chiude la proposta §3.5 della nota
`2026-09-13-ordine-dei-moduli`, che resta valida per il resto.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: nessun meccanismo del prodotto cambia, ma cambia il modo in cui si
lavora sul repository — chi scrive che cosa, chi decide, chi unisce — e le regole di lavoro escono da un file privato.

## 1. Che cosa serve

- Carmine passa il modulo **Training (M3)** a un collega, **`dalberone`** (già collaboratore del repository con
  permesso `write`), che lavora con il suo Claude Code su branch suoi, **mentre Carmine chiude M2** (T20, poi T21).
- Il progetto deve restare **rigido**, soprattutto sul nucleo.
- **Restrizione assoluta, chiesta da Carmine:** su `main` unisce **solo Carmine**, e solo dopo che il suo Claude ha
  rivisto il lavoro dell'altro Claude. L'altro Claude **documenta** quello che fa, per rendere possibile la revisione.

## 2. Che cosa c'era

Guardato il 24 settembre, non supposto:

- **`main` non aveva nessuna protezione** né ruleset, e `dalberone` aveva già `write`: poteva fare push su `main`.
  Con `write` poteva anche fare push di un tag `v*`, che fa partire `release.yml`.
- **Le regole non viaggiavano con il repository.** `CLAUDE.md` e `.claude/` erano in `.gitignore` (piano §16.E: «file
  privato di Carmine»), e le lezioni pratiche stavano nelle memorie locali di Claude sulla macchina di Carmine. Il
  Claude del collega avrebbe visto solo il piano.
- **La CI di una PR gira con il workflow del branch**: una PR che modifica `build-test.yml` o `ArchitectureTests.cs`
  si dà da sola la spunta verde.
- Il piano e `HANDOFF.md` si aggiornano a ogni fase: due branch che alzano la stessa versione del piano vanno sempre in
  conflitto.

## 3. Le quattro risposte di Carmine

1. Il collega è **`dalberone`**.
2. **`CLAUDE.md` diventa pubblico, in inglese, versionato**, con le regole di tutti; le istruzioni personali di Carmine
   vanno in **`CLAUDE.local.md`**, che Claude Code legge da solo ed è già in `.gitignore`.
3. **Branch nel repository di Carmine** con ruleset, **non un fork**.
4. **T20 resta di Carmine e va avanti**; il collega parte dal **documento di design** di Training, che non tocca il
   codice (la stessa sequenza consigliata nella nota del 13 settembre §3.5).

## 4. Che cosa si fa

**Tre ruoli** (`CLAUDE.md` §0): **maintainer** (Carmine: merge, tag, piano, regole, pipeline), **revisore** (le
sessioni Claude di Carmine: rivedono e riferiscono; dopo il merge portano le decisioni nel piano), **collaboratore**
(`dalberone` e le sue sessioni: branch, PR, il suo modulo e i suoi documenti).

**Il blocco su GitHub**, che Carmine attiva come admin:

- ruleset **`main`**: PR obbligatoria, check `build-test` e `core-guard` obbligatori, niente force push né
  cancellazione, e **«Restrict updates»**: aggiorna `main` solo chi è nella lista di bypass, cioè il ruolo *Repository
  admin* — Carmine —, e anche lui solo attraverso una PR;
- ruleset **`release tags`**: creare, spostare o cancellare un tag `v*` solo al ruolo admin;
- `.github/CODEOWNERS`: tutto a `@SkyMistery`, con le zone del nucleo elencate a parte.

**Le regole nel repository:** `CLAUDE.md` (pubblico, inglese) con §0 sui ruoli e §9 su che cosa il collaboratore
scrive; `CONTRIBUTING.md` (inglese) con il lavoro di una fase, i test e le trappole prese dalle memorie; il template
della PR con la sezione **«For the reviewer»**; `CLAUDE.local.md` (privato) con la procedura di revisione.

**La guardia del nucleo** (`.github/workflows/core-guard.yml` + `.github/scripts/core-guard.sh`): su una PR che non è di
Carmine divide i file in tre. **Solo del maintainer** (regole, piano 00–06, `HANDOFF.md`, note già scritte,
`.github/`, `ArchitectureTests.cs`, il modulo `flightops`): il check fallisce comunque. **Nucleo** (`Core`, `Web`,
`web/src` fuori dai moduli, le lingue del nucleo, le dipendenze, i test e le fixture già esistenti): serve una **nota
nuova** in `decisions/` nella stessa PR. **Il resto** è il modulo del collaboratore. Gira con `pull_request_target` e
legge lo script da `main`, quindi una PR non può allentare la guardia che la giudica; non esegue niente della PR, legge
solo i nomi dei file. È un segnale per il revisore, **non il lucchetto**: il lucchetto è che unisce solo Carmine.

**I documenti del collaboratore:** `07-design-m3.md` (la prima PR, senza codice, approvata da Carmine prima di ogni
riga), `08-piano-implementazione-m3.md` con «Com'è andata» sotto ogni fase, **`HANDOFF-M3.md`** con «Che cosa ha
lasciato» a ogni fase, e le note. **Il piano 00 e `HANDOFF.md` li scrive solo il revisore**, dopo il merge, dalla sezione
«Da portare nel piano» di ogni nota: un solo autore del piano, niente conflitti di versione.

**Una decisione del collaboratore** è una nota «Proposta» con la raccomandazione; la domanda va a Carmine in un
commento sulla PR, Carmine risponde lì, e la nota riporta la risposta **con il link al commento**. Il revisore controlla
che a decidere sia stato Carmine e non una sessione.

**Un cambio al nucleo è una PR a sé**, prima del codice del modulo che lo usa, come T4 e T19a in M2.

**Per i test del modulo:** VID `790001–790099` (liberi il 24 settembre), slug `trn-test-`.

## 5. Le alternative scartate

| Alternativa | Perché no |
|---|---|
| Il collega su un **fork** | Più rigido per costruzione (nessun diritto su `main`), ma la CI delle PR da fork va approvata a mano a ogni giro e i branch non sono visibili dal repository. Carmine ha scelto il branch; il ruleset protegge anche dagli errori delle sessioni di Carmine. |
| Un **branch lungo** `m3/training` unito alla fine | Una revisione di mesi di lavoro in un colpo solo non è una revisione. Una fase, una PR, come M2. |
| Il collaboratore aggiorna il **piano** a ogni fase | Conflitti garantiti su versione e changelog con le fasi di M2 in parallelo. |
| Regole in un file a parte (`AGENTS.md`, `docs/internal/REGOLE-DI-LAVORO.md`) con `CLAUDE.md` che resta privato | Claude Code legge da solo solo `CLAUDE.md` e `CLAUDE.local.md`: un file che va ricordato di leggere è un file che una sessione prima o poi non legge. E sarebbero due copie delle stesse regole. |
| Vietare al collaboratore **ogni** file del nucleo | Contro la regola (b): un meccanismo che non basta si estende, non si aggira nel modulo. Il nucleo si tocca, ma dichiarato, con una nota e in una PR a sé. |
| Un hook di Claude Code che blocca `git push` su `main` | Vive sulla macchina del collega e si toglie con una riga; il ruleset vive su GitHub. |

## 6. Che cosa non è verificato

- **Che «Restrict updates» con il solo bypass del ruolo admin funzioni su un repository personale** (non di
  un'organizzazione) come sulla carta: si verifica con una PR di prova di `dalberone`, che non deve poter unire, e con
  un push diretto su `main`, che deve essere rifiutato.
- **Il primo giro di `core-guard` su GitHub**: lo script è provato in locale su cinque casi (solo modulo, nucleo senza
  nota, nucleo con nota, file del maintainer, questo stesso branch — che la guardia fermerebbe se non fosse di Carmine),
  ma `pull_request_target` legge il workflow da `main`, quindi il check esiste solo **dopo** il merge di questa PR.
- Le sessioni Claude di Carmine hanno gli stessi permessi GitHub di Carmine: che non uniscano una PR del collaboratore
  lo garantisce `CLAUDE.local.md`, non GitHub.

## 7. Da portare nel piano

- Versione **1.06**, riga di changelog.
- **§16.E**: `CLAUDE.md` non è più privato; `CLAUDE.local.md`; la guardia del nucleo accanto al template e ai test della
  spina dorsale.
- **§13**, M3: il modulo lo scrive `dalberone`, a partire dal design, con i documenti `07`/`08` e `HANDOFF-M3.md`.
- Nota `2026-09-13-ordine-dei-moduli`: lo stato di §3.5 punta qui.
