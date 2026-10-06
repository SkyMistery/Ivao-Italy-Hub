# I conteggi dei blocchi e le colonne delle rotte (E4)

**Data:** 6 ottobre 2026 — fase E4 di M4 (il pubblico e le rotte), PR #222
**Stato:** **nessuna decisione nuova**: questa nota dice dove si applicano due decisioni già prese — i conteggi dei blocchi (Carmine,
[commento sulla #125][c125], come per il training in `2026-09-27-i-conteggi-dei-blocchi-del-training`) e le colonne di persona in
`ErasureTests` (la risposta 2 di Carmine sulla #187, nota `2026-09-29-la-persona-cancellata-nel-nucleo`, come per lo scheletro in
`2026-09-30-le-colonne-degli-eventi-in-erasuretests`) —, e la regola di tutte le fasi di `10-piano-implementazione-m4.md` («Un test
condiviso si tocca solo nei due casi in cui il test lo chiede»).
**Regola applicata:** `CLAUDE.md` §0 regola 3 (nessun test che non si è scritto si cambia per farlo passare) e regola 6 (una PR che
tocca un file del nucleo porta una nota nuova, e `core-guard` la chiede).

[c125]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/125#issuecomment-5835026941

## 1. Che cosa

La fase E4 aggiunge **un blocco Data**, `events.eventList` (design M4 §7.3), con le sue due metà nella stessa PR, e **una tabella**,
`evt_routes` (design M4 §1.4), nella migrazione additiva `AddEventRoutes` del contesto degli eventi. Tre test condivisi lo scrivono
per esteso, di proposito:

- `web/src/features/admin/uiKit.test.ts` (`the gallery shows the whole set of blocks the milestone declares`): `registry.blocks` da
  **42** a **43**;
- `tests/IvaoHub.IntegrationTests/DataBlockEndToEndTests.cs` (`EveryDataBlockTypeHasAProvider`): i blocchi Data da **17** a **18**;
- `tests/IvaoHub.IntegrationTests/ErasureTests.cs` (`TheColumnsThatNameAPersonAreTheOnesTheErasureKnows`): le due righe
  `evt_routes.created_by` ed `evt_routes.updated_by` (l'audit del nucleo: la rotta è `IAuditable`) nella lista `PersonColumnsOfTheHub`,
  in ordine, fra `evt_events.updated_by` e `fo_aircraft_groups.created_by`.

Nei primi due cambiano il numero e, nel commento accanto che elenca da che cosa viene, la frase che nomina il blocco degli eventi; nel
terzo le due righe. Nient'altro: nessuna asserzione, nessun altro test.

## 2. Perché non è far passare un test

È il caso che `CONTRIBUTING.md` descrive già («A new block bumps two counts») e quello per cui `ErasureTests` esiste: far pensare alla
cancellazione di ogni colonna nuova di una persona. A tutte e due le colonne delle rotte la cancellazione del nucleo scrive lo
pseudonimo, perché i nomi seguono la convenzione (`…By`), senza niente da scrivere nel modulo: una rotta è una riga dello staff, non
è **su** nessuno. I numeri salgono di quello che la fase dichiara, e i tre test continuano a fare il loro lavoro per il blocco e la
colonna dopo.

## 3. Perché una nota

`core-guard` classifica i tre file come nucleo — il primo sta in `web/src/features/`, gli altri due sono test che c'erano già sotto
`tests/` e non portano il nome degli eventi —, e una PR che tocca il nucleo porta una nota nuova in `decisions/`. È questa, breve come
quelle che la precedono.

## Da portare nel piano

Niente: le regole sono già in `10-piano-implementazione-m4.md` («Regole di tutte le fasi») e nel piano. Dopo E4 i due conteggi valgono
**43** e **18**, e la lista di `ErasureTests` ha sette righe `evt_`; i blocchi di E6b, E12 ed E13b li alzano ancora di uno ciascuno.
