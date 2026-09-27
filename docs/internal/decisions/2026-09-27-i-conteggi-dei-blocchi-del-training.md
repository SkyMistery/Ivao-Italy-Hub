# I due conteggi dei blocchi, alzati dei quattro del training

**Data:** 27 settembre 2026 — fase A10b di M3
**Stato:** **decisa** (Carmine, 25 settembre 2026, [commento sulla PR #125][c125], risposta 2, come raccomandato in A0). Questa nota
non decide niente di nuovo: dice dove quella decisione è applicata.
**Regola applicata:** `CONTRIBUTING.md`, «A new block bumps two counts»; `CLAUDE.md` §0 regola 3 (nessun test che non si è scritto si
cambia per farlo passare) e regola 6 (una PR che tocca un file del nucleo porta una nota nuova, e `core-guard` la chiede).

[c125]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/125#issuecomment-5835026941

## 1. Che cosa

La fase A10b aggiunge i quattro blocchi Data del training — `training.upcomingSessions`, `training.myTraining`,
`training.trainerQueue`, `training.approvalQueue` (design M3 §4.3) —, ognuno con le sue due metà nella stessa PR. Due test condivisi
scrivono per esteso quanti blocchi esistono, di proposito: un blocco perso in un merge lo dice la CI, non una galleria più corta.

- `web/src/features/admin/uiKit.test.ts` (`the gallery shows the whole set of blocks the milestone declares`): `registry.blocks` da
  **38** a **42**.
- `tests/IvaoHub.IntegrationTests/DataBlockEndToEndTests.cs` (`EveryDataBlockTypeHasAProvider`): i blocchi Data da **13** a **17**.

In tutti e due cambiano il numero e, nel commento accanto che elenca da che cosa viene il numero, la frase che nomina i quattro del
training. Nient'altro: nessuna asserzione, nessun altro test.

## 2. Perché non è far passare un test

È il caso che `CONTRIBUTING.md` descrive già: un blocco nuovo alza i due conteggi. Il numero non si abbassa e non si aggira: sale dei
blocchi che la fase dichiara, e i due test continuano a fare il loro lavoro per il blocco dopo. Carmine l'ha deciso in anticipo, sulla
domanda che A0 aveva posto proprio per questo (`08`, A0 e A10 punto 1): «raising `uiKit.test.ts` and `DataBlockEndToEndTests` by the
blocks A10 adds is the expected change, not making a test pass».

## 3. Perché una nota

`core-guard` classifica i due file come nucleo — il primo sta in `web/src/features/`, il secondo è un test che c'era già sotto
`tests/` e non nomina il training —, e una PR che tocca il nucleo porta una nota nuova in `decisions/`. Carmine ha chiesto che fosse
breve e che rimandasse al suo commento: è questa.

## Da portare nel piano

Nessuna sezione del piano cambia: la decisione è già in `08-piano-implementazione-m3.md` (A0, «Com'è andata»; A10, punto 1). Dopo A10b i
due conteggi valgono **42** e **17**; A10c non aggiunge blocchi (gli esami entrano in `training.upcomingSessions` e in `/training`).
